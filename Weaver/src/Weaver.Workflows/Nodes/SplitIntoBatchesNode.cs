using System.Text.Json.Nodes;

namespace Weaver.Workflows.Nodes;

/// <summary>
/// Config: { "arrayPath": "items", "batchSize": 10 }. Resolves an array (the node's whole input if
/// arrayPath is blank, otherwise a dot-path into it -- same resolver Condition/Template use) and
/// chunks it into fixed-size batches, output as an array of arrays. There's no loop/iterate node in
/// this engine, so a downstream Code node is how a workflow actually walks each batch; this node's
/// job is just the chunking.
/// </summary>
public class SplitIntoBatchesNode : INodeHandler
{
    public string Type => "action.splitIntoBatches";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var arrayPath = context.Config?["arrayPath"]?.GetValue<string>() ?? string.Empty;
        var batchSize = context.Config?["batchSize"]?.GetValue<int>() ?? 10;

        if (batchSize < 1)
        {
            return Task.FromResult(NodeExecutionResult.Fail("Split into Batches node's 'batchSize' must be at least 1."));
        }

        var source = string.IsNullOrWhiteSpace(arrayPath)
            ? context.Input
            : JsonPathHelper.ResolveWithContext(context.Input, context.AllNodeOutputs, arrayPath);

        if (source is not JsonArray array)
        {
            return Task.FromResult(NodeExecutionResult.Fail(
                string.IsNullOrWhiteSpace(arrayPath)
                    ? "Split into Batches node's input is not a JSON array."
                    : $"Split into Batches node's 'arrayPath' ({arrayPath}) did not resolve to a JSON array."));
        }

        var batches = new JsonArray();
        for (var i = 0; i < array.Count; i += batchSize)
        {
            var batch = new JsonArray();
            foreach (var item in array.Skip(i).Take(batchSize))
            {
                batch.Add(item?.DeepClone());
            }
            batches.Add(batch);
        }

        context.Log($"splitIntoBatches: {array.Count} item(s) -> {batches.Count} batch(es) of up to {batchSize}");
        return Task.FromResult(NodeExecutionResult.Ok(batches));
    }
}
