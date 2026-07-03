namespace Weaver.Domain;

/// <summary>
/// A point-in-time snapshot of a workflow's full graph, captured automatically before each update
/// so an accidental edit can be rolled back. SnapshotJson uses the same portable format as the
/// workflow export file (secrets redacted the same way).
/// </summary>
public class WorkflowRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkflowId { get; set; }
    public Guid OwnerUserId { get; set; }

    /// <summary>Workflow name at snapshot time (the graph inside may carry a different name than the current one).</summary>
    public string WorkflowName { get; set; } = string.Empty;

    public string SnapshotJson { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
