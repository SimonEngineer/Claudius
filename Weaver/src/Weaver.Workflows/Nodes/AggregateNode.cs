using System.Globalization;
using System.Text.Json.Nodes;

namespace Weaver.Workflows.Nodes;

/// <summary>
/// Config: { "arrayPath": "items", "field": "price", "operation": "avg" }. Computes
/// count/sum/avg/min/max over the numeric values of a field across an array. Non-numeric or
/// missing values are skipped (count counts array entries regardless). Output:
/// { "operation", "field", "count", "result" }.
/// </summary>
public class AggregateNode : INodeHandler
{
    public string Type => "action.aggregate";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var arrayPath = context.Config?["arrayPath"]?.GetValue<string>() ?? string.Empty;
        var field = context.Config?["field"]?.GetValue<string>() ?? string.Empty;
        var operation = (context.Config?["operation"]?.GetValue<string>() ?? "count").ToLowerInvariant();

        var source = string.IsNullOrWhiteSpace(arrayPath)
            ? context.Input
            : JsonPathHelper.ResolveWithContext(context.Input, context.AllNodeOutputs, arrayPath);

        if (source is not JsonArray array)
        {
            return Task.FromResult(NodeExecutionResult.Fail(
                string.IsNullOrWhiteSpace(arrayPath)
                    ? "Aggregate node's input is not a JSON array."
                    : $"Aggregate node's 'arrayPath' ({arrayPath}) did not resolve to a JSON array."));
        }

        var numbers = new List<double>();
        foreach (var entry in array)
        {
            var valueNode = string.IsNullOrWhiteSpace(field) ? entry : JsonPathHelper.Resolve(entry, field);
            var text = valueNode is JsonValue v ? v.ToString() : null;
            if (text is not null && double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var number))
            {
                numbers.Add(number);
            }
        }

        double? result = operation switch
        {
            "count" => array.Count,
            "sum" => numbers.Count > 0 ? numbers.Sum() : 0,
            "avg" => numbers.Count > 0 ? numbers.Average() : null,
            "min" => numbers.Count > 0 ? numbers.Min() : null,
            "max" => numbers.Count > 0 ? numbers.Max() : null,
            _ => null,
        };

        if (result is null && operation is not ("avg" or "min" or "max"))
        {
            return Task.FromResult(NodeExecutionResult.Fail($"Aggregate node's 'operation' must be count, sum, avg, min or max (got '{operation}')."));
        }

        var output = new JsonObject
        {
            ["operation"] = operation,
            ["field"] = field,
            ["count"] = array.Count,
            ["result"] = result,
        };

        context.Log($"aggregate: {operation}({field}) over {array.Count} item(s) = {result?.ToString(CultureInfo.InvariantCulture) ?? "null"}");
        return Task.FromResult(NodeExecutionResult.Ok(output));
    }
}
