using System.Text.Json;

namespace Weaver.Scraping;

public static class CustomHeadersParser
{
    /// <summary>Parses a project's CustomHeadersJson into a case-insensitive name/value map (header names are conventionally case-insensitive, and so is the special "Cookie" name the fetchers treat specially).</summary>
    public static Dictionary<string, string> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return new Dictionary<string, string>(parsed ?? new(), StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
