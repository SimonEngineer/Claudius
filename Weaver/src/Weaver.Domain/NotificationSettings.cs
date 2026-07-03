namespace Weaver.Domain;

/// <summary>
/// Per-user failure notification preferences. Email goes to the account's address via the
/// configured SMTP settings; the webhook (if set) receives a JSON POST. Both default off.
/// </summary>
public class NotificationSettings
{
    /// <summary>PK and FK to the owning user (one row per user, created lazily).</summary>
    public Guid UserId { get; set; }

    public bool NotifyOnScrapeFailure { get; set; }
    public bool NotifyOnWorkflowFailure { get; set; }

    /// <summary>Send failure notifications to the account's email address.</summary>
    public bool EmailEnabled { get; set; }

    /// <summary>Optional webhook that receives a JSON POST on failure. Encrypted at rest (it may
    /// embed a token, like a Slack incoming-webhook URL).</summary>
    public string? WebhookUrl { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
