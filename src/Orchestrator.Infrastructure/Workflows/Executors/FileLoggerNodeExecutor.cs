using System.Text.Json;

namespace Orchestrator.Infrastructure.Workflows.Executors;

/// <summary>Config: {"path": "...", "format": "csv"|"json"|"txt"} -- appends one line/record per
/// run to the given file, creating it (and its directory) on first write.</summary>
public class FileLoggerNodeExecutor : IWorkflowNodeExecutor
{
    public string Type => "action.fileLogger";

    public async Task<string> ExecuteAsync(WorkflowNodeContext context)
    {
        var path = WorkflowTemplating.GetConfigString(context.Config, "path")
            ?? throw new InvalidOperationException("fileLogger node is missing a 'path'.");
        var format = (WorkflowTemplating.GetConfigString(context.Config, "format") ?? "json").ToLowerInvariant();

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var line = format switch
        {
            "csv" => ToCsvLine(context.TriggerPayload),
            "txt" => $"{DateTimeOffset.UtcNow:O}\t{context.TriggerPayload}",
            _ => JsonSerializer.Serialize(new { timestamp = DateTimeOffset.UtcNow, payload = context.TriggerPayload }),
        };

        await File.AppendAllTextAsync(path, line + Environment.NewLine, context.CancellationToken);
        return $"Appended a {format} record to {path}.";
    }

    private static string ToCsvLine(JsonElement payload)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var payloadField = payload.ToString().Replace("\"", "\"\"");
        return $"\"{timestamp}\",\"{payloadField}\"";
    }
}
