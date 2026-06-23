namespace Orchestrator.Domain;

public class Run
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public AgentTask? Task { get; set; }

    public EngineType Engine { get; set; }
    public required string Model { get; set; }
    public RunStatus Status { get; set; } = RunStatus.Running;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }

    public decimal? CostUsd { get; set; }
    public int? TokensIn { get; set; }
    public int? TokensOut { get; set; }

    /// <summary>Final structured outcome (diff summary, verdict, error) as JSON.</summary>
    public string? ExitSummaryJson { get; set; }

    public List<TaskEvent> Events { get; set; } = [];
}
