using System.Text.Json.Nodes;

namespace Weaver.Workflows;

/// <summary>Tiny dot-path resolver ("input.price", "current.title") over JsonNode -- no array indexing, no JSONPath spec, just enough for template substitution and condition checks.</summary>
public static class JsonPathHelper
{
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
}
