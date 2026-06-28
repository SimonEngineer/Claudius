using System.Text.Json;
using Orchestrator.Domain;
using Orchestrator.Infrastructure.Persistence;

namespace Orchestrator.Infrastructure.Scheduling;

/// <summary>
/// Turns an approved plan into runnable work: either a single ReadyForWork task (one-step
/// plans) or a Decomposed parent plus one child per step. Children are chained sequentially via
/// DependsOnTaskId in plan order -- only the first step starts ReadyForWork, each later step sits
/// Blocked until the step before it reaches Done -- since a plan's steps are usually written
/// assuming earlier steps' edits are already on disk. Shared between TaskRunnerJob (immediate
/// path) and ApprovalsController (plan-review-gated path) so both produce identical task graphs
/// regardless of whether a human reviewed the plan first.
/// </summary>
public static class PlanDecomposer
{
    public static void Apply(AgentTask task, OrchestratorDbContext db)
    {
        var steps = ExtractPlanSteps(task.PlanJson);
        if (steps.Count > 1)
        {
            AgentTask? previous = null;
            foreach (var step in steps)
            {
                var child = new AgentTask
                {
                    ProjectId = task.ProjectId,
                    GoalId = task.GoalId,
                    ParentTaskId = task.Id,
                    DependsOnTaskId = previous?.Id,
                    Title = step,
                    Description = step,
                    Lane = Lane.Worker,
                    State = previous is null ? TaskState.ReadyForWork : TaskState.Blocked,
                    Priority = task.Priority,
                    AcceptanceCriteriaJson = task.AcceptanceCriteriaJson
                };
                db.Tasks.Add(child);
                previous = child;
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
