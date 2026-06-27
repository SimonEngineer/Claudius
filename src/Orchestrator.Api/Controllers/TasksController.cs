using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Domain;
using Orchestrator.Domain.Streaming;
using Orchestrator.Infrastructure.Engines;
using Orchestrator.Infrastructure.Persistence;
using Orchestrator.Infrastructure.Scheduling;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api")]
public class TasksController(
    OrchestratorDbContext db,
    TaskLifecycleService lifecycleService,
    GitWorktreeService worktreeService) : ControllerBase
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
    /// Cancelling a decomposed parent cascades to its still-active children; cancelling a child
    /// (or the parent itself) propagates up to a decomposed parent, same as an engine-driven
    /// dead-letter would -- see TaskLifecycleService.
    /// </summary>
    [HttpPost("tasks/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var result = await lifecycleService.CancelAsync(id, ct);
        return result switch
        {
            null => NotFound(),
            false => Conflict($"Task {id} is already terminal."),
            true => NoContent()
        };
    }

    /// <summary>
    /// Discards uncommitted changes in the task's git worktree (reset --hard + clean -fd) --
    /// the rollback affordance for when an agent's run went off the rails and the in-progress
    /// edits shouldn't be kept. Doesn't change the task's state; it's safe to run on a task
    /// that's about to be retried or cancelled.
    /// </summary>
    [HttpPost("tasks/{id:guid}/discard-changes")]
    public async Task<IActionResult> DiscardChanges(Guid id, CancellationToken ct)
    {
        var task = await db.Tasks.Include(t => t.Project).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null)
        {
            return NotFound();
        }

        var rootTaskId = task.ParentTaskId ?? task.Id;
        try
        {
            await worktreeService.DiscardChangesAsync(task.Project!.RepoPath, rootTaskId, ct);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }

        AuditLogger.Record(db, action: "Task.ChangesDiscarded", projectId: task.ProjectId, taskId: task.Id);
        await db.SaveChangesAsync(ct);

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
