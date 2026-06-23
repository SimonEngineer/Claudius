namespace Orchestrator.Domain.Engines;

public record EngineEvent(EventType Type, string PayloadJson, DateTimeOffset Timestamp)
{
    public static EngineEvent Token(string text) =>
        new(EventType.Token, System.Text.Json.JsonSerializer.Serialize(new { text }), DateTimeOffset.UtcNow);

    public static EngineEvent Log(string message) =>
        new(EventType.Log, System.Text.Json.JsonSerializer.Serialize(new { message }), DateTimeOffset.UtcNow);

    public static EngineEvent StatusChange(string status, string? detail = null) =>
        new(EventType.StatusChange, System.Text.Json.JsonSerializer.Serialize(new { status, detail }), DateTimeOffset.UtcNow);

    public static EngineEvent FileDiff(string path, string diff) =>
        new(EventType.FileDiff, System.Text.Json.JsonSerializer.Serialize(new { path, diff }), DateTimeOffset.UtcNow);

    public static EngineEvent ToolCall(string tool, string argsJson) =>
        new(EventType.ToolCall, System.Text.Json.JsonSerializer.Serialize(new { tool, argsJson }), DateTimeOffset.UtcNow);
}
