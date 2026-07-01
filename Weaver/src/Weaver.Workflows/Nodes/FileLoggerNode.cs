using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CsvHelper;

namespace Weaver.Workflows.Nodes;

/// <summary>
/// Config: { "filePath": "/data/log.csv", "format": "csv" | "json" | "txt" }. Always appends.
/// "json" writes newline-delimited JSON (one object per line) so appending never requires
/// rewriting the whole file; "csv" writes a header the first time the file is created.
/// </summary>
public class FileLoggerNode : INodeHandler
{
    public string Type => "action.fileLogger";
    private static readonly SemaphoreSlim FileLock = new(1, 1);

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var filePath = context.Config?["filePath"]?.GetValue<string>();
        var format = context.Config?["format"]?.GetValue<string>()?.ToLowerInvariant() ?? "json";

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return NodeExecutionResult.Fail("File Logger node is missing 'filePath' in config.");
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await FileLock.WaitAsync(context.CancellationToken);
        try
        {
            switch (format)
            {
                case "csv":
                    await AppendCsvAsync(filePath, context.Input);
                    break;
                case "txt":
                    await File.AppendAllTextAsync(filePath, $"[{DateTimeOffset.UtcNow:O}] {context.Input?.ToJsonString()}\n", context.CancellationToken);
                    break;
                case "json":
                default:
                    await File.AppendAllTextAsync(filePath, context.Input?.ToJsonString() + "\n", context.CancellationToken);
                    break;
            }
        }
        finally
        {
            FileLock.Release();
        }

        context.Log($"fileLogger: appended to {filePath} ({format})");
        return NodeExecutionResult.Ok(context.Input);
    }

    private static async Task AppendCsvAsync(string filePath, JsonNode? input)
    {
        var rows = input switch
        {
            JsonArray array => array.OfType<JsonObject>().ToList(),
            JsonObject obj => [obj],
            _ => []
        };

        if (rows.Count == 0)
        {
            return;
        }

        var writeHeader = !File.Exists(filePath);
        var columns = rows[0].Select(kv => kv.Key).ToList();

        await using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.None);
        await using var writer = new StreamWriter(stream);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        if (writeHeader)
        {
            foreach (var column in columns)
            {
                csv.WriteField(column);
            }
            await csv.NextRecordAsync();
        }

        foreach (var row in rows)
        {
            foreach (var column in columns)
            {
                csv.WriteField(row[column]?.ToString() ?? string.Empty);
            }
            await csv.NextRecordAsync();
        }
    }
}
