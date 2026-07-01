using System.Diagnostics;
using System.Text.Json.Nodes;
using Weaver.Workflows;
using Weaver.Workflows.Nodes;
using Xunit;

namespace Weaver.Tests;

public class DelayAndSplitIntoBatchesNodeTests
{
    private static NodeExecutionContext Context(JsonNode? config, JsonNode? input) => new()
    {
        WorkflowRunId = Guid.NewGuid(),
        WorkflowNodeId = Guid.NewGuid(),
        NodeType = "test",
        Config = config,
        Input = input,
        AllNodeOutputs = JsonPathHelper.EmptyContext,
        Services = new DummyServiceProvider(),
        Log = _ => { },
    };

    [Fact]
    public async Task DelayNode_WaitsApproximatelyTheConfiguredDuration()
    {
        var config = new JsonObject { ["seconds"] = 0.2 };
        var input = JsonNode.Parse("""{"a":1}""");
        var node = new DelayNode();

        var sw = Stopwatch.StartNew();
        var result = await node.ExecuteAsync(Context(config, input));
        sw.Stop();

        Assert.True(result.Success);
        Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(180), $"Only waited {sw.ElapsedMilliseconds}ms");
        Assert.Equal(1, result.Output!["a"]!.GetValue<int>());
    }

    [Fact]
    public async Task DelayNode_NegativeSeconds_Fails()
    {
        // Built via JsonNode.Parse (not a direct JsonObject initializer) to match how the engine
        // actually constructs NodeExecutionContext.Config from a stored ConfigJson string --
        // JsonValue nodes backed by a parsed JsonElement support numeric GetValue<T> widening
        // (int literal -> double) that a directly-constructed JsonValue<int> does not.
        var node = new DelayNode();
        var result = await node.ExecuteAsync(Context(JsonNode.Parse("""{"seconds": -1}"""), null));
        Assert.False(result.Success);
    }

    [Fact]
    public async Task DelayNode_WholeNumberSecondsFromParsedJson_Works()
    {
        // Reproduces the exact shape of a real stored config: a whole-number seconds value.
        var node = new DelayNode();
        var result = await node.ExecuteAsync(Context(JsonNode.Parse("""{"seconds": 0}"""), JsonNode.Parse("""{"a":1}""")));
        Assert.True(result.Success);
    }

    [Fact]
    public async Task SplitIntoBatches_ChunksWholeInputArray()
    {
        var input = JsonNode.Parse("[1,2,3,4,5]");
        var node = new SplitIntoBatchesNode();

        var result = await node.ExecuteAsync(Context(new JsonObject { ["batchSize"] = 2 }, input));

        Assert.True(result.Success);
        var batches = result.Output!.AsArray();
        Assert.Equal(3, batches.Count);
        Assert.Equal(2, batches[0]!.AsArray().Count);
        Assert.Equal(2, batches[1]!.AsArray().Count);
        Assert.Single(batches[2]!.AsArray());
    }

    [Fact]
    public async Task SplitIntoBatches_UsesArrayPathWhenGiven()
    {
        var input = JsonNode.Parse("""{"items":[10,20,30]}""");
        var node = new SplitIntoBatchesNode();

        var result = await node.ExecuteAsync(Context(new JsonObject { ["arrayPath"] = "items", ["batchSize"] = 10 }, input));

        Assert.True(result.Success);
        var batches = result.Output!.AsArray();
        Assert.Single(batches);
        Assert.Equal(3, batches[0]!.AsArray().Count);
    }

    [Fact]
    public async Task SplitIntoBatches_NonArrayInput_Fails()
    {
        var input = JsonNode.Parse("""{"not":"an array"}""");
        var node = new SplitIntoBatchesNode();

        var result = await node.ExecuteAsync(Context(new JsonObject { ["batchSize"] = 2 }, input));

        Assert.False(result.Success);
    }

    [Fact]
    public async Task SplitIntoBatches_InvalidBatchSize_Fails()
    {
        var node = new SplitIntoBatchesNode();
        var result = await node.ExecuteAsync(Context(new JsonObject { ["batchSize"] = 0 }, JsonNode.Parse("[1,2]")));
        Assert.False(result.Success);
    }

    private class DummyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
