using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Weaver.Workflows.Nodes;

/// <summary>
/// Config: { "arrayPath": "items", "field": "price", "operator": "lessThan", "value": "100" }.
/// Keeps the entries of an array whose field matches the condition (same operators as the
/// Condition node); outputs the filtered array. Blank arrayPath means the node's whole input.
/// </summary>
public class FilterItemsNode : INodeHandler
{
    public string Type => "action.filterItems";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var arrayPath = context.Config?["arrayPath"]?.GetValue<string>() ?? string.Empty;
        var field = context.Config?["field"]?.GetValue<string>() ?? string.Empty;
        var op = context.Config?["operator"]?.GetValue<string>() ?? "equals";
        var compareValue = context.Config?["value"]?.GetValue<string>();

        var source = string.IsNullOrWhiteSpace(arrayPath)
            ? context.Input
            : JsonPathHelper.ResolveWithContext(context.Input, context.AllNodeOutputs, arrayPath);

        if (source is not JsonArray array)
        {
            return Task.FromResult(NodeExecutionResult.Fail(
                string.IsNullOrWhiteSpace(arrayPath)
                    ? "Filter Items node's input is not a JSON array."
                    : $"Filter Items node's 'arrayPath' ({arrayPath}) did not resolve to a JSON array."));
        }

        var kept = new JsonArray();
        try
        {
            foreach (var entry in array)
            {
                var actual = string.IsNullOrWhiteSpace(field) ? entry : JsonPathHelper.Resolve(entry, field);
                var actualString = actual switch
                {
                    JsonValue value => value.ToString(),
                    null => null,
                    _ => actual.ToJsonString(),
                };
                if (ConditionNode.Evaluate(op, actual, actualString, compareValue))
                {
                    kept.Add(entry?.DeepClone());
                }
            }
        }
        catch (ArgumentException ex)
        {
            return Task.FromResult(NodeExecutionResult.Fail($"Filter Items node's regex is invalid: {ex.Message}"));
        }
        catch (RegexMatchTimeoutException)
        {
            return Task.FromResult(NodeExecutionResult.Fail("Filter Items node's regex took too long to evaluate."));
        }

        context.Log($"filterItems: {array.Count} -> {kept.Count} item(s) where {field} {op} {compareValue}");
        return Task.FromResult(NodeExecutionResult.Ok(kept));
    }
}
