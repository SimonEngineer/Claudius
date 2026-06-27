using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Api.Dtos;
using Orchestrator.Domain;
using Orchestrator.Domain.Streaming;
using Orchestrator.Infrastructure.Persistence;
using Orchestrator.Infrastructure.Scheduling;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/approvals")]
public class ApprovalsController(OrchestratorDbContext db, IEventBroadcaster broadcaster) : ControllerBase
{
    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<Approval>>> ListPending(CancellationToken ct)
    {
        var approvals = await db.Approvals
            .Where(a => a.Status == ApprovalStatus.Pending)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);
        return Ok(approvals);
    }

    /// <summary>
    /// Approving/answering unblocks the task immediately: it flips back to ReadyForWork so the
    /// next scheduler tick can pick it up -- the queue never sits idle waiting on a human past
    /// the moment they actually respond.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, ResolveApprovalRequest request, CancellationToken ct)
        => await Resolve(id, ApprovalStatus.Approved, request, ct);

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, ResolveApprovalRequest request, CancellationToken ct)
        => await Resolve(id, ApprovalStatus.Rejected, request, ct);

    private async Task<IActionResult> Resolve(Guid id, ApprovalStatus status, ResolveApprovalRequest request, CancellationToken ct)
    {
        var approval = await db.Approvals.Include(a => a.Task).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (approval is null)
        {
            return NotFound();
        }

        if (approval.Status != ApprovalStatus.Pending)
        {
            return Conflict($"Approval {id} was already {approval.Status}.");
        }

        approval.Status = status;
        approval.Answer = request.Answer;
        approval.ResolvedBy = request.ResolvedBy;
        approval.ResolvedAt = DateTimeOffset.UtcNow;

        var task = approval.Task!;
        if (status == ApprovalStatus.Approved)
        {
            if (approval.Kind == ApprovalKind.PlanReview)
            {
                PlanDecomposer.Apply(task, db);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(request.Answer))
                {
                    task.Description = $"{task.Description}\n\nUser answered: {request.Answer}";
                }
                task.State = TaskState.ReadyForWork;
            }
        }
        else if (approval.Kind == ApprovalKind.PlanReview)
        {
            // Rejecting a plan just means it needs another pass, not that the task is dead --
            // send it back to the supervisor with the reviewer's feedback folded in.
            task.PlanJson = null;
            task.AcceptanceCriteriaJson = null;
            if (!string.IsNullOrWhiteSpace(request.Answer))
            {
                task.Description = $"{task.Description}\n\nPlan rejected, feedback: {request.Answer}";
            }
            task.State = TaskState.Queued;
        }
        else
        {
            task.State = TaskState.DeadLetter;
        }
        task.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        await broadcaster.BroadcastApprovalResolvedAsync(task.Id, task.ProjectId, approval.Id, status, ct);
        await broadcaster.BroadcastTaskStateChangedAsync(task.Id, task.ProjectId, task.State, ct);

        return NoContent();
    }
}
