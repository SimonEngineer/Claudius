using System.Text.Json;

namespace Orchestrator.Infrastructure.Workflows;

public class WorkflowNodeDefinition
{
    public required string Id { get; set; }

    /// <summary>e.g. "trigger.event", "trigger.cron", "trigger.http", "action.sendEmail",
    /// "action.sendDiscordMessage", "action.codeBlock", "action.fileLogger".</summary>
    public required string Type { get; set; }

    public JsonElement Config { get; set; }

    /// <summary>Canvas coordinates; the engine never reads this, it's purely for the GUI builder
    /// to remember layout across edits.</summary>
    public double X { get; set; }
    public double Y { get; set; }
}

public class WorkflowEdgeDefinition
{
    public required string From { get; set; }
    public required string To { get; set; }
}

public class WorkflowDefinition
{
    public List<WorkflowNodeDefinition> Nodes { get; set; } = [];
    public List<WorkflowEdgeDefinition> Edges { get; set; } = [];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static WorkflowDefinition Parse(string definitionJson) =>
        JsonSerializer.Deserialize<WorkflowDefinition>(definitionJson, JsonOptions)
        ?? new WorkflowDefinition();

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public IEnumerable<WorkflowNodeDefinition> TriggerNodesOfType(string triggerType) =>
        Nodes.Where(n => n.Type == triggerType);

    public IEnumerable<WorkflowNodeDefinition> NodesDownstreamOf(string nodeId) =>
        Edges.Where(e => e.From == nodeId)
            .Select(e => Nodes.FirstOrDefault(n => n.Id == e.To))
            .Where(n => n is not null)!;
}
