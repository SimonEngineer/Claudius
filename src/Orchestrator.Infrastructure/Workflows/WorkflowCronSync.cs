using Hangfire;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Infrastructure.Persistence;

namespace Orchestrator.Infrastructure.Workflows;

/// <summary>
/// Mirrors enabled workflows' trigger.cron nodes onto Hangfire recurring jobs. Re-running this is
/// always safe (job id is deterministic per workflow) -- it's called once at startup and again
/// after every create/update/delete/enable-toggle in WorkflowsController, so cron changes take
/// effect immediately rather than waiting for a restart.
/// </summary>
public class WorkflowCronSync(OrchestratorDbContext db, IRecurringJobManager recurringJobs)
{
    private static string JobId(Guid workflowId) => $"workflow-cron-{workflowId:N}";

    public async Task SyncAsync(CancellationToken ct)
    {
        var workflows = await db.Workflows.ToListAsync(ct);
        foreach (var workflow in workflows)
        {
            var jobId = JobId(workflow.Id);
            var cronExpression = workflow.IsEnabled
                ? WorkflowDefinition.Parse(workflow.DefinitionJson)
                    .TriggerNodesOfType("trigger.cron")
                    .Select(n => WorkflowTemplating.GetConfigString(n.Config, "cronExpression"))
                    .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))
                : null;

            if (cronExpression is null)
            {
                recurringJobs.RemoveIfExists(jobId);
            }
            else
            {
                recurringJobs.AddOrUpdate<WorkflowEngine>(jobId, e => e.RunEnqueuedAsync(workflow.Id, "{}"), cronExpression);
            }
        }
    }
}
