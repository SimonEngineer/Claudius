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
    IEventBroadcaster broadcaster,
    IOptions<SchedulerOptions> options,
    ILogger<TaskRunnerJob> logger)
{
    /// <summary>Dispatched onto the "supervisor" Hangfire queue by SchedulerTickJob.</summary>
    [Queue("supervisor")]
    public Task ExecuteSupervisorTaskAsync(Guid taskId) => ExecuteAsync(taskId);

    /// <summary>Dispatched onto the "worker" Hangfire queue by SchedulerTickJob.</summary>
    [Queue("worker")]
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
        var model = lane == Lane.Supervisor ? project.CloudModel : project.LocalModel;
        var timeout = lane == Lane.Supervisor ? options.Value.SupervisorRunTimeout : options.Value.WorkerRunTimeout;

        var run = new Run { TaskId = task.Id, Engine = engine, Model = model, Status = RunStatus.Running };
        db.Runs.Add(run);
        await db.SaveChangesAsync(ct);

        var context = new RunContext(
            TaskId: task.Id,
            RunId: run.Id,
            Mode: mode!.Value,
            WorkingDirectory: project.RepoPath,
            Model: model,
            Instruction: BuildInstruction(task, mode.Value),
            PlanJson: task.PlanJson,
            AcceptanceCriteriaJson: task.AcceptanceCriteriaJson,
            Timeout: timeout);

        async Task OnEvent(EngineEvent ev)
        {
            var entity = new TaskEvent { RunId = run.Id, Type = ev.Type, Timestamp = ev.Timestamp, PayloadJson = ev.PayloadJson };
            db.Events.Add(entity);
            await db.SaveChangesAsync(ct);
            await broadcaster.BroadcastEventAsync(task.Id, run.Id, entity, ct);
        }

        EngineResult result;
        try
        {
            var adapter = lane == Lane.Supervisor ? (IEngineAdapter)claudeCodeAdapter : aiderAdapter;
            result = await adapter.RunAsync(context, OnEvent, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TaskRunnerJob: unhandled error running task {TaskId}", taskId);
            result = new EngineResult(EngineOutcome.Failed, $"Unhandled error: {ex.Message}");
        }

        run.Status = result.Outcome switch
        {
            EngineOutcome.Succeeded => RunStatus.Succeeded,
            EngineOutcome.NeedsInput => RunStatus.Succeeded,
            _ => RunStatus.Failed
        };
        run.FinishedAt = DateTimeOffset.UtcNow;
        run.TokensIn = result.TokensIn;
        run.TokensOut = result.TokensOut;
        run.CostUsd = result.CostUsd;
        run.ExitSummaryJson = JsonSerializer.Serialize(new { result.Outcome, result.Summary });

        await ApplyStateTransitionAsync(task, mode.Value, result, run, ct);

        task.LockedBy = null;
        task.LeaseExpiresAt = null;
        task.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        await broadcaster.BroadcastTaskStateChangedAsync(task.Id, task.ProjectId, task.State, ct);
    }

    private async Task ApplyStateTransitionAsync(AgentTask task, EngineMode mode, EngineResult result, Run run, CancellationToken ct)
    {
        switch (result.Outcome)
        {
            case EngineOutcome.Succeeded when mode == EngineMode.Plan:
                task.PlanJson = result.PlanJson;
                task.AcceptanceCriteriaJson = ExtractAcceptanceCriteria(result.PlanJson);
                task.State = TaskState.ReadyForWork;
                break;

            case EngineOutcome.Succeeded when mode == EngineMode.Implement:
                task.State = TaskState.Verifying;
                break;

            case EngineOutcome.Succeeded when mode == EngineMode.Verify:
                task.State = TaskState.Done;
                break;

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
                // Supervisor found issues and supplied a concrete follow-up instruction;
                // no human approval needed, just loop back for another implementation pass.
                task.PlanJson = result.PlanJson ?? task.PlanJson;
                task.State = TaskState.NeedsFix;
                break;

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
                break;
        }

        await Task.CompletedTask;
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
