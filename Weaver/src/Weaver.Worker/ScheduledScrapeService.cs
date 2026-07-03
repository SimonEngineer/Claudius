using Cronos;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Weaver.Domain;
using Weaver.Infrastructure.Persistence;
using Weaver.Infrastructure.Queue;

namespace Weaver.Worker;

/// <summary>
/// Ticks like the cron trigger scheduler, but for scraping projects that carry their own
/// ScheduleCron -- so a project can run on a cadence without wrapping it in a workflow. Uses the
/// same Redis SETNX-per-occurrence lock so exactly one worker instance enqueues each occurrence,
/// and routes through the normal distributed job queue rather than scraping inline.
/// </summary>
public class ScheduledScrapeService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ScheduledScrapeService> _logger;

    public ScheduledScrapeService(IServiceScopeFactory scopeFactory, IConnectionMultiplexer redis, ILogger<ScheduledScrapeService> logger)
    {
        _scopeFactory = scopeFactory;
        _redis = redis;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled scrape tick failed");
            }

            await Task.Delay(TickInterval, stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeaverDbContext>();
        var queue = scope.ServiceProvider.GetRequiredService<IScrapeJobQueue>();

        var scheduled = await db.ScrapingProjects
            .Where(p => p.IsEnabled && p.ScheduleCron != null && p.ScheduleCron != "")
            .Select(p => new { p.Id, p.Name, p.ScheduleCron })
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var windowStart = now.AddMinutes(-1);

        foreach (var project in scheduled)
        {
            CronExpression cron;
            try
            {
                cron = CronExpression.Parse(project.ScheduleCron!);
            }
            catch (CronFormatException)
            {
                _logger.LogWarning("Project {ProjectId} has an invalid schedule cron '{Expression}'", project.Id, project.ScheduleCron);
                continue;
            }

            var occurrence = cron.GetNextOccurrence(windowStart, inclusive: false);
            if (occurrence is null || occurrence > now)
            {
                continue;
            }

            var minuteBucket = occurrence.Value.ToString("yyyyMMddHHmm");
            var lockKey = $"weaver:scrape-schedule:fired:{project.Id}:{minuteBucket}";
            var acquired = await _redis.GetDatabase().StringSetAsync(lockKey, "1", TimeSpan.FromMinutes(5), When.NotExists);
            if (!acquired)
            {
                continue;
            }

            _logger.LogInformation("Enqueueing scheduled scrape for project {ProjectName} (occurrence {Occurrence})", project.Name, occurrence);

            var job = new ScrapeJob { ScrapingProjectId = project.Id, TriggeredBy = TriggerKind.Schedule, Status = ScrapeJobStatus.Queued };
            db.ScrapeJobs.Add(job);
            await db.SaveChangesAsync(cancellationToken);

            var streamId = await queue.EnqueueAsync(new ScrapeJobMessage(job.Id, project.Id, TriggerKind.Schedule, null), cancellationToken);
            job.StreamMessageId = streamId;
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
