using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Weaver.Domain;
using Weaver.Infrastructure.Persistence;
using Weaver.Workflows;

namespace Weaver.Worker;

/// <summary>
/// Polls every enabled workflow's "trigger.feed" nodes (config: { feedUrl, intervalMinutes }) and
/// starts a run per NEW feed entry, with the entry as the trigger payload. Redis keeps the state:
/// a per-node poll lock spaces polls to the configured interval across worker instances, and a
/// per-node set of seen entry ids provides dedupe. The first poll of a feed only seeds the seen
/// set -- otherwise adding a trigger to an established feed would fire a run per historical entry.
/// </summary>
public class FeedTriggerSchedulerService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(60);
    private const int MaxRunsPerPoll = 10;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionMultiplexer _redis;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FeedTriggerSchedulerService> _logger;

    public FeedTriggerSchedulerService(
        IServiceScopeFactory scopeFactory,
        IConnectionMultiplexer redis,
        IHttpClientFactory httpClientFactory,
        ILogger<FeedTriggerSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _redis = redis;
        _httpClientFactory = httpClientFactory;
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
                _logger.LogError(ex, "Feed trigger tick failed");
            }

            await Task.Delay(TickInterval, stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeaverDbContext>();
        var engine = scope.ServiceProvider.GetRequiredService<IWorkflowExecutionEngine>();

        var feedNodes = await db.WorkflowNodes
            .Where(n => n.Type == "trigger.feed" && !n.IsDisabled)
            .Join(db.Workflows.Where(w => w.IsEnabled), n => n.WorkflowId, w => w.Id, (n, w) => n)
            .ToListAsync(cancellationToken);

        var redis = _redis.GetDatabase();

        foreach (var node in feedNodes)
        {
            var config = ParseConfig(node.ConfigJson);
            var feedUrl = config?["feedUrl"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(feedUrl) || !Uri.TryCreate(feedUrl, UriKind.Absolute, out _))
            {
                continue;
            }

            var intervalMinutes = Math.Clamp(config?["intervalMinutes"]?.GetValue<int>() ?? 5, 1, 24 * 60);
            var pollLockKey = $"weaver:feed:polled:{node.Id}";
            if (!await redis.StringSetAsync(pollLockKey, "1", TimeSpan.FromMinutes(intervalMinutes), When.NotExists))
            {
                continue; // Polled recently (by this or another worker instance).
            }

            List<FeedEntry> entries;
            try
            {
                var client = _httpClientFactory.CreateClient("feed-poller");
                var xml = await client.GetStringAsync(feedUrl, cancellationToken);
                entries = FeedParser.Parse(xml);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Feed poll failed for {FeedUrl}: {Error}", feedUrl, ex.Message);
                continue;
            }

            var seenKey = $"weaver:feed:seen:{node.Id}";
            var isFirstPoll = !await redis.KeyExistsAsync(seenKey);
            var fired = 0;

            foreach (var entry in entries)
            {
                var isNew = await redis.SetAddAsync(seenKey, entry.Id);
                if (!isNew || isFirstPoll || fired >= MaxRunsPerPoll)
                {
                    continue;
                }

                fired++;
                var payload = new JsonObject
                {
                    ["id"] = entry.Id,
                    ["title"] = entry.Title,
                    ["link"] = entry.Link,
                    ["published"] = entry.Published,
                    ["summary"] = entry.Summary,
                    ["feedUrl"] = feedUrl,
                };

                _logger.LogInformation("Feed trigger {NodeId} firing for entry \"{Title}\"", node.Id, entry.Title);
                await engine.StartRunFromNodeAsync(node.WorkflowId, node.Id, TriggerKind.Event, payload, cancellationToken);
            }

            if (isFirstPoll)
            {
                _logger.LogInformation("Feed trigger {NodeId} seeded {Count} existing entrie(s) from {FeedUrl}", node.Id, entries.Count, feedUrl);
            }
        }
    }

    private static JsonNode? ParseConfig(string configJson)
    {
        try
        {
            return string.IsNullOrWhiteSpace(configJson) ? null : JsonNode.Parse(configJson);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
