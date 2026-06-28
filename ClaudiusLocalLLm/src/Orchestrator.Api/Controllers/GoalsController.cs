using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Api.Dtos;
using Orchestrator.Domain;
using Orchestrator.Infrastructure.Persistence;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/goals")]
public class GoalsController(OrchestratorDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Goal>>> List(Guid projectId, CancellationToken ct)
    {
        var goals = await db.Goals.Where(g => g.ProjectId == projectId).OrderByDescending(g => g.CreatedAt).ToListAsync(ct);
        return Ok(goals);
    }

    /// <summary>
    /// Creates a Goal and its first AgentTask in the Queued state (Lane.Supervisor) -- the
    /// scheduler will pick it up on the next tick and have the supervisor turn it into a plan.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Goal>> Create(Guid projectId, CreateGoalRequest request, CancellationToken ct)
    {
        var project = await db.Projects.FindAsync([projectId], ct);
        if (project is null)
        {
            return NotFound($"Project {projectId} not found.");
        }

        var goal = new Goal { ProjectId = projectId, Description = request.Description };
        db.Goals.Add(goal);

        var task = new AgentTask
        {
            ProjectId = projectId,
            GoalId = goal.Id,
            Title = request.Title ?? request.Description,
            Description = request.Description,
            Lane = Lane.Supervisor,
            State = TaskState.Queued,
            Priority = project.Priority
        };
        db.Tasks.Add(task);

        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(List), new { projectId }, goal);
    }
}
