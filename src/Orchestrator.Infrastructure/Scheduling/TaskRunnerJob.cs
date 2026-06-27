using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Domain;
using Orchestrator.Domain.Engines;
using Orchestrator.Domain.Streaming;
using Orchestrator.Infrastructure.Engines;
using Orchestrator.Infrastructure.Persistence;

namespace Orchestrator.Infrastructure.Scheduling;

/// <summary>
/// Executes exactly one claimed AgentTask by dispatching it to the right engine adapter,
/// persisting + broadcasting every event as it streams in, then applying the task state
/// machine transition based on the EngineResult. Enqueued onto a Hangfire queue
/// ("supervisor" or "worker") by SchedulerTickJob.
/// </summary>
public class TaskRunnerJob(
    OrchestratorDbContext db,
    ClaudeCodeAdapter claudeCodeAdapter,
    AiderAdapter aiderAdapter,
    GitWorktreeService worktreeService,
    IRunCancellationRegistry cancellationRegistry,
    IEventBroadcaster broadcaster,
    IModelCircuitBreaker circuitBreaker,
    IOptions<SchedulerOptions> options,
    ILogger<TaskRunnerJob> logger)
{
    private const int MaxSkillsInPrompt = 10;

    /// <summary>
    /// AutomaticRetry is disabled: the task state machine (RetryCount/DeadLetter) is our retry
    /// policy. Letting Hangfire also retry on top would double-dispatch the same AgentTask and
    /// break the single-claim invariant the lease/lock columns are there to enforce.
    /// </summary>
    [Queue("supervisor")]
    [AutomaticRetry(Attempts = 0)]
    public Task ExecuteSupervisorTaskAsync(Guid taskId) => ExecuteAsync(taskId);

    [Queue("worker")]
    [AutomaticRetry(Attempts = 0)]
    public Task ExecuteWorkerTaskAsync(Guid taskId) => ExecuteAsync(taskId);

    private async Task ExecuteAsync(Guid taskId)
    {
        var ct = CancellationToken.None;
        var task = await db.Tasks.Include(t => t.Project).FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task is null)
        {
            logger.LogWarning("TaskRunnerJob: task {TaskId} not found, skipping", taskId);
            return;
        }

        var (lane, mode) = ResolveLaneAndMode(task.State);
        if (lane is null)
        {
            logger.LogWarning("TaskRunnerJob: task {TaskId} in unexpected state {State}, skipping", taskId, task.State);
            return;
        }

        var project = task.Project!;
        var engine = lane == Lane.Supervisor ? EngineType.ClaudeCode : EngineType.Aider;
        var model = lane == Lane.Supervisor ? project.SupervisorModel : project.WorkerModel;
        var timeout = lane == Lane.Supervisor ? options.Value.SupervisorRunTimeout : options.Value.WorkerRunTimeout;

        if (circuitBreaker.IsOpen(model))
        {
            logger.LogWarning(
                "TaskRunnerJob: circuit breaker open for model {Model}, requeueing task {TaskId} without spending an attempt",
                model, taskId);
            task.LockedBy = null;
            task.LeaseExpiresAt = null;
            task.State = lane == Lane.Supervisor
                ? (task.State == TaskState.Verifying ? TaskState.Verifying : TaskState.Queued)
                : TaskState.ReadyForWork;
            task.NextAttemptAt = circuitBreaker.OpenUntil(model) ?? DateTimeOffset.UtcNow.Add(options.Value.CircuitBreakerCooldown);
            task.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            await broadcaster.BroadcastTaskStateChangedAsync(task.Id, task.ProjectId, task.State, ct);
            return;
        }

        var run = new Run { TaskId = task.Id, Engine = engine, Model = model, Status = RunStatus.Running };
        db.Runs.Add(run);
        await db.SaveChangesAsync(ct);

        var rootTaskId = task.ParentTaskId ?? task.Id;
        string workingDirectory;
        try
        {
            workingDirectory = await worktreeService.EnsureWorktreeAsync(project.RepoPath, rootTaskId, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TaskRunnerJob: failed to prepare worktree for task {TaskId}, falling back to repo root", taskId);
            workingDirectory = project.RepoPath;
        }

        var existingSkillsJson = await BuildExistingSkillsJsonAsync(project.Id, ct);

        var context = new RunContext(
            TaskId: task.Id,
            RunId: run.Id,
            Mode: mode!.Value,
            WorkingDirectory: workingDirectory,
            Model: model,
            Instruction: BuildInstruction(task, mode.Value),
            PlanJson: task.PlanJson,
            AcceptanceCriteriaJson: task.AcceptanceCriteriaJson,
            Timeout: timeout,
            ExistingSkillsJson: existingSkillsJson);

        async Task OnEvent(EngineEvent ev)
        {
            var entity = new TaskEvent { RunId = run.Id, Type = ev.Type, Timestamp = ev.Timestamp, PayloadJson = ev.PayloadJson };
            db.Events.Add(entity);
            await db.SaveChangesAsync(ct);
            await broadcaster.BroadcastEventAsync(task.Id, run.Id, entity, ct);
        }

        var runToken = cancellationRegistry.Register(task.Id);
        EngineResult result;
        try
        {
            var adapter = lane == Lane.Supervisor ? (IEngineAdapter)claudeCodeAdapter : aiderAdapter;
            result = await adapter.RunAsync(context, OnEvent, runToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("TaskRunnerJob: run {RunId} for task {TaskId} was cancelled", run.Id, taskId);
            result = new EngineResult(EngineOutcome.Cancelled, "Cancelled by user.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TaskRunnerJob: unhandled error running task {TaskId}", taskId);
            result = new EngineResult(EngineOutcome.Failed, $"Unhandled error: {ex.Message}");
        }
        finally
        {
            cancellationRegistry.Unregister(task.Id);
        }

        if (result.Outcome is EngineOutcome.Succeeded or EngineOutcome.NeedsInput)
        {
            circuitBreaker.RecordSuccess(model);
        }
        else if (result.Outcome == EngineOutcome.Failed)
        {
            circuitBreaker.RecordFailure(model);
        }

        run.Status = result.Outcome switch
        {
            EngineOutcome.Succeeded => RunStatus.Succeeded,
            EngineOutcome.NeedsInput => RunStatus.Succeeded,
            EngineOutcome.Cancelled => RunStatus.Cancelled,
            _ => RunStatus.Failed
        };
        run.FinishedAt = DateTimeOffset.UtcNow;
        run.TokensIn = result.TokensIn;
        run.TokensOut = result.TokensOut;
        run.CostUsd = result.CostUsd;
        run.ExitSummaryJson = JsonSerializer.Serialize(new { result.Outcome, result.Summary });

        var touchedParent = await ApplyStateTransitionAsync(task, mode.Value, result, run, ct);

        task.LockedBy = null;
        task.LeaseExpiresAt = null;
        task.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        await broadcaster.BroadcastTaskStateChangedAsync(task.Id, task.ProjectId, task.State, ct);
        if (touchedParent is not null)
        {
            await broadcaster.BroadcastTaskStateChangedAsync(touchedParent.Id, touchedParent.ProjectId, touchedParent.State, ct);
        }
    }

    private async Task<AgentTask?> ApplyStateTransitionAsync(AgentTask task, EngineMode mode, EngineResult result, Run run, CancellationToken ct)
    {
        switch (result.Outcome)
        {
            case EngineOutcome.Succeeded when mode == EngineMode.Plan:
                task.PlanJson = result.PlanJson;
                task.AcceptanceCriteriaJson = ExtractAcceptanceCriteria(result.PlanJson);
                if (task.Project!.RequirePlanApproval)
                {
                    var planApproval = new Approval
                    {
                        TaskId = task.Id,
                        RunId = run.Id,
                        Kind = ApprovalKind.PlanReview,
                        Question = "Review the proposed plan before any worker tasks start."
                    };
                    db.Approvals.Add(planApproval);
                    task.State = TaskState.AwaitingInput;
                    await broadcaster.BroadcastApprovalRequestedAsync(task.Id, task.ProjectId, planApproval, ct);
                }
                else
                {
                    PlanDecomposer.Apply(task, db);
                }
                break;

            case EngineOutcome.Succeeded when mode == EngineMode.Implement:
                task.State = TaskState.Verifying;
                break;

            case EngineOutcome.Succeeded when mode == EngineMode.Verify:
                task.State = TaskState.Done;
                if (result.SkillName is not null && result.SkillContent is not null)
                {
                    db.Skills.Add(new Skill
                    {
                        ProjectId = task.ProjectId,
                        Name = result.SkillName,
                        Content = result.SkillContent,
                        CreatedByRunId = run.Id
                    });
                }
                return await PropagateToParentAsync(task, TaskState.Done, ct);

            case EngineOutcome.NeedsInput when mode == EngineMode.Implement:
                var approval = new Approval
                {
                    TaskId = task.Id,
                    RunId = run.Id,
                    Question = result.ApprovalQuestion ?? "The worker needs more information to proceed.",
                    OptionsJson = result.ApprovalOptions is null ? null : JsonSerializer.Serialize(result.ApprovalOptions)
                };
                db.Approvals.Add(approval);
                task.State = TaskState.AwaitingInput;
                await broadcaster.BroadcastApprovalRequestedAsync(task.Id, task.ProjectId, approval, ct);
                break;

            case EngineOutcome.NeedsInput when mode == EngineMode.Verify:
                // Supervisor found issues and supplied a concrete follow-up instruction; no human
                // approval needed, just loop back for another implementation pass. Counts against
                // RetryCount like Failed does, so a stuck Implement<->Verify loop still dead-letters
                // instead of cycling forever.
                task.RetryCount += 1;
                if (task.RetryCount > AgentTask.MaxRetries)
                {
                    task.State = TaskState.DeadLetter;
                    return await PropagateToParentAsync(task, TaskState.DeadLetter, ct);
                }
                task.PlanJson = result.PlanJson ?? task.PlanJson;
                task.State = TaskState.NeedsFix;
                task.NextAttemptAt = DateTimeOffset.UtcNow.Add(options.Value.GetRetryBackoff(task.RetryCount));
                break;

            case EngineOutcome.Cancelled:
                task.State = TaskState.DeadLetter;
                return await PropagateToParentAsync(task, TaskState.DeadLetter, ct);

            case EngineOutcome.Failed:
            default:
                task.RetryCount += 1;
                task.State = task.RetryCount > AgentTask.MaxRetries
                    ? TaskState.DeadLetter
                    : mode switch
                    {
                        EngineMode.Plan => TaskState.Queued,
                        EngineMode.Implement => TaskState.ReadyForWork,
                        EngineMode.Verify => TaskState.ReadyForWork,
                        _ => TaskState.Queued
                    };
                if (task.State == TaskState.DeadLetter)
                {
                    return await PropagateToParentAsync(task, TaskState.DeadLetter, ct);
                }
                task.NextAttemptAt = DateTimeOffset.UtcNow.Add(options.Value.GetRetryBackoff(task.RetryCount));
                break;
        }

        return null;
    }

    /// <summary>
    /// Bubbles a child task's completion (or terminal failure) up to its decomposed parent. The
    /// child's own State mutation above is only in the EF change tracker at this point (not yet
    /// SaveChanges'd), so siblings are queried excluding the child by id and the in-memory
    /// `childState` is trusted instead of re-reading the child's row from the database.
    /// </summary>
    private async Task<AgentTask?> PropagateToParentAsync(AgentTask child, TaskState childState, CancellationToken ct)
    {
        if (child.ParentTaskId is not { } parentId)
        {
            return null;
        }

        var parent = await db.Tasks.FirstOrDefaultAsync(t => t.Id == parentId, ct);
        if (parent is null || parent.State != TaskState.Decomposed)
        {
            return null;
        }

        if (childState == TaskState.DeadLetter)
        {
            parent.State = TaskState.DeadLetter;
            parent.UpdatedAt = DateTimeOffset.UtcNow;
            return parent;
        }

        var incompleteSiblings = await db.Tasks.CountAsync(
            t => t.ParentTaskId == parentId && t.Id != child.Id && t.State != TaskState.Done, ct);

        if (incompleteSiblings == 0)
        {
            // All children done: hand the combined work back to the supervisor for a final check.
            parent.State = TaskState.Verifying;
            parent.UpdatedAt = DateTimeOffset.UtcNow;
            return parent;
        }

        return null;
    }

    private async Task<string?> BuildExistingSkillsJsonAsync(Guid projectId, CancellationToken ct)
    {
        var skills = await db.Skills
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(MaxSkillsInPrompt)
            .Select(s => new { s.Name, s.Content })
            .ToListAsync(ct);

        return skills.Count == 0 ? null : JsonSerializer.Serialize(skills);
    }

    private static string? ExtractAcceptanceCriteria(string? planJson)
    {
        if (planJson is null)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(planJson);
            return doc.RootElement.TryGetProperty("acceptanceCriteria", out var ac) ? ac.GetRawText() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static (Lane? Lane, EngineMode? Mode) ResolveLaneAndMode(TaskState state) => state switch
    {
        TaskState.Planning => (Lane.Supervisor, EngineMode.Plan),
        TaskState.Verifying => (Lane.Supervisor, EngineMode.Verify),
        TaskState.InProgress => (Lane.Worker, EngineMode.Implement),
        _ => (null, null)
    };

    private static string BuildInstruction(AgentTask task, EngineMode mode) => mode switch
    {
        EngineMode.Implement when task.PlanJson is not null => $"{task.Title}\n\nPlan: {task.PlanJson}",
        _ => task.Description ?? task.Title
    };
}
