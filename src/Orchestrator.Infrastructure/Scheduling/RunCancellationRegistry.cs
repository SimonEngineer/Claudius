using System.Collections.Concurrent;

namespace Orchestrator.Infrastructure.Scheduling;

/// <summary>
/// Tracks the in-flight CancellationTokenSource for each currently-running task so a user-initiated
/// cancel request can stop the engine adapter call without affecting persistence operations (which
/// always use CancellationToken.None, deliberately, so a cancel can't corrupt mid-write state).
/// </summary>
public interface IRunCancellationRegistry
{
    CancellationToken Register(Guid taskId);
    void Unregister(Guid taskId);
    bool TryCancel(Guid taskId);
}

public class RunCancellationRegistry : IRunCancellationRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _sources = new();

    public CancellationToken Register(Guid taskId)
    {
        var cts = new CancellationTokenSource();
        _sources[taskId] = cts;
        return cts.Token;
    }

    public void Unregister(Guid taskId)
    {
        if (_sources.TryRemove(taskId, out var cts))
        {
            cts.Dispose();
        }
    }

    public bool TryCancel(Guid taskId)
    {
        if (!_sources.TryGetValue(taskId, out var cts))
        {
            return false;
        }

        cts.Cancel();
        return true;
    }
}
