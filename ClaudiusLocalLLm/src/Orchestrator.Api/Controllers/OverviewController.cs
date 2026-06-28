using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Api.Dtos;
using Orchestrator.Domain;
using Orchestrator.Infrastructure.Persistence;

namespace Orchestrator.Api.Controllers;

/// <summary>
/// Backs the dashboard's system-wide overview: aggregate task-state counts, what's actively
/// running, and what needs a human (failures/dead-letters/pending approvals) -- all in one call
/// so the home page doesn't need to fan a request out across every project.
/// </summary>
[ApiController]
[Route("api/overview")]
public class OverviewController(OrchestratorDbContext db) : ControllerBase
{
    private static readonly TaskState[] ActiveStates =
        [TaskState.Planning, TaskState.InProgress, TaskState.Verifying, TaskState.NeedsFix];

    private static readonly TaskState[] AttentionStates =
        [TaskState.AwaitingInput, TaskState.Failed, TaskState.DeadLetter];

    [HttpGet]
    public async Task<ActionResult<OverviewDto>> Get(CancellationToken ct)
    {
        var projectCount = await db.Projects.CountAsync(ct);

        var stateCounts = await db.Tasks
            .GroupBy(t => t.State)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var inProgress = await QuerySummaries(ActiveStates, 20, ct);
        var needsAttention = await QuerySummaries(AttentionStates, 20, ct);

        var pendingApprovals = await db.Approvals
            .Where(a => a.Status == ApprovalStatus.Pending)
            .Include(a => a.Task).ThenInclude(t => t!.Project)
            .OrderBy(a => a.CreatedAt)
            .Take(20)
            .Select(a => new ApprovalSummaryDto(
                a.Id, a.TaskId, a.Task!.ProjectId, a.Task.Project!.Name, a.Task.Title, a.Question, a.CreatedAt))
            .ToListAsync(ct);

        return Ok(new OverviewDto(
            projectCount,
            stateCounts.ToDictionary(s => s.State.ToString(), s => s.Count),
            inProgress,
            needsAttention,
            pendingApprovals));
    }

    private async Task<List<TaskSummaryDto>> QuerySummaries(TaskState[] states, int take, CancellationToken ct)
        => await db.Tasks
            .Where(t => states.Contains(t.State))
            .Include(t => t.Project)
            .OrderByDescending(t => t.UpdatedAt)
            .Take(take)
            .Select(t => new TaskSummaryDto(
                t.Id, t.ProjectId, t.Project!.Name, t.Title, t.State, t.Lane, t.RetryCount, t.UpdatedAt))
            .ToListAsync(ct);
}
