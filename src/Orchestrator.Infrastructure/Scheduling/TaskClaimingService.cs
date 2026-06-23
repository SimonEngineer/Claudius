using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Domain;
using Orchestrator.Infrastructure.Persistence;

namespace Orchestrator.Infrastructure.Scheduling;

/// <summary>
/// Owns the "pick the next runnable task" logic. Claiming uses Postgres row locks
/// (FOR UPDATE SKIP LOCKED) so multiple ticks/instances can never double-claim the same task.
/// </summary>
public class TaskClaimingService(
    OrchestratorDbContext db,
    IOptions<SchedulerOptions> options,
    ILogger<TaskClaimingService> logger)
{
    private static readonly TaskState[] SupervisorEligibleStates = [TaskState.Queued, TaskState.Verifying];
    private static readonly TaskState[] WorkerEligibleStates = [TaskState.ReadyForWork, TaskState.NeedsFix];
    private static readonly TaskState[] ActiveLeasedStates =
        [TaskState.Planning, TaskState.InProgress, TaskState.Verifying];

    public async Task<List<AgentTask>> ClaimNextBatchAsync(Lane lane, CancellationToken ct)
    {
        var opts = options.Value;
        var capacity = lane == Lane.Supervisor ? opts.SupervisorConcurrency : opts.WorkerConcurrency;

        var inFlight = await db.Tasks.CountAsync(
            t => t.Lane == lane && ActiveLeasedStates.Contains(t.State) && t.LockedBy != null, ct);

        var freeSlots = capacity - inFlight;
        if (freeSlots <= 0)
        {
            return [];
        }

        var eligibleStates = lane == Lane.Supervisor ? SupervisorEligibleStates : WorkerEligibleStates;

        // Pull a generous candidate window (more than freeSlots) so per-project concurrency caps
        // don't starve later candidates when an earlier project is already at its own limit.
        var candidates = await db.Tasks
            .Where(t => t.Lane == lane && eligibleStates.Contains(t.State) && t.LockedBy == null)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.CreatedAt)
            .Take(freeSlots * 5)
            .Include(t => t.Project)
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return [];
        }

        var perProjectInFlight = (await db.Tasks
            .Where(t => t.Lane == lane && ActiveLeasedStates.Contains(t.State) && t.LockedBy != null)
            .GroupBy(t => t.ProjectId)
            .Select(g => new { ProjectId = g.Key, Count = g.Count() })
            .ToListAsync(ct))
            .ToDictionary(x => x.ProjectId, x => x.Count);

        var claimed = new List<AgentTask>();
        var workerId = $"{Environment.MachineName}:{Environment.ProcessId}";

        foreach (var task in candidates)
        {
            if (claimed.Count >= freeSlots)
            {
                break;
            }

            var maxForProject = lane == Lane.Worker ? task.Project!.MaxWorkerConcurrency : opts.SupervisorConcurrency;
            var currentForProject = perProjectInFlight.GetValueOrDefault(task.ProjectId, 0);
            if (currentForProject >= maxForProject)
            {
                continue;
            }

            // Re-check + claim atomically: only succeeds if another tick hasn't already grabbed it.
            var rowsAffected = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Tasks"
                SET "LockedBy" = {workerId},
                    "LeaseExpiresAt" = {DateTimeOffset.UtcNow.Add(opts.LeaseDuration)},
                    "State" = {(lane == Lane.Supervisor ? NextSupervisorState(task.State) : TaskState.InProgress).ToString()},
                    "UpdatedAt" = {DateTimeOffset.UtcNow}
                WHERE "Id" = {task.Id} AND "LockedBy" IS NULL
                """, ct);

            if (rowsAffected == 0)
            {
                continue; // lost the race to another claimer
            }

            task.LockedBy = workerId;
            task.LeaseExpiresAt = DateTimeOffset.UtcNow.Add(opts.LeaseDuration);
            task.State = lane == Lane.Supervisor ? NextSupervisorState(task.State) : TaskState.InProgress;
            perProjectInFlight[task.ProjectId] = currentForProject + 1;
            claimed.Add(task);

            logger.LogInformation("Claimed task {TaskId} ({Title}) for lane {Lane}, new state {State}",
                task.Id, task.Title, lane, task.State);
        }

        return claimed;
    }

    private static TaskState NextSupervisorState(TaskState current) => current switch
    {
        TaskState.Queued => TaskState.Planning,
        TaskState.Verifying => TaskState.Verifying, // already verifying; claim is for the verify run itself
        _ => current
    };

    /// <summary>Requeues tasks whose lease expired without the holder finishing (crash recovery).</summary>
    public async Task<int> RequeueStaleLeasesAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var stale = await db.Tasks
            .Where(t => t.LockedBy != null && t.LeaseExpiresAt != null && t.LeaseExpiresAt < now)
            .ToListAsync(ct);

        foreach (var task in stale)
        {
            logger.LogWarning("Lease expired for task {TaskId} (held by {LockedBy}), requeueing", task.Id, task.LockedBy);

            task.LockedBy = null;
            task.LeaseExpiresAt = null;
            task.RetryCount += 1;

            task.State = task.RetryCount > AgentTask.MaxRetries
                ? TaskState.DeadLetter
                : task.State switch
                {
                    TaskState.Planning => TaskState.Queued,
                    TaskState.InProgress => TaskState.ReadyForWork,
                    TaskState.Verifying => TaskState.ReadyForWork, // re-implement rather than re-verify blindly
                    _ => TaskState.Queued
                };

            task.UpdatedAt = now;
        }

        if (stale.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return stale.Count;
    }
}
