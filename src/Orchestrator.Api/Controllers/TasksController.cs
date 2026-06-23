using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Domain;
using Orchestrator.Infrastructure.Persistence;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api")]
public class TasksController(OrchestratorDbContext db) : ControllerBase
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
