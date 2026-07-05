using System.Text.Json.Nodes;
using Weaver.Workflows;
using Weaver.Workflows.Nodes;
using Xunit;

namespace Weaver.Tests;

/// <summary>Direct handler tests for the pure data nodes (filter/setFields/aggregate) -- they need
/// no services, so a hand-built context is enough.</summary>
public class DataNodesTests
{
    private static NodeExecutionContext Context(string configJson, string inputJson) => new()
    {
        WorkflowRunId = Guid.NewGuid(),
        WorkflowNodeId = Guid.NewGuid(),
        NodeType = "test",
        Config = JsonNode.Parse(configJson),
        Input = JsonNode.Parse(inputJson),
        AllNodeOutputs = new Dictionary<string, JsonNode?>(StringComparer.OrdinalIgnoreCase),
        Services = null!,
        Log = _ => { },
        CancellationToken = CancellationToken.None,
    };

    [Fact]
    public async Task FilterItems_KeepsMatchingEntries()
    {
        var context = Context(
            """{"arrayPath": "", "field": "price", "operator": "lessThan", "value": "100"}""",
            """[{"name":"a","price":"50"},{"name":"b","price":"150"},{"name":"c","price":"99"}]""");

        var result = await new FilterItemsNode().ExecuteAsync(context);

        Assert.True(result.Success);
        var array = Assert.IsType<JsonArray>(result.Output);
        Assert.Equal(2, array.Count);
        Assert.Equal("a", array[0]?["name"]?.GetValue<string>());
        Assert.Equal("c", array[1]?["name"]?.GetValue<string>());
    }

    [Fact]
    public async Task FilterItems_NonArrayInput_Fails()
    {
        var result = await new FilterItemsNode().ExecuteAsync(Context("""{"field":"x","operator":"equals","value":"1"}""", """{"not":"array"}"""));
        Assert.False(result.Success);
    }

    [Fact]
    public async Task SetFields_RendersTemplates()
    {
        var context = Context(
            """{"mappings": {"headline": "Price is {{price}}", "fixed": "constant"}}""",
            """{"price": "42"}""");

        var result = await new SetFieldsNode().ExecuteAsync(context);

        Assert.True(result.Success);
        Assert.Equal("Price is 42", result.Output?["headline"]?.GetValue<string>());
        Assert.Equal("constant", result.Output?["fixed"]?.GetValue<string>());
    }

    [Fact]
    public async Task SetFields_NoMappings_Fails()
    {
        var result = await new SetFieldsNode().ExecuteAsync(Context("""{"mappings": {}}""", "{}"));
        Assert.False(result.Success);
    }

    [Theory]
    [InlineData("count", 3)]
    [InlineData("sum", 60)]
    [InlineData("avg", 20)]
    [InlineData("min", 10)]
    [InlineData("max", 30)]
    public async Task Aggregate_ComputesOperations(string operation, double expected)
    {
        var context = Context(
            $$"""{"arrayPath": "", "field": "n", "operation": "{{operation}}"}""",
            """[{"n":"10"},{"n":"20"},{"n":"30"}]""");

        var result = await new AggregateNode().ExecuteAsync(context);

        Assert.True(result.Success);
        Assert.Equal(expected, result.Output?["result"]?.GetValue<double>());
        Assert.Equal(3, result.Output?["count"]?.GetValue<int>());
    }

    [Fact]
    public async Task Aggregate_SkipsNonNumericValues()
    {
        var context = Context(
            """{"field": "n", "operation": "sum"}""",
            """[{"n":"10"},{"n":"n/a"},{"n":"5"}]""");

        var result = await new AggregateNode().ExecuteAsync(context);
        Assert.True(result.Success);
        Assert.Equal(15, result.Output?["result"]?.GetValue<double>());
    }

    [Fact]
    public async Task Aggregate_UnknownOperation_Fails()
    {
        var result = await new AggregateNode().ExecuteAsync(Context("""{"operation": "median"}""", "[]"));
        Assert.False(result.Success);
    }
}
