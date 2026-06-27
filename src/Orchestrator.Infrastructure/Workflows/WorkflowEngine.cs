using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orchestrator.Domain;
using Orchestrator.Infrastructure.Persistence;

namespace Orchestrator.Infrastructure.Workflows;

public class WorkflowEngine(
    OrchestratorDbContext db,
    IEnumerable<IWorkflowNodeExecutor> executors,
    IBackgroundJobClient backgroundJobClient,
    ILogger<WorkflowEngine> logger) : IWorkflowEngine
{
    private readonly Dictionary<string, IWorkflowNodeExecutor> _executorsByType = executors.ToDictionary(e => e.Type);

    public async Task TriggerEventAsync(string eventName, object payload, CancellationToken ct)
    {
        var payloadJson = JsonSerializer.Serialize(payload);

        var enabledWorkflows = await db.Workflows.Where(w => w.IsEnabled).ToListAsync(ct);
        foreach (var workflow in enabledWorkflows)
        {
            var definition = WorkflowDefinition.Parse(workflow.DefinitionJson);
            var matches = definition.TriggerNodesOfType("trigger.event")
                .Any(n => WorkflowTemplating.GetConfigString(n.Config, "eventName") == eventName);
            if (!matches)
            {
                continue;
            }

            logger.LogInformation("Workflow {WorkflowId} matched event {EventName}; enqueueing.", workflow.Id, eventName);
            backgroundJobClient.Enqueue<WorkflowEngine>(e => e.RunEnqueuedAsync(workflow.Id, payloadJson));
        }
    }

    /// <summary>Hangfire entry point -- mirrors TaskRunnerJob.ExecuteSupervisorTaskAsync's pattern
    /// of a thin no-CancellationToken wrapper, since Hangfire jobs don't get the caller's token.</summary>
    public Task RunEnqueuedAsync(Guid workflowId, string triggerPayloadJson) =>
        ExecuteWorkflowAsync(workflowId, triggerPayloadJson, CancellationToken.None);

    public async Task<Guid> ExecuteWorkflowAsync(Guid workflowId, string triggerPayloadJson, CancellationToken ct)
    {
        var workflow = await db.Workflows.FirstOrDefaultAsync(w => w.Id == workflowId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found.");

        var definition = WorkflowDefinition.Parse(workflow.DefinitionJson);
        using var triggerDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(triggerPayloadJson) ? "{}" : triggerPayloadJson);
        var triggerPayload = triggerDoc.RootElement;

        var run = new WorkflowRun { WorkflowId = workflow.Id, TriggerPayloadJson = triggerPayloadJson };
        db.WorkflowRuns.Add(run);
        await db.SaveChangesAsync(ct);

        var stepLog = new List<object>();
        var visited = new HashSet<string>();
        var queue = new Queue<string>(definition.Nodes.Where(n => n.Type.StartsWith("trigger.")).SelectMany(t => definition.NodesDownstreamOf(t.Id)).Select(n => n.Id).Distinct());

        var failed = false;
        while (queue.Count > 0 && !failed)
        {
            var nodeId = queue.Dequeue();
            if (!visited.Add(nodeId))
            {
                continue;
            }

            var node = definition.Nodes.First(n => n.Id == nodeId);
            if (!_executorsByType.TryGetValue(node.Type, out var executor))
            {
                stepLog.Add(new { nodeId, type = node.Type, status = "Failed", error = $"No executor registered for node type '{node.Type}'." });
                failed = true;
                break;
            }

            var startedAt = DateTimeOffset.UtcNow;
            try
            {
                var output = await executor.ExecuteAsync(new WorkflowNodeContext(node.Config, triggerPayload, db, ct));
                stepLog.Add(new { nodeId, type = node.Type, status = "Succeeded", output, startedAt, finishedAt = DateTimeOffset.UtcNow });
                foreach (var next in definition.NodesDownstreamOf(nodeId))
                {
                    queue.Enqueue(next.Id);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Workflow {WorkflowId} node {NodeId} ({NodeType}) failed.", workflow.Id, nodeId, node.Type);
                stepLog.Add(new { nodeId, type = node.Type, status = "Failed", error = ex.Message, startedAt, finishedAt = DateTimeOffset.UtcNow });
                failed = true;
            }
        }

        run.StepLogJson = JsonSerializer.Serialize(stepLog);
        run.Status = failed ? WorkflowRunStatus.Failed : WorkflowRunStatus.Succeeded;
        run.ErrorMessage = failed ? "One or more nodes failed; see step log." : null;
        run.FinishedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return run.Id;
    }
}
