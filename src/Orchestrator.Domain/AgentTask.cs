namespace Orchestrator.Domain;

/// <summary>
/// A unit of work tracked by the scheduler. Named AgentTask (not Task) to avoid
/// colliding with System.Threading.Tasks.Task.
/// </summary>
public class AgentTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    public Guid? GoalId { get; set; }
    public Goal? Goal { get; set; }

    public Guid? ParentTaskId { get; set; }
    public AgentTask? ParentTask { get; set; }

    public required string Title { get; set; }
    public string? Description { get; set; }

    public Lane Lane { get; set; } = Lane.Worker;
    public TaskState State { get; set; } = TaskState.Queued;
    public int Priority { get; set; } = 0;
    public int RetryCount { get; set; } = 0;
    public const int MaxRetries = 3;

    /// <summary>Plan produced by the supervisor: steps, files to touch, etc. Stored as JSON.</summary>
    public string? PlanJson { get; set; }

    /// <summary>Acceptance criteria the supervisor checks against during verification. Stored as JSON.</summary>
    public string? AcceptanceCriteriaJson { get; set; }

    /// <summary>Worker id (process/lease holder) currently processing this task, if any.</summary>
    public string? LockedBy { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<Run> Runs { get; set; } = [];
    public List<Approval> Approvals { get; set; } = [];
}
