using Microsoft.EntityFrameworkCore;
using Orchestrator.Domain;
using Orchestrator.Domain.Streaming;
using Orchestrator.Infrastructure.Persistence;

namespace Orchestrator.Infrastructure.Scheduling;

/// <summary>
/// User-initiated cancellation, shared by TasksController so a dashboard cancel behaves like an
/// engine-driven dead-letter: it stops the in-flight process, cascades to any still-active children
/// of a decomposed task, and propagates up to a decomposed parent the same way
/// TaskRunnerJob.PropagateToParentAsync does for engine-driven failures.
/// </summary>
public class TaskLifecycleService(
    OrchestratorDbContext db,
    IRunCancellationRegistry cancellationRegistry,
    IEventBroadcaster broadcaster)
{
    private static readonly TaskState[] TerminalStates = [TaskState.Done, TaskState.Failed, TaskState.DeadLetter];

    /// <summary>Returns null if the task doesn't exist, false if it's already terminal, true on success.</summary>
    public async Task<bool?> CancelAsync(Guid taskId, CancellationToken ct)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task is null)
        {
            return null;
        }

        if (TerminalStates.Contains(task.State))
        {
            return false;
        }

        var touched = new List<AgentTask> { task };

        if (task.State == TaskState.Decomposed)
        {
            var children = await db.Tasks
                .Where(t => t.ParentTaskId == taskId && !TerminalStates.Contains(t.State))
                .ToListAsync(ct);
            foreach (var child in children)
            {
                cancellationRegistry.TryCancel(child.Id);
                child.State = TaskState.DeadLetter;
                child.LockedBy = null;
                child.LeaseExpiresAt = null;
                child.UpdatedAt = DateTimeOffset.UtcNow;
                touched.Add(child);
            }
        }

        cancellationRegistry.TryCancel(taskId);
        task.State = TaskState.DeadLetter;
        task.LockedBy = null;
        task.LeaseExpiresAt = null;
        task.UpdatedAt = DateTimeOffset.UtcNow;

        if (task.ParentTaskId is { } parentId)
        {
            var parent = await db.Tasks.FirstOrDefaultAsync(t => t.Id == parentId, ct);
            if (parent is not null && parent.State == TaskState.Decomposed)
            {
                parent.State = TaskState.DeadLetter;
                parent.UpdatedAt = DateTimeOffset.UtcNow;
                touched.Add(parent);
            }
        }

        AuditLogger.Record(db, action: "Task.Cancelled", projectId: task.ProjectId, taskId: task.Id);

        await db.SaveChangesAsync(ct);

        foreach (var t in touched)
        {
            await broadcaster.BroadcastTaskStateChangedAsync(t.Id, t.ProjectId, t.State, ct);
        }

        return true;
    }
}
