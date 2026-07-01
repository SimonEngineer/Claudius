namespace Weaver.Domain;

public enum AuditAction
{
    Created,
    Updated,
    Deleted,
}

/// <summary>
/// A record of who did what to which resource -- ResourceName is a snapshot taken at the time of
/// the action, not a live lookup, so a "Deleted" entry can still say what was deleted after the
/// resource itself is gone.
/// </summary>
public class AuditLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; set; }

    public AuditAction Action { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
