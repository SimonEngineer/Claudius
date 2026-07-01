using Cronos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Weaver.Domain;
using Weaver.Infrastructure.Persistence;
using Weaver.Workflows;

namespace Weaver.Worker;

/// <summary>
/// Ticks once a minute, finds every enabled workflow's "trigger.cron" nodes, and fires any whose
/// cron expression has an occurrence in the just-elapsed minute. Because every worker instance
/// runs this same tick independently, a Redis SETNX-with-expiry lock keyed by node+minute-bucket
/// ensures only the first instance to see a given occurrence actually starts the run -- the rest
/// see the key already exists and skip it.
/// </summary>
public class CronTriggerSchedulerService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<CronTriggerSchedulerService> _logger;

    public CronTriggerSchedulerService(IServiceScopeFactory scopeFactory, IConnectionMultiplexer redis, ILogger<CronTriggerSchedulerService> logger)
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
                _logger.LogError(ex, "Cron scheduler tick failed");
            }

            await Task.Delay(TickInterval, stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeaverDbContext>();
        var engine = scope.ServiceProvider.GetRequiredService<IWorkflowExecutionEngine>();

        var cronNodes = await db.WorkflowNodes
            .Where(n => n.Type == "trigger.cron")
            .Join(db.Workflows.Where(w => w.IsEnabled), n => n.WorkflowId, w => w.Id, (n, w) => n)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var windowStart = now.AddMinutes(-1);

        foreach (var node in cronNodes)
        {
            var expression = ExtractCronExpression(node.ConfigJson);
            if (string.IsNullOrWhiteSpace(expression))
            {
                continue;
            }

            CronExpression cron;
            try
            {
                cron = CronExpression.Parse(expression);
            }
            catch (CronFormatException)
            {
                _logger.LogWarning("Node {NodeId} has an invalid cron expression '{Expression}'", node.Id, expression);
                continue;
            }

            var occurrence = cron.GetNextOccurrence(windowStart, inclusive: false);
            if (occurrence is null || occurrence > now)
            {
                continue;
            }

            var minuteBucket = occurrence.Value.ToString("yyyyMMddHHmm");
            var lockKey = $"weaver:cron:fired:{node.Id}:{minuteBucket}";
            var db0 = _redis.GetDatabase();
            var acquired = await db0.StringSetAsync(lockKey, "1", TimeSpan.FromMinutes(5), When.NotExists);
            if (!acquired)
            {
                continue;
            }

            _logger.LogInformation("Firing cron trigger {NodeId} for occurrence {Occurrence}", node.Id, occurrence);
            await engine.StartRunFromNodeAsync(node.WorkflowId, node.Id, TriggerKind.Schedule,
                System.Text.Json.Nodes.JsonValue.Create(occurrence.Value.ToString("O")), cancellationToken);
        }
    }

    private static string? ExtractCronExpression(string configJson)
    {
        try
        {
            var node = string.IsNullOrWhiteSpace(configJson) ? null : System.Text.Json.Nodes.JsonNode.Parse(configJson);
            return node?["cronExpression"]?.GetValue<string>();
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
