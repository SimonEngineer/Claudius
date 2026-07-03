using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Weaver.Workflows;

/// <summary>
/// Replaces {{secrets.NAME}} placeholders inside node config string values with the owner's stored
/// credential values at execution time -- so a workflow definition (and its export file) never
/// carries the secret itself, just a reference to it. Unknown names are left untouched so the node
/// fails visibly (e.g. a 401 from the target API) instead of silently substituting an empty string.
/// </summary>
public static class SecretsTemplate
{
    private static readonly Regex Token = new(@"\{\{\s*secrets\.([A-Za-z0-9_\-.]+)\s*\}\}", RegexOptions.Compiled);

    public static bool ContainsSecretTokens(string? configJson) =>
        !string.IsNullOrEmpty(configJson) && Token.IsMatch(configJson);

    /// <summary>Walks the config tree, replacing tokens inside every string value. Mutates in place
    /// where possible; returns the (possibly replaced) node for value nodes.</summary>
    public static JsonNode? Resolve(JsonNode? node, Func<string, string?> lookup)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(kv => kv.Key).ToList())
                {
                    var child = obj[key];
                    var resolved = Resolve(child, lookup);
                    if (!ReferenceEquals(resolved, child))
                    {
                        obj[key] = resolved;
                    }
                }
                return obj;

            case JsonArray arr:
                for (var i = 0; i < arr.Count; i++)
                {
                    var child = arr[i];
                    var resolved = Resolve(child, lookup);
                    if (!ReferenceEquals(resolved, child))
                    {
                        arr[i] = resolved;
                    }
                }
                return arr;

            case JsonValue value when value.TryGetValue<string>(out var s):
                var replaced = Token.Replace(s, m => lookup(m.Groups[1].Value) ?? m.Value);
                return replaced == s ? value : JsonValue.Create(replaced);

            default:
                return node;
        }
    }
}
