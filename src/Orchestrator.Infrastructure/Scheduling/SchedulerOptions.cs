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
}
