namespace Orchestrator.Domain;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string RepoPath { get; set; }
    public string? GitRemote { get; set; }

    /// <summary>Model name as routed through the LLM gateway, e.g. "ollama/qwen2.5-coder:32b".</summary>
    public required string LocalModel { get; set; }

    /// <summary>Model name as routed through the LLM gateway, e.g. "anthropic/claude-sonnet-4-6".</summary>
    public required string CloudModel { get; set; }

    public int MaxWorkerConcurrency { get; set; } = 1;
    public int Priority { get; set; } = 0;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<Goal> Goals { get; set; } = [];
    public List<AgentTask> Tasks { get; set; } = [];
    public List<Skill> Skills { get; set; } = [];
}
