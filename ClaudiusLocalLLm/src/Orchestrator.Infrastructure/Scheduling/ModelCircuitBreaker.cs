using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Orchestrator.Infrastructure.Scheduling;

/// <summary>
/// Per-model (not per-project) failure tracking so a rate-limited or down model host doesn't get
/// hammered with engine runs that are very likely to fail too -- after enough consecutive
/// failures the breaker opens and TaskRunnerJob requeues the task without spending an attempt,
/// rather than burning RetryCount on calls doomed before they start.
/// </summary>
public interface IModelCircuitBreaker
{
    bool IsOpen(string model);
    DateTimeOffset? OpenUntil(string model);
    void RecordSuccess(string model);
    void RecordFailure(string model);
}

public class ModelCircuitBreaker(IOptions<SchedulerOptions> options) : IModelCircuitBreaker
{
    private sealed class State
    {
        public int ConsecutiveFailures;
        public DateTimeOffset? OpenUntil;
    }

    private readonly ConcurrentDictionary<string, State> _states = new();

    public bool IsOpen(string model)
    {
        if (!_states.TryGetValue(model, out var state) || state.OpenUntil is null)
        {
            return false;
        }

        if (state.OpenUntil <= DateTimeOffset.UtcNow)
        {
            // Half-open: let the next run through as a probe rather than staying open forever.
            state.OpenUntil = null;
            return false;
        }

        return true;
    }

    public DateTimeOffset? OpenUntil(string model) =>
        _states.TryGetValue(model, out var state) ? state.OpenUntil : null;

    public void RecordSuccess(string model)
    {
        if (_states.TryGetValue(model, out var state))
        {
            state.ConsecutiveFailures = 0;
            state.OpenUntil = null;
        }
    }

    public void RecordFailure(string model)
    {
        var state = _states.GetOrAdd(model, _ => new State());
        var failures = Interlocked.Increment(ref state.ConsecutiveFailures);
        if (failures >= options.Value.CircuitBreakerFailureThreshold)
        {
            state.OpenUntil = DateTimeOffset.UtcNow.Add(options.Value.CircuitBreakerCooldown);
        }
    }
}
