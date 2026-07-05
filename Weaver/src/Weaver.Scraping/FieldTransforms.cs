using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Weaver.Scraping;

/// <summary>One post-extraction transform step. Kind is one of: trim, lowercase, uppercase,
/// stripHtml, regexExtract (Pattern; first group if present, else whole match), parseNumber
/// (normalizes "1.234,56 kr" style values to an invariant decimal string).</summary>
public record FieldTransform(string Kind, string? Pattern = null);

public static class FieldTransforms
{
    private static readonly Regex HtmlTags = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex NonNumeric = new(@"[^0-9,.\-]", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static List<FieldTransform> Parse(string? transformsJson)
    {
        if (string.IsNullOrWhiteSpace(transformsJson))
        {
            return new List<FieldTransform>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<FieldTransform>>(transformsJson, Options) ?? new List<FieldTransform>();
        }
        catch (JsonException)
        {
            return new List<FieldTransform>();
        }
    }

    /// <summary>Applies the steps in order. A step that can't produce a value (regex with no match,
    /// unparseable number) yields null, and later steps pass null through.</summary>
    public static string? Apply(string? value, IReadOnlyList<FieldTransform> transforms)
    {
        foreach (var transform in transforms)
        {
            if (value is null)
            {
                return null;
            }

            value = transform.Kind switch
            {
                "trim" => value.Trim(),
                "lowercase" => value.ToLowerInvariant(),
                "uppercase" => value.ToUpperInvariant(),
                "stripHtml" => HtmlTags.Replace(value, string.Empty).Trim(),
                "regexExtract" => RegexExtract(value, transform.Pattern),
                "parseNumber" => ParseNumber(value),
                _ => value, // Unknown kinds pass through so an old client can't corrupt data.
            };
        }

        return value;
    }

    private static string? RegexExtract(string value, string? pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return value;
        }

        Match match;
        try
        {
            match = Regex.Match(value, pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        }
        catch (ArgumentException)
        {
            return value; // Invalid user pattern: pass through rather than nulling the field.
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }

        if (!match.Success)
        {
            return null;
        }

        return match.Groups.Count > 1 && match.Groups[1].Success ? match.Groups[1].Value : match.Value;
    }

    private static string? ParseNumber(string value)
    {
        var cleaned = NonNumeric.Replace(value, string.Empty);
        if (cleaned.Length == 0)
        {
            return null;
        }

        // Disambiguate thousand vs decimal separators: the LAST separator is the decimal one
        // when it's followed by 1-2 digits; everything else is grouping.
        var lastComma = cleaned.LastIndexOf(',');
        var lastDot = cleaned.LastIndexOf('.');
        var lastSep = Math.Max(lastComma, lastDot);
        if (lastSep >= 0)
        {
            var fractionDigits = cleaned.Length - lastSep - 1;
            var integerPart = new string(cleaned[..lastSep].Where(char.IsDigit).ToArray());
            var sign = cleaned.StartsWith('-') ? "-" : "";
            cleaned = fractionDigits is >= 1 and <= 2
                ? $"{sign}{integerPart}.{cleaned[(lastSep + 1)..]}"
                : $"{sign}{integerPart}{cleaned[(lastSep + 1)..]}";
        }

        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            ? number.ToString(CultureInfo.InvariantCulture)
            : null;
    }
}
