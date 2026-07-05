using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Weaver.Workflows.Nodes;

/// <summary>
/// Config: { "field": "current.price", "operator": "lessThan", "value": "100" }.
/// Routes to the "true" or "false" outgoing edge based on comparing a field against value.
/// "field" may start with an earlier node's name (e.g. "Scrape Fixture.itemsChanged") to reach
/// back past the immediate predecessor; "value" is template-rendered first, so it can also
/// reference upstream data (e.g. "{{Get Threshold.maxPrice}}").
/// </summary>
public class ConditionNode : INodeHandler
{
    public string Type => "condition";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var field = context.Config?["field"]?.GetValue<string>();
        var op = context.Config?["operator"]?.GetValue<string>() ?? "equals";
        var compareValueTemplate = context.Config?["value"]?.GetValue<string>() ?? string.Empty;
        var compareValue = TemplateEngine.Render(compareValueTemplate, context.Input, context.AllNodeOutputs);

        if (string.IsNullOrWhiteSpace(field))
        {
            return Task.FromResult(NodeExecutionResult.Fail("Condition node is missing 'field' in config."));
        }

        var actual = JsonPathHelper.ResolveWithContext(context.Input, context.AllNodeOutputs, field);
        var actualString = JsonPathHelper.ResolveAsStringWithContext(context.Input, context.AllNodeOutputs, field);

        bool matched;
        try
        {
            matched = Evaluate(op, actual, actualString, compareValue);
        }
        catch (ArgumentException ex)
        {
            return Task.FromResult(NodeExecutionResult.Fail($"Condition node's regex is invalid: {ex.Message}"));
        }
        catch (RegexMatchTimeoutException)
        {
            return Task.FromResult(NodeExecutionResult.Fail("Condition node's regex took too long to evaluate (possible catastrophic backtracking)."));
        }

        context.Log($"condition: {field} {op} {compareValue} -> actual={actualString ?? "<null>"} -> {matched}");
        return Task.FromResult(NodeExecutionResult.Ok(context.Input, matched ? "true" : "false"));
    }

    // Internal so Filter Items can apply the exact same operator semantics per array entry.
    internal static bool Evaluate(string op, JsonNode? actual, string? actualString, string? compareValue)
    {
        switch (op)
        {
            case "exists":
                return actual is not null;
            case "notExists":
                return actual is null;
            case "contains":
                return actualString is not null && compareValue is not null && actualString.Contains(compareValue, StringComparison.OrdinalIgnoreCase);
            case "matchesRegex":
                return actualString is not null && compareValue is not null
                    && Regex.IsMatch(actualString, compareValue, RegexOptions.None, TimeSpan.FromMilliseconds(500));
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
