namespace Weaver.Domain;

/// <summary>
/// Durable audit record for an enqueued scrape request. The actual distributed delivery/leasing
/// across worker instances happens via a Redis stream + consumer group (see Weaver.Infrastructure);
/// this row is what the UI/API query for queue and run history, correlated by StreamMessageId.
/// </summary>
public class ScrapeJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScrapingProjectId { get; set; }

    public ScrapeJobStatus Status { get; set; } = ScrapeJobStatus.Queued;
    public TriggerKind TriggeredBy { get; set; } = TriggerKind.Manual;
    public Guid? WorkflowRunId { get; set; }

    /// <summary>Redis stream entry id, once enqueued.</summary>
    public string? StreamMessageId { get; set; }

    /// <summary>Which worker instance currently holds the lease, if any.</summary>
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }

    public int Attempts { get; set; }
    public Guid? ScrapeRunId { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}
