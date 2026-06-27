using System.Text.Json;
using Orchestrator.Domain;
using Orchestrator.Infrastructure.Persistence;

namespace Orchestrator.Infrastructure.Scheduling;

/// <summary>
/// Turns an approved plan into runnable work: either a single ReadyForWork task (one-step
/// plans) or a Decomposed parent plus one ReadyForWork child per step. Shared between
/// TaskRunnerJob (immediate path) and ApprovalsController (plan-review-gated path) so both
/// produce identical task graphs regardless of whether a human reviewed the plan first.
/// </summary>
public static class PlanDecomposer
{
    public static void Apply(AgentTask task, OrchestratorDbContext db)
    {
        var steps = ExtractPlanSteps(task.PlanJson);
        if (steps.Count > 1)
        {
            foreach (var step in steps)
            {
                db.Tasks.Add(new AgentTask
                {
                    ProjectId = task.ProjectId,
                    GoalId = task.GoalId,
                    ParentTaskId = task.Id,
                    Title = step,
                    Description = step,
                    Lane = Lane.Worker,
                    State = TaskState.ReadyForWork,
                    Priority = task.Priority,
                    AcceptanceCriteriaJson = task.AcceptanceCriteriaJson
                });
            }
            task.State = TaskState.Decomposed;
        }
        else
        {
            task.State = TaskState.ReadyForWork;
        }
    }

    public static List<string> ExtractPlanSteps(string? planJson)
    {
        if (planJson is null)
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(planJson);
            if (!doc.RootElement.TryGetProperty("plan", out var plan) || plan.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return plan.EnumerateArray()
                .Select(e => e.GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
