using System.Text.Json;
using StackExchange.Redis;

namespace Weaver.Infrastructure.Realtime;

public record RunStatusMessage(Guid OwnerUserId, string Kind, Guid RunId, string Status);

/// <summary>
/// Scrape runs execute in the Worker process; workflow runs mostly execute in the Api process
/// (a manual "Run" click, an inbound webhook) but sometimes in the Worker (a cron trigger firing).
/// Either way, whoever's watching a run's status lives in a browser tab connected to the Api's
/// SignalR hub -- so both processes publish here, and only the Api subscribes, relaying onward to
/// whichever connected client actually owns the run. Redis pub/sub is already-present shared infra
/// for this, not a new moving part.
/// </summary>
public interface IRunStatusPublisher
{
    Task PublishAsync(Guid ownerUserId, string kind, Guid runId, string status, CancellationToken cancellationToken = default);
}

public class RunStatusPublisher : IRunStatusPublisher
{
    public const string ChannelName = "weaver:run-status";

    private readonly IConnectionMultiplexer _redis;

    public RunStatusPublisher(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task PublishAsync(Guid ownerUserId, string kind, Guid runId, string status, CancellationToken cancellationToken = default)
    {
        var message = new RunStatusMessage(ownerUserId, kind, runId, status);
        var json = JsonSerializer.Serialize(message);
        await _redis.GetSubscriber().PublishAsync(RedisChannel.Literal(ChannelName), json);
    }
}
