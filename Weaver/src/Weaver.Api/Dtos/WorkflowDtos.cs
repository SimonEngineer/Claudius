using Weaver.Domain;
using Weaver.Infrastructure.Security;

namespace Weaver.Api.Dtos;

public record WorkflowNodeDto(
    Guid Id, string Type, string Name, System.Text.Json.Nodes.JsonNode? Config,
    bool IsDisabled, int MaxRetries, int RetryDelayMs, double PositionX, double PositionY)
{
    /// <summary>Decrypts any encrypted-at-rest fields (e.g. a Discord webhook URL) back to plaintext
    /// so the workflow builder can display and re-edit them like any other config value.</summary>
    public static WorkflowNodeDto FromEntity(WorkflowNode n, ISensitiveConfigProtector protector)
    {
        var configJson = string.IsNullOrWhiteSpace(n.ConfigJson) ? null : protector.DecryptForUse(n.Type, n.ConfigJson);
        return new(
            n.Id, n.Type, n.Name,
            configJson is null ? null : System.Text.Json.Nodes.JsonNode.Parse(configJson),
            n.IsDisabled, n.MaxRetries, n.RetryDelayMs, n.PositionX, n.PositionY);
    }
}

public record WorkflowEdgeDto(Guid Id, Guid SourceNodeId, string? SourceHandle, Guid TargetNodeId, string? TargetHandle)
{
    public static WorkflowEdgeDto FromEntity(WorkflowEdge e) => new(e.Id, e.SourceNodeId, e.SourceHandle, e.TargetNodeId, e.TargetHandle);
}

public record WorkflowDto(Guid Id, string Name, string? Description, bool IsEnabled, DateTimeOffset UpdatedAt, List<WorkflowNodeDto> Nodes, List<WorkflowEdgeDto> Edges)
{
    public static WorkflowDto FromEntity(Workflow w, ISensitiveConfigProtector protector) => new(
        w.Id, w.Name, w.Description, w.IsEnabled, w.UpdatedAt,
        w.Nodes.Select(n => WorkflowNodeDto.FromEntity(n, protector)).ToList(),
        w.Edges.Select(WorkflowEdgeDto.FromEntity).ToList());
}

public record UpsertWorkflowRequest(string Name, string? Description, bool IsEnabled, List<WorkflowNodeDto> Nodes, List<WorkflowEdgeDto> Edges);

public record WorkflowRunDto(
    Guid Id, Guid WorkflowId, RunStatus Status, TriggerKind TriggerKind, string? TriggerNodeType,
    string? ErrorMessage, DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt)
{
    public static WorkflowRunDto FromEntity(WorkflowRun r) => new(
        r.Id, r.WorkflowId, r.Status, r.TriggerKind, r.TriggerNodeType, r.ErrorMessage, r.CreatedAt, r.StartedAt, r.CompletedAt);
}

public record NodeRunDto(
    Guid Id, Guid WorkflowNodeId, string NodeType, RunStatus Status,
    System.Text.Json.Nodes.JsonNode? Input, System.Text.Json.Nodes.JsonNode? Output,
    string? ErrorMessage, string? LogText, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt)
{
    public static NodeRunDto FromEntity(NodeRun n) => new(
        n.Id, n.WorkflowNodeId, n.NodeType, n.Status,
        TryParse(n.InputJson), TryParse(n.OutputJson),
        n.ErrorMessage, n.LogText, n.StartedAt, n.CompletedAt);

    private static System.Text.Json.Nodes.JsonNode? TryParse(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : System.Text.Json.Nodes.JsonNode.Parse(json);
}

public record WorkflowRunDetailDto(WorkflowRunDto Run, List<NodeRunDto> NodeRuns);
