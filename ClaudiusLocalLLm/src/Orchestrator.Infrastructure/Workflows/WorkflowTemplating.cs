using System.Text.Json;
using System.Text.RegularExpressions;

namespace Orchestrator.Infrastructure.Workflows;

/// <summary>Minimal `{{trigger.path.to.field}}` substitution for node config strings (email
/// subject/body, Discord message, log line, etc). Not a general templating engine on purpose --
/// the only data a node has is the payload that triggered the run.</summary>
public static partial class WorkflowTemplating
{
    [GeneratedRegex(@"\{\{\s*trigger(?:\.([\w.]+))?\s*\}\}")]
    private static partial Regex TriggerPlaceholder();

    public static string Render(string template, JsonElement triggerPayload) =>
        TriggerPlaceholder().Replace(template, match =>
        {
            var path = match.Groups[1].Value;
            if (string.IsNullOrEmpty(path))
            {
                return triggerPayload.ToString();
            }

            var current = triggerPayload;
            foreach (var segment in path.Split('.'))
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                {
                    return match.Value;
                }
            }

            return current.ValueKind == JsonValueKind.String ? current.GetString()! : current.ToString();
        });

    public static string? GetConfigString(JsonElement config, string property) =>
        config.ValueKind == JsonValueKind.Object && config.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
