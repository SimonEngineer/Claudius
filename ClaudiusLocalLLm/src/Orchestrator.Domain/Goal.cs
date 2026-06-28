namespace Orchestrator.Domain;

public class Goal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    public required string Description { get; set; }
    public GoalStatus Status { get; set; } = GoalStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<AgentTask> Tasks { get; set; } = [];
}
