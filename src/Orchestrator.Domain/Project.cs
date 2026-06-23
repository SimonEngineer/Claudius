namespace Orchestrator.Domain;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string RepoPath { get; set; }
    public string? GitRemote { get; set; }

    /// <summary>
    /// Model id for the worker lane (implementation), as routed through the LLM gateway.
    /// Backend-agnostic -- e.g. "ollama/qwen2.5-coder:32b", "localai/qwen2.5-coder", or a
    /// cloud id like "anthropic/claude-haiku-4-5" if you want the worker lane in the cloud too.
    /// </summary>
    public required string WorkerModel { get; set; }

    /// <summary>
    /// Model id for the supervisor lane (plan/verify), as routed through the LLM gateway and
    /// driven by the Claude Code CLI. Defaults to a local model via LocalAI/Ollama so the whole
    /// pipeline can run fully offline; point it at "anthropic/claude-sonnet-4-6" to opt into the
    /// cloud for planning/verification instead. Note: Claude Code's tool-calling format is tuned
    /// for Claude models, so small/local models may be less reliable in this role than in the
    /// worker lane -- prefer a capable local model (32B+ class) here.
    /// </summary>
    public required string SupervisorModel { get; set; }

    public int MaxWorkerConcurrency { get; set; } = 1;
    public int Priority { get; set; } = 0;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<Goal> Goals { get; set; } = [];
    public List<AgentTask> Tasks { get; set; } = [];
    public List<Skill> Skills { get; set; } = [];
}
