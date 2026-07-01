using System.Text.Json.Nodes;

namespace Weaver.Workflows.Nodes;

/// <summary>
/// Config: { "field": "current.price", "operator": "lessThan", "value": "100" }.
/// Routes to the "true" or "false" outgoing edge based on comparing Input's field against value.
/// </summary>
public class ConditionNode : INodeHandler
{
    public string Type => "condition";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var field = context.Config?["field"]?.GetValue<string>();
        var op = context.Config?["operator"]?.GetValue<string>() ?? "equals";
        var compareValue = context.Config?["value"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(field))
        {
            return Task.FromResult(NodeExecutionResult.Fail("Condition node is missing 'field' in config."));
        }

        var actual = JsonPathHelper.Resolve(context.Input, field);
        var actualString = JsonPathHelper.ResolveAsString(context.Input, field);
        var matched = Evaluate(op, actual, actualString, compareValue);

        context.Log($"condition: {field} {op} {compareValue} -> actual={actualString ?? "<null>"} -> {matched}");
        return Task.FromResult(NodeExecutionResult.Ok(context.Input, matched ? "true" : "false"));
    }

    private static bool Evaluate(string op, JsonNode? actual, string? actualString, string? compareValue)
    {
        switch (op)
        {
            case "exists":
                return actual is not null;
            case "notExists":
                return actual is null;
            case "contains":
                return actualString is not null && compareValue is not null && actualString.Contains(compareValue, StringComparison.OrdinalIgnoreCase);
            case "notEquals":
                return !string.Equals(actualString, compareValue, StringComparison.OrdinalIgnoreCase);
            case "greaterThan":
            case "lessThan":
            case "greaterThanOrEqual":
            case "lessThanOrEqual":
                if (!double.TryParse(actualString, out var a) || !double.TryParse(compareValue, out var b))
                {
                    return false;
                }
                return op switch
                {
                    "greaterThan" => a > b,
                    "lessThan" => a < b,
                    "greaterThanOrEqual" => a >= b,
                    "lessThanOrEqual" => a <= b,
                    _ => false
                };
            case "equals":
            default:
                return string.Equals(actualString, compareValue, StringComparison.OrdinalIgnoreCase);
        }
    }
}
