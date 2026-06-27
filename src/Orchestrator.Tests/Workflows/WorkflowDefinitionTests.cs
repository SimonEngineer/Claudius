using Orchestrator.Infrastructure.Workflows;
using Xunit;

namespace Orchestrator.Tests.Workflows;

public class WorkflowDefinitionTests
{
    private const string Json =
        """
        {
          "nodes": [
            { "id": "n1", "type": "trigger.event", "config": { "eventName": "task.done" }, "x": 0, "y": 0 },
            { "id": "n2", "type": "action.fileLogger", "config": { "path": "/tmp/x.log" }, "x": 100, "y": 0 },
            { "id": "n3", "type": "action.sendEmail", "config": { "to": "a@b.com" }, "x": 200, "y": 0 }
          ],
          "edges": [
            { "from": "n1", "to": "n2" },
            { "from": "n2", "to": "n3" }
          ]
        }
        """;

    [Fact]
    public void Parse_ReadsNodesAndEdges()
    {
        var definition = WorkflowDefinition.Parse(Json);

        Assert.Equal(3, definition.Nodes.Count);
        Assert.Equal(2, definition.Edges.Count);
    }

    [Fact]
    public void TriggerNodesOfType_FiltersByType()
    {
        var definition = WorkflowDefinition.Parse(Json);

        var triggers = definition.TriggerNodesOfType("trigger.event").ToList();

        Assert.Single(triggers);
        Assert.Equal("n1", triggers[0].Id);
    }

    [Fact]
    public void NodesDownstreamOf_FollowsEdges()
    {
        var definition = WorkflowDefinition.Parse(Json);

        var downstream = definition.NodesDownstreamOf("n1").Select(n => n.Id).ToList();

        Assert.Equal(["n2"], downstream);
    }

    [Fact]
    public void ToJson_RoundTrips()
    {
        var definition = WorkflowDefinition.Parse(Json);

        var reparsed = WorkflowDefinition.Parse(definition.ToJson());

        Assert.Equal(3, reparsed.Nodes.Count);
        Assert.Equal(2, reparsed.Edges.Count);
    }
}
