using Orchestrator.Domain;

namespace Orchestrator.Infrastructure.Scheduling;

public class SchedulerOptions
{
    public const string SectionName = "Scheduler";

    /// <summary>Max concurrent supervisor-lane runs (planning/verification), across all projects.</summary>
    public int SupervisorConcurrency { get; set; } = 3;

    /// <summary>Max concurrent worker-lane runs (implementation), across all projects. Keep low when the
    /// local model host is a single slow machine.</summary>
    public int WorkerConcurrency { get; set; } = 1;

    /// <summary>How often the scheduler tick looks for newly-runnable tasks.</summary>
    public TimeSpan TickInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Added on top of the lane's own RunTimeout to get that lane's lease duration, so a
    /// crashed holder is recovered shortly after its engine call should have timed out -- not after
    /// a single global duration sized for the slowest lane. See SchedulerOptionsExtensions.</summary>
    public TimeSpan LeaseBuffer { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Per-call timeout for supervisor (cloud model) engine runs.</summary>
    public TimeSpan SupervisorRunTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Per-call timeout for worker (local model) engine runs. Local models can be very slow.</summary>
    public TimeSpan WorkerRunTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Lease duration for a claimed task in the given lane: the lane's own RunTimeout plus a
    /// fixed buffer, so a crashed supervisor task (timeout 5m) is recovered in minutes rather than
    /// waiting out a single global duration sized for the much slower worker lane.</summary>
    public TimeSpan GetLeaseDuration(Lane lane) =>
        (lane == Lane.Supervisor ? SupervisorRunTimeout : WorkerRunTimeout) + LeaseBuffer;

    /// <summary>Base delay for the first retry after a failure. Doubled per subsequent retry, up
    /// to RetryBackoffMax, so a flaky/down model host gets hit with decreasing frequency instead
    /// of being hammered in a tight retry loop.</summary>
    public TimeSpan RetryBackoffBase { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Ceiling on the exponential retry backoff.</summary>
    public TimeSpan RetryBackoffMax { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Exponential backoff for the given retry attempt (1-indexed): RetryBackoffBase * 2^(retryCount-1),
    /// capped at RetryBackoffMax.</summary>
    public TimeSpan GetRetryBackoff(int retryCount)
    {
        if (retryCount <= 0)
        {
            return TimeSpan.Zero;
        }

        var shift = Math.Min(retryCount - 1, 30); // guard against overflow on pathological RetryCount values
        var backoff = RetryBackoffBase * Math.Pow(2, shift);
        return backoff > RetryBackoffMax ? RetryBackoffMax : backoff;
    }

    /// <summary>Consecutive engine-run failures for a given model before its circuit breaker
    /// opens -- protects against hammering a rate-limited or down model host with runs that are
    /// very likely to fail too.</summary>
    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    /// <summary>How long a model's circuit breaker stays open once tripped, before the next run
    /// is allowed through as a half-open probe.</summary>
    public TimeSpan CircuitBreakerCooldown { get; set; } = TimeSpan.FromMinutes(2);
}
