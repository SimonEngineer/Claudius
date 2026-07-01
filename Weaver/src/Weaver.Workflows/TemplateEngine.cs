using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Weaver.Workflows;

/// <summary>
/// Mustache-lite: replaces {{dot.path}} tokens with values resolved against the node's input (or,
/// when the first path segment names an earlier node, that node's output -- see JsonPathHelper).
/// </summary>
public static partial class TemplateEngine
{
    // Deliberately permissive (anything but braces) rather than \w+ so a node name containing
    // spaces -- the common case, e.g. "Scrape Fixture.items" -- works as the first path segment.
    [GeneratedRegex(@"\{\{\s*([^{}]+?)\s*\}\}")]
    private static partial Regex TokenPattern();

    public static string Render(string template, JsonNode? data) => Render(template, data, JsonPathHelper.EmptyContext);

    public static string Render(string template, JsonNode? data, IReadOnlyDictionary<string, JsonNode?> allNodeOutputs)
    {
        return TokenPattern().Replace(template, match =>
        {
            var path = match.Groups[1].Value;
            return JsonPathHelper.ResolveAsStringWithContext(data, allNodeOutputs, path) ?? string.Empty;
        });
    }
}
