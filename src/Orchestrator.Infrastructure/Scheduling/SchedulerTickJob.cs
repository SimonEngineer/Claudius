using Hangfire;
using Microsoft.Extensions.Logging;
using Orchestrator.Domain;

namespace Orchestrator.Infrastructure.Scheduling;

/// <summary>
/// Registered as a Hangfire recurring job (see Program.cs). Each tick: claims any
/// newly-runnable tasks up to free lane capacity and enqueues them onto the matching
/// Hangfire queue. This is what makes "task waiting on approval frees the slot
/// immediately" work -- the tick runs independently of any single task's lifecycle.
/// </summary>
public class SchedulerTickJob(
    TaskClaimingService claimingService,
    IBackgroundJobClient backgroundJobClient,
    ILogger<SchedulerTickJob> logger)
{
    public async Task TickAsync()
    {
        await RequeueStaleAsync();
        await ClaimAndDispatchAsync(Lane.Supervisor);
        await ClaimAndDispatchAsync(Lane.Worker);
    }

    private async Task RequeueStaleAsync()
    {
        var requeued = await claimingService.RequeueStaleLeasesAsync(CancellationToken.None);
        if (requeued > 0)
        {
            logger.LogWarning("Requeued {Count} task(s) with expired leases", requeued);
        }
    }

    private async Task ClaimAndDispatchAsync(Lane lane)
    {
        var claimed = await claimingService.ClaimNextBatchAsync(lane, CancellationToken.None);
        foreach (var task in claimed)
        {
            if (lane == Lane.Supervisor)
            {
                backgroundJobClient.Enqueue<TaskRunnerJob>(job => job.ExecuteSupervisorTaskAsync(task.Id));
            }
            else
            {
                backgroundJobClient.Enqueue<TaskRunnerJob>(job => job.ExecuteWorkerTaskAsync(task.Id));
            }

            logger.LogInformation("Dispatched task {TaskId} onto {Lane} queue", task.Id, lane);
        }
    }
}
