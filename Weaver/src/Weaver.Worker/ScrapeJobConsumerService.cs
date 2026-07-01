using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Weaver.Domain;
using Weaver.Infrastructure.Persistence;
using Weaver.Infrastructure.Queue;
using Weaver.Scraping;

namespace Weaver.Worker;

/// <summary>
/// Pulls scrape jobs off the shared Redis stream as one named consumer in the "weaver-workers"
/// group. Any number of these can run at once (one per process/machine) -- the consumer group
/// guarantees a given job is only ever handed to one of them at a time, and ReclaimStaleAsync
/// picks back up jobs whose owning instance died mid-processing (idle longer than LeaseDuration).
/// </summary>
public class ScrapeJobConsumerService : BackgroundService
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private const int MaxAttempts = 3;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScrapeJobQueue _queue;
    private readonly ILogger<ScrapeJobConsumerService> _logger;
    private readonly string _consumerName;

    public ScrapeJobConsumerService(IServiceScopeFactory scopeFactory, IScrapeJobQueue queue, ILogger<ScrapeJobConsumerService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
        _consumerName = Environment.GetEnvironmentVariable("WEAVER_INSTANCE_ID") ?? $"{Environment.MachineName}-{Guid.NewGuid():N}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _queue.EnsureGroupExistsAsync(stoppingToken);
        _logger.LogInformation("Scrape job consumer '{Consumer}' started", _consumerName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var reclaimed = await _queue.ReclaimStaleAsync(_consumerName, LeaseDuration, 10, stoppingToken);
                foreach (var job in reclaimed)
                {
                    await ProcessAsync(job, stoppingToken);
                }

                var fresh = await _queue.ReadAsync(_consumerName, 10, TimeSpan.FromSeconds(5), stoppingToken);
                foreach (var job in fresh)
                {
                    await ProcessAsync(job, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (RedisServerException ex) when (ex.Message.StartsWith("NOGROUP"))
            {
                // The stream/group vanished from under us (e.g. Redis data was reset). Self-heal
                // rather than looping this error forever -- once recreated, reads resume normally.
                _logger.LogWarning("Consumer group missing, recreating: {Message}", ex.Message);
                await _queue.EnsureGroupExistsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scrape job consumer loop failed, retrying shortly");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ProcessAsync(QueuedScrapeJob queued, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeaverDbContext>();

        var job = await db.ScrapeJobs.FirstOrDefaultAsync(j => j.Id == queued.Message.ScrapeJobId, cancellationToken);
        if (job is null)
        {
            // Nothing to reconcile against (e.g. deleted) -- ack so it stops being redelivered.
            await _queue.AcknowledgeAsync(queued.StreamMessageId, cancellationToken);
            return;
        }

        if (job.Status == ScrapeJobStatus.Completed)
        {
            await _queue.AcknowledgeAsync(queued.StreamMessageId, cancellationToken);
            return;
        }

        job.Status = ScrapeJobStatus.Leased;
        job.LeaseOwner = _consumerName;
        job.LeaseExpiresAt = DateTimeOffset.UtcNow.Add(LeaseDuration);
        job.Attempts++;
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var runner = scope.ServiceProvider.GetRequiredService<ScrapeRunner>();
            var run = await runner.ExecuteAsync(job.ScrapingProjectId, job.TriggeredBy, job.WorkflowRunId, cancellationToken);

            job.ScrapeRunId = run.Id;
            job.Status = run.Status == RunStatus.Succeeded ? ScrapeJobStatus.Completed : ScrapeJobStatus.Failed;
            job.ErrorMessage = run.ErrorMessage;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await _queue.AcknowledgeAsync(queued.StreamMessageId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scrape job {JobId} failed on attempt {Attempt}", job.Id, job.Attempts);
            job.ErrorMessage = ex.Message;

            if (job.Attempts >= MaxAttempts)
            {
                job.Status = ScrapeJobStatus.Failed;
                job.CompletedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                await _queue.AcknowledgeAsync(queued.StreamMessageId, cancellationToken);
            }
            else
            {
                // Leave un-acked: it stays in the group's pending list and ReclaimStaleAsync will
                // hand it to some consumer again once this lease's idle time is exceeded.
                job.Status = ScrapeJobStatus.Queued;
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
