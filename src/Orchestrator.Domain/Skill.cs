namespace Orchestrator.Domain;

/// <summary>A reusable prompt/playbook authored by the supervisor and injected into future prompts.</summary>
public class Skill
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    public required string Name { get; set; }
    public required string Content { get; set; }
    public Guid? CreatedByRunId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
