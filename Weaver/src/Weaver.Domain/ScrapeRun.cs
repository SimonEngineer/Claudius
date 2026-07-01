namespace Weaver.Domain;

public class ScrapeRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScrapingProjectId { get; set; }
    public ScrapingProject? ScrapingProject { get; set; }

    public RunStatus Status { get; set; } = RunStatus.Pending;
    public TriggerKind TriggeredBy { get; set; } = TriggerKind.Manual;

    /// <summary>Set when a workflow node ("Scrape Action") kicked this run off.</summary>
    public Guid? WorkflowRunId { get; set; }

    public int PagesCrawled { get; set; }
    public int ItemsFound { get; set; }
    public int ItemsChanged { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public List<ScrapedItem> Items { get; set; } = new();
}

/// <summary>
/// One extracted item (a list row, or the single-item page result). ItemKey lets us match
/// the "same" item across runs so we can detect field changes (price drops, new listings, etc).
/// </summary>
public class ScrapedItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScrapeRunId { get; set; }
    public Guid ScrapingProjectId { get; set; }

    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>Stable identity derived from the field(s) marked IsKey, or a hash of all data if none marked.</summary>
    public string ItemKey { get; set; } = string.Empty;

    /// <summary>Extracted field values, keyed by FieldSelector.Name.</summary>
    public Dictionary<string, string?> Data { get; set; } = new();

    /// <summary>Hash of Data, used to cheaply detect whether this item changed since it was last seen.</summary>
    public string ContentHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
