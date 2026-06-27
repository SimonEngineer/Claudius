using System.Text.Json;
using Orchestrator.Infrastructure.Persistence;

namespace Orchestrator.Infrastructure.Workflows;

/// <summary>Everything an action node needs: its own config, the payload that started the run,
/// and a scoped DbContext for nodes (like the code block) that need to read orchestrator data.</summary>
public class WorkflowNodeContext(JsonElement config, JsonElement triggerPayload, OrchestratorDbContext db, CancellationToken ct)
{
    public JsonElement Config { get; } = config;
    public JsonElement TriggerPayload { get; } = triggerPayload;
    public OrchestratorDbContext Db { get; } = db;
    public CancellationToken CancellationToken { get; } = ct;
}

/// <summary>One starter block. Implementations are stateless and registered by Type in DI; the
/// engine looks one up per action node it walks. Triggers don't implement this -- they're handled
/// by the engine itself (matching event name / cron / webhook) since they have no "execute" step.</summary>
public interface IWorkflowNodeExecutor
{
    string Type { get; }

    /// <returns>A short human-readable result, recorded in the run's step log.</returns>
    Task<string> ExecuteAsync(WorkflowNodeContext context);
}
