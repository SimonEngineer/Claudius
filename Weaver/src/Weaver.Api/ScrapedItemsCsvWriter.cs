using System.Text;
using Weaver.Domain;

namespace Weaver.Api;

/// <summary>Renders scraped items as CSV: fixed metadata columns first, then one column per field
/// name (in the project's configured order), falling back to whatever keys actually show up in
/// the data if a project's fields changed after items were captured.</summary>
public static class ScrapedItemsCsvWriter
{
    public static string Write(List<string> fieldNames, List<ScrapedItem> items)
    {
        var columns = fieldNames.Count > 0
            ? fieldNames
            : items.SelectMany(i => i.Data.Keys).Distinct().ToList();

        var sb = new StringBuilder();
        sb.Append(string.Join(',', new[] { "SourceUrl", "ItemKey", "CreatedAt" }.Concat(columns).Select(Escape)));
        sb.Append('\n');

        foreach (var item in items)
        {
            var values = new List<string> { item.SourceUrl, item.ItemKey, item.CreatedAt.ToString("O") };
            values.AddRange(columns.Select(c => item.Data.TryGetValue(c, out var v) ? v ?? string.Empty : string.Empty));
            sb.Append(string.Join(',', values.Select(Escape)));
            sb.Append('\n');
        }

        return sb.ToString();
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
