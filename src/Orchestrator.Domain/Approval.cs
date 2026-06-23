namespace Orchestrator.Domain;

public class Approval
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public AgentTask? Task { get; set; }
    public Guid? RunId { get; set; }

    public required string Question { get; set; }
    /// <summary>Suggested answer options, if any, as a JSON string array.</summary>
    public string? OptionsJson { get; set; }

    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public string? Answer { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
}
