using System.Text.Json.Nodes;

namespace Weaver.Workflows;

/// <summary>Tiny dot-path resolver ("input.price", "current.title") over JsonNode -- no array indexing, no JSONPath spec, just enough for template substitution and condition checks.</summary>
public static class JsonPathHelper
{
    public static readonly IReadOnlyDictionary<string, JsonNode?> EmptyContext =
        new Dictionary<string, JsonNode?>(StringComparer.OrdinalIgnoreCase);

    public static JsonNode? Resolve(JsonNode? root, string path)
    {
        if (root is null || string.IsNullOrWhiteSpace(path))
        {
            return root;
        }

        JsonNode? current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current is JsonObject obj && obj.TryGetPropertyValue(segment, out var next))
            {
                current = next;
            }
            else
            {
                return null;
            }
        }

        return current;
    }

    public static string? ResolveAsString(JsonNode? root, string path)
    {
        var value = Resolve(root, path);
        return value switch
        {
            null => null,
            JsonValue v => v.ToString(),
            _ => value.ToJsonString()
        };
    }

    /// <summary>
    /// Like <see cref="Resolve"/>, but if the path's first segment names a node in
    /// <paramref name="allNodeOutputs"/>, resolves the remainder against that node's output
    /// instead of <paramref name="input"/> -- e.g. "Scrape Fixture.items" reaches back to that
    /// node's output regardless of how many hops downstream this reference is written.
    /// </summary>
    public static JsonNode? ResolveWithContext(JsonNode? input, IReadOnlyDictionary<string, JsonNode?> allNodeOutputs, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return input;
        }

        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 0 && allNodeOutputs.TryGetValue(segments[0], out var nodeOutput))
        {
            return Resolve(nodeOutput, string.Join('.', segments.Skip(1)));
        }

        return Resolve(input, path);
    }

    public static string? ResolveAsStringWithContext(JsonNode? input, IReadOnlyDictionary<string, JsonNode?> allNodeOutputs, string path)
    {
        var value = ResolveWithContext(input, allNodeOutputs, path);
        return value switch
        {
            null => null,
            JsonValue v => v.ToString(),
            _ => value.ToJsonString()
        };
    }
}
