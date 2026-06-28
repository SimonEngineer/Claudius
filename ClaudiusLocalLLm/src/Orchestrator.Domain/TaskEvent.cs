namespace Orchestrator.Domain;

/// <summary>A single streamed event (token chunk, tool call, diff, log line, status change) from a Run.</summary>
public class TaskEvent
{
    public long Id { get; set; }
    public Guid RunId { get; set; }
    public Run? Run { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public EventType Type { get; set; }
    public required string PayloadJson { get; set; }
}
