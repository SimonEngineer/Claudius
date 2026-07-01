using System.Text.Json.Nodes;
using Weaver.Workflows;
using Weaver.Workflows.Nodes;
using Xunit;

namespace Weaver.Tests;

public class ConditionNodeTests
{
    private static NodeExecutionContext Context(JsonNode? config, JsonNode? input) => new()
    {
        WorkflowRunId = Guid.NewGuid(),
        WorkflowNodeId = Guid.NewGuid(),
        NodeType = "condition",
        Config = config,
        Input = input,
        AllNodeOutputs = JsonPathHelper.EmptyContext,
        Services = new DummyServiceProvider(),
        Log = _ => { },
    };

    private static JsonNode Config(string field, string op, string value) => new JsonObject
    {
        ["field"] = field,
        ["operator"] = op,
        ["value"] = value,
    };

    [Theory]
    [InlineData("50", "lessThan", "100", true)]
    [InlineData("150", "lessThan", "100", false)]
    [InlineData("150", "greaterThan", "100", true)]
    [InlineData("100", "greaterThanOrEqual", "100", true)]
    [InlineData("99", "greaterThanOrEqual", "100", false)]
    public async Task NumericComparisons_EvaluateCorrectly(string actual, string op, string threshold, bool expectedTrueBranch)
    {
        var input = JsonNode.Parse($$"""{"price":{{actual}}}""");
        var node = new ConditionNode();

        var result = await node.ExecuteAsync(Context(Config("price", op, threshold), input));

        Assert.True(result.Success);
        Assert.Equal(expectedTrueBranch ? "true" : "false", result.Branch);
    }

    [Fact]
    public async Task Equals_IsCaseInsensitive()
    {
        var input = JsonNode.Parse("""{"status":"InStock"}""");
        var node = new ConditionNode();

        var result = await node.ExecuteAsync(Context(Config("status", "equals", "instock"), input));

        Assert.Equal("true", result.Branch);
    }

    [Fact]
    public async Task Contains_MatchesSubstring()
    {
        var input = JsonNode.Parse("""{"title":"Wireless Mouse Pro"}""");
        var node = new ConditionNode();

        var result = await node.ExecuteAsync(Context(Config("title", "contains", "mouse"), input));

        Assert.Equal("true", result.Branch);
    }

    [Fact]
    public async Task MatchesRegex_ValidPattern_Matches()
    {
        var input = JsonNode.Parse("""{"sku":"ABC-1234"}""");
        var node = new ConditionNode();

        var result = await node.ExecuteAsync(Context(Config("sku", "matchesRegex", "^ABC-\\d+$"), input));

        Assert.True(result.Success);
        Assert.Equal("true", result.Branch);
    }

    [Fact]
    public async Task MatchesRegex_InvalidPattern_FailsGracefully()
    {
        var input = JsonNode.Parse("""{"sku":"ABC-1234"}""");
        var node = new ConditionNode();

        var result = await node.ExecuteAsync(Context(Config("sku", "matchesRegex", "(unclosed"), input));

        Assert.False(result.Success);
        Assert.Contains("regex", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Exists_And_NotExists()
    {
        var input = JsonNode.Parse("""{"a":1}""");
        var node = new ConditionNode();

        var exists = await node.ExecuteAsync(Context(Config("a", "exists", ""), input));
        var notExists = await node.ExecuteAsync(Context(Config("b", "notExists", ""), input));

        Assert.Equal("true", exists.Branch);
        Assert.Equal("true", notExists.Branch);
    }

    [Fact]
    public async Task MissingField_FailsWithClearError()
    {
        var config = JsonNode.Parse("""{"operator":"equals","value":"x"}""");
        var node = new ConditionNode();

        var result = await node.ExecuteAsync(Context(config, JsonNode.Parse("{}")));

        Assert.False(result.Success);
        Assert.Contains("field", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private class DummyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
