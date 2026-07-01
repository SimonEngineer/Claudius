using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Weaver.Workflows;

/// <summary>Mustache-lite: replaces {{dot.path}} tokens with values resolved against the node's input via JsonPathHelper.</summary>
public static partial class TemplateEngine
{
    [GeneratedRegex(@"\{\{\s*([\w\.]+)\s*\}\}")]
    private static partial Regex TokenPattern();

    public static string Render(string template, JsonNode? data)
    {
        return TokenPattern().Replace(template, match =>
        {
            var path = match.Groups[1].Value;
            return JsonPathHelper.ResolveAsString(data, path) ?? string.Empty;
        });
    }
}
