namespace Weaver.Domain;

public class Workflow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;

    /// <summary>How many days to keep completed workflow runs before a background job purges them.
    /// Null means keep forever.</summary>
    public int? RunRetentionDays { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<WorkflowNode> Nodes { get; set; } = new();
    public List<WorkflowEdge> Edges { get; set; } = new();
    public List<WorkflowRun> Runs { get; set; } = new();
}

/// <summary>
/// One block on the canvas. Type is a registry key (e.g. "trigger.cron", "action.sendEmail")
/// resolved against Weaver.Workflows' INodeHandler registry. Config is handler-specific JSON.
/// </summary>
public class WorkflowNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkflowId { get; set; }

    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = "{}";

    /// <summary>When true, this node isn't executed at all; its input passes straight through as its output, as if it were never in the graph.</summary>
    public bool IsDisabled { get; set; }

    /// <summary>Extra attempts after an initial failure (0 = no retry). Ignored by trigger nodes.</summary>
    public int MaxRetries { get; set; }

    /// <summary>Delay between retry attempts, in milliseconds.</summary>
    public int RetryDelayMs { get; set; } = 1000;

    public double PositionX { get; set; }
    public double PositionY { get; set; }
}

/// <summary>
/// A connection between two nodes. SourceHandle distinguishes multiple outputs from one node
/// (e.g. a Condition block's "true"/"false" branches, or "success"/"error").
/// </summary>
public class WorkflowEdge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkflowId { get; set; }

    public Guid SourceNodeId { get; set; }
    public string? SourceHandle { get; set; }

    public Guid TargetNodeId { get; set; }
    public string? TargetHandle { get; set; }
}

public class WorkflowRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkflowId { get; set; }
    public Workflow? Workflow { get; set; }

    public RunStatus Status { get; set; } = RunStatus.Pending;
    public TriggerKind TriggerKind { get; set; }
    public string? TriggerNodeType { get; set; }

    /// <summary>Which trigger node started this run -- lets a finished run be replayed with the
    /// same payload even if the workflow has several triggers.</summary>
    public Guid? TriggerNodeId { get; set; }

    /// <summary>JSON payload the trigger produced (cron tick, HTTP body, event payload, ...).</summary>
    public string TriggerPayloadJson { get; set; } = "{}";

    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public List<NodeRun> NodeRuns { get; set; } = new();
}

public class NodeRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkflowRunId { get; set; }
    public Guid WorkflowNodeId { get; set; }

    public string NodeType { get; set; } = string.Empty;
    public RunStatus Status { get; set; } = RunStatus.Pending;

    public string InputJson { get; set; } = "{}";
    public string? OutputJson { get; set; }
    public string? ErrorMessage { get; set; }
    public string? LogText { get; set; }

    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
