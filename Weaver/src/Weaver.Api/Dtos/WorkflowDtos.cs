using Weaver.Domain;

namespace Weaver.Api.Dtos;

public record WorkflowNodeDto(
    Guid Id, string Type, string Name, System.Text.Json.Nodes.JsonNode? Config,
    int MaxRetries, int RetryDelayMs, double PositionX, double PositionY)
{
    public static WorkflowNodeDto FromEntity(WorkflowNode n) => new(
        n.Id, n.Type, n.Name,
        string.IsNullOrWhiteSpace(n.ConfigJson) ? null : System.Text.Json.Nodes.JsonNode.Parse(n.ConfigJson),
        n.MaxRetries, n.RetryDelayMs, n.PositionX, n.PositionY);
}

public record WorkflowEdgeDto(Guid Id, Guid SourceNodeId, string? SourceHandle, Guid TargetNodeId, string? TargetHandle)
{
    public static WorkflowEdgeDto FromEntity(WorkflowEdge e) => new(e.Id, e.SourceNodeId, e.SourceHandle, e.TargetNodeId, e.TargetHandle);
}

public record WorkflowDto(Guid Id, string Name, string? Description, bool IsEnabled, DateTimeOffset UpdatedAt, List<WorkflowNodeDto> Nodes, List<WorkflowEdgeDto> Edges)
{
    public static WorkflowDto FromEntity(Workflow w) => new(
        w.Id, w.Name, w.Description, w.IsEnabled, w.UpdatedAt,
        w.Nodes.Select(WorkflowNodeDto.FromEntity).ToList(),
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
