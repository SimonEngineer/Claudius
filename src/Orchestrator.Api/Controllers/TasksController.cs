using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Domain;
using Orchestrator.Domain.Streaming;
using Orchestrator.Infrastructure.Persistence;
using Orchestrator.Infrastructure.Scheduling;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api")]
public class TasksController(
    OrchestratorDbContext db,
    IRunCancellationRegistry cancellationRegistry,
    IEventBroadcaster broadcaster) : ControllerBase
{
    [HttpGet("projects/{projectId:guid}/tasks")]
    public async Task<ActionResult<IEnumerable<AgentTask>>> ListForProject(Guid projectId, CancellationToken ct)
    {
        var tasks = await db.Tasks
            .Where(t => t.ProjectId == projectId)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);
        return Ok(tasks);
    }

    [HttpGet("tasks/{id:guid}")]
    public async Task<ActionResult<AgentTask>> Get(Guid id, CancellationToken ct)
    {
        var task = await db.Tasks
            .Include(t => t.Runs)
            .Include(t => t.Approvals)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        return task is null ? NotFound() : Ok(task);
    }

    /// <summary>
    /// Cancels a task: stops the in-flight engine process immediately (if currently running) and
    /// marks the task dead-lettered, freeing the slot for the next scheduler tick. Cancelling a
    /// task that isn't currently running still dead-letters it -- the user explicitly opted out.
    /// </summary>
    [HttpPost("tasks/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null)
        {
            return NotFound();
        }

        if (task.State is TaskState.Done or TaskState.Failed or TaskState.DeadLetter)
        {
            return Conflict($"Task {id} is already terminal ({task.State}).");
        }

        cancellationRegistry.TryCancel(id);

        task.State = TaskState.DeadLetter;
        task.LockedBy = null;
        task.LeaseExpiresAt = null;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        await broadcaster.BroadcastTaskStateChangedAsync(task.Id, task.ProjectId, task.State, ct);

        return NoContent();
    }

    /// <summary>
    /// Events for a run, optionally filtered to those after `sinceId` -- this is what lets the
    /// dashboard reconnect after a network blip or page reload and replay only what it missed
    /// before resuming the live SignalR tail.
    /// </summary>
    [HttpGet("runs/{runId:guid}/events")]
    public async Task<ActionResult<IEnumerable<TaskEvent>>> GetEvents(Guid runId, [FromQuery] long sinceId = 0, CancellationToken ct = default)
    {
        var events = await db.Events
            .Where(e => e.RunId == runId && e.Id > sinceId)
            .OrderBy(e => e.Id)
            .ToListAsync(ct);
        return Ok(events);
    }
}
