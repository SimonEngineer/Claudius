using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orchestrator.Api.Dtos;
using Orchestrator.Domain;
using Orchestrator.Infrastructure.Persistence;
using Orchestrator.Infrastructure.Scheduling;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/system-health")]
public class SystemHealthController(OrchestratorDbContext db, IOptions<SchedulerOptions> options) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SystemHealthDto>> Get(CancellationToken ct)
    {
        var dbHealthy = await db.Database.CanConnectAsync(ct);
        var opts = options.Value;
        var now = DateTimeOffset.UtcNow;

        var supervisor = await BuildLaneHealthAsync(
            Lane.Supervisor, TaskClaimingService.SupervisorEligibleStates, opts.SupervisorConcurrency, ct);
        var worker = await BuildLaneHealthAsync(
            Lane.Worker, TaskClaimingService.WorkerEligibleStates, opts.WorkerConcurrency, ct);

        var staleLeaseCount = await db.Tasks.CountAsync(
            t => t.LockedBy != null && t.LeaseExpiresAt != null && t.LeaseExpiresAt < now, ct);
        var deadLetterCount = await db.Tasks.CountAsync(t => t.State == TaskState.DeadLetter, ct);
        var pausedProjectCount = await db.Projects.CountAsync(p => p.IsPaused, ct);

        return Ok(new SystemHealthDto(dbHealthy, supervisor, worker, staleLeaseCount, deadLetterCount, pausedProjectCount));
    }

    private async Task<LaneHealthDto> BuildLaneHealthAsync(
        Lane lane, TaskState[] eligibleStates, int capacity, CancellationToken ct)
    {
        var queueDepth = await db.Tasks.CountAsync(
            t => t.Lane == lane && eligibleStates.Contains(t.State) && t.LockedBy == null, ct);
        var inFlight = await db.Tasks.CountAsync(
            t => t.Lane == lane && TaskClaimingService.ActiveLeasedStates.Contains(t.State) && t.LockedBy != null, ct);

        return new LaneHealthDto(queueDepth, inFlight, capacity);
    }
}
