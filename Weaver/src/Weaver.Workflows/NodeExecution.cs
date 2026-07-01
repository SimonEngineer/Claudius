using System.Text.Json.Nodes;

namespace Weaver.Workflows;

public class NodeExecutionContext
{
    public required Guid WorkflowRunId { get; init; }
    public required Guid WorkflowNodeId { get; init; }
    public required string NodeType { get; init; }

    /// <summary>Handler-specific config, deserialized from WorkflowNode.ConfigJson.</summary>
    public required JsonNode? Config { get; init; }

    /// <summary>Merged output(s) of upstream node(s), or the original trigger payload for trigger nodes.</summary>
    public required JsonNode? Input { get; init; }

    public required IServiceProvider Services { get; init; }
    public required Action<string> Log { get; init; }
    public CancellationToken CancellationToken { get; init; }
}

public record NodeExecutionResult(bool Success, JsonNode? Output, string? ErrorMessage = null, string? Branch = null)
{
    public static NodeExecutionResult Ok(JsonNode? output, string? branch = null) => new(true, output, null, branch);
    public static NodeExecutionResult Fail(string errorMessage) => new(false, null, errorMessage);
}

/// <summary>
/// One block type in the palette (e.g. "action.sendEmail"). Trigger handlers (Type starting with
/// "trigger.") are only ever invoked as the first node of a run, where ExecuteAsync just reshapes
/// the external payload that already caused the run to start; everything downstream is a normal
/// action/condition handler reacting to upstream output.
/// </summary>
public interface INodeHandler
{
    string Type { get; }

    Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context);
}
