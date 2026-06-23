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

    /// <summary>Lease duration granted to a claimed task; must exceed the slowest expected local-model
    /// call so a healthy run is never mistaken for a crashed worker.</summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(45);

    /// <summary>Per-call timeout for supervisor (cloud model) engine runs.</summary>
    public TimeSpan SupervisorRunTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Per-call timeout for worker (local model) engine runs. Local models can be very slow.</summary>
    public TimeSpan WorkerRunTimeout { get; set; } = TimeSpan.FromMinutes(30);
}
