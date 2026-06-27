using Orchestrator.Domain;
using Orchestrator.Infrastructure.Persistence;

namespace Orchestrator.Infrastructure.Scheduling;

public static class AuditLogger
{
    public static void Record(
        OrchestratorDbContext db,
        string action,
        Guid? projectId = null,
        Guid? taskId = null,
        string? actor = null,
        string? details = null)
    {
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            Action = action,
            ProjectId = projectId,
            TaskId = taskId,
            Actor = actor,
            Details = details
        });
    }
}
