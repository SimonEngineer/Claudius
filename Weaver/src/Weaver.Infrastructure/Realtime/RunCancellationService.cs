using System.Collections.Concurrent;
using StackExchange.Redis;

namespace Weaver.Infrastructure.Realtime;

/// <summary>
/// Lets a run started in ANY process (Api or Worker) be cancelled from the Api: each executing
/// process registers a local CancellationTokenSource per run id, and cancel requests are broadcast
/// over a Redis pub/sub channel so whichever process actually owns the run sees it and cancels.
/// A request for a run nobody owns (already finished, or its process died) is a harmless no-op.
/// </summary>
public interface IRunCancellationService
{
    /// <summary>Registers a run as executing in this process; dispose the returned registration when the run ends.</summary>
    RunCancellationRegistration Register(Guid runId, CancellationToken upstream);

    /// <summary>Broadcasts a cancel request for the run to every process.</summary>
    Task RequestCancelAsync(Guid runId, CancellationToken cancellationToken = default);
}

public sealed class RunCancellationRegistration : IDisposable
{
    private readonly Action _onDispose;

    public RunCancellationRegistration(CancellationToken token, Action onDispose)
    {
        Token = token;
        _onDispose = onDispose;
    }

    /// <summary>Cancelled when either the upstream token fires or a cancel request arrives for this run.</summary>
    public CancellationToken Token { get; }

    public void Dispose() => _onDispose();
}

public class RunCancellationService : IRunCancellationService, IDisposable
{
    private const string Channel = "weaver:run-cancel";

    private readonly IConnectionMultiplexer _redis;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _active = new();

    public RunCancellationService(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _redis.GetSubscriber().Subscribe(RedisChannel.Literal(Channel), (_, message) =>
        {
            if (Guid.TryParse(message, out var runId) && _active.TryGetValue(runId, out var cts))
            {
                cts.Cancel();
            }
        });
    }

    public RunCancellationRegistration Register(Guid runId, CancellationToken upstream)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(upstream);
        _active[runId] = cts;
        return new RunCancellationRegistration(cts.Token, () =>
        {
            _active.TryRemove(runId, out _);
            cts.Dispose();
        });
    }

    public Task RequestCancelAsync(Guid runId, CancellationToken cancellationToken = default) =>
        _redis.GetSubscriber().PublishAsync(RedisChannel.Literal(Channel), runId.ToString());

    public void Dispose() => _redis.GetSubscriber().Unsubscribe(RedisChannel.Literal(Channel));
}
