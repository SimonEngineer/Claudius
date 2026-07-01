using Weaver.Domain;
using Weaver.Infrastructure.Persistence;

namespace Weaver.Infrastructure.Auditing;

/// <summary>
/// Records a create/update/delete against the same WeaverDbContext the calling controller already
/// has open, so the audit entry lands in the same SaveChangesAsync call as the change it's
/// describing -- there's no separate transaction to fail out of step with the actual write.
/// </summary>
public interface IAuditLogger
{
    void Record(Guid userId, AuditAction action, string resourceType, Guid resourceId, string resourceName);
}

public class AuditLogger : IAuditLogger
{
    private readonly WeaverDbContext _db;

    public AuditLogger(WeaverDbContext db)
    {
        _db = db;
    }

    public void Record(Guid userId, AuditAction action, string resourceType, Guid resourceId, string resourceName)
    {
        _db.AuditLogEntries.Add(new AuditLogEntry
        {
            OwnerUserId = userId,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            ResourceName = resourceName,
        });
    }
}
