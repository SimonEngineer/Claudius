namespace Orchestrator.Domain;

/// <summary>
/// A user-authored automation: a graph of trigger/action nodes stored as one JSON blob (the
/// graph shape doesn't map cleanly onto relational tables, and the whole thing is always read/
/// written together by the GUI builder and the engine). Fired either by a matching event raised
/// from code (<c>IWorkflowEngine.TriggerEventAsync</c>), a cron schedule, or an inbound webhook.
/// </summary>
public class Workflow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;

    /// <summary>{"nodes":[{"id","type","config","position"}],"edges":[{"from","to"}]} -- see
    /// WorkflowDefinition for the parsed shape.</summary>
    public required string DefinitionJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<WorkflowRun> Runs { get; set; } = [];
}

public enum WorkflowRunStatus
{
    Running,
    Succeeded,
    Failed
}

/// <summary>One execution of a Workflow, recording what triggered it and a per-node trail so the
/// editor's run history can show exactly which step (if any) failed.</summary>
public class WorkflowRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkflowId { get; set; }
    public Workflow? Workflow { get; set; }

    public WorkflowRunStatus Status { get; set; } = WorkflowRunStatus.Running;
    public string? TriggerPayloadJson { get; set; }

    /// <summary>JSON array of {nodeId, type, status, output, error, startedAt, finishedAt}.</summary>
    public string StepLogJson { get; set; } = "[]";
    public string? ErrorMessage { get; set; }

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }
}
