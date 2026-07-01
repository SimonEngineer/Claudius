using System.Globalization;
using System.Text;
using Weaver.Domain;

namespace Weaver.Api;

public static class ScrapeRunsCsvWriter
{
    private static readonly string[] Columns =
    {
        "Status", "TriggeredBy", "PagesCrawled", "ItemsFound", "ItemsChanged", "ErrorMessage", "CreatedAt", "StartedAt", "CompletedAt",
    };

    public static string Write(List<ScrapeRun> runs)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(',', Columns.Select(Escape)));
        sb.Append('\n');

        foreach (var run in runs)
        {
            var values = new[]
            {
                run.Status.ToString(),
                run.TriggeredBy.ToString(),
                run.PagesCrawled.ToString(CultureInfo.InvariantCulture),
                run.ItemsFound.ToString(CultureInfo.InvariantCulture),
                run.ItemsChanged.ToString(CultureInfo.InvariantCulture),
                run.ErrorMessage ?? string.Empty,
                run.CreatedAt.ToString("O"),
                run.StartedAt?.ToString("O") ?? string.Empty,
                run.CompletedAt?.ToString("O") ?? string.Empty,
            };
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
