namespace Orchestrator.Domain;

/// <summary>A record of a human or system action that changed project/task/approval state,
/// so "who approved/rejected/paused/cancelled what" can be reconstructed after the fact.</summary>
public class AuditLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ProjectId { get; set; }
    public Guid? TaskId { get; set; }

    public required string Action { get; set; }
    public string? Actor { get; set; }
    public string? Details { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
