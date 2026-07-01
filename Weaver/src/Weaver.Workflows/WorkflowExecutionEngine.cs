using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Weaver.Domain;
using Weaver.Infrastructure.Persistence;

namespace Weaver.Workflows;

public interface IWorkflowExecutionEngine
{
    Task<Guid> StartRunFromNodeAsync(
        Guid workflowId,
        Guid triggerNodeId,
        TriggerKind triggerKind,
        JsonNode? payload,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Walks the workflow graph breadth-first starting from one trigger node. Each node executes at
/// most once per run; a Condition node's Branch result prunes which outgoing edges (matched by
/// SourceHandle) get followed, so the untaken branch's nodes never execute. A failed node routes
/// to any edges tagged "error"; if none exist, that branch stops and the whole run is marked Failed
/// (independent branches from the same trigger keep going).
/// </summary>
public class WorkflowExecutionEngine : IWorkflowExecutionEngine
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly INodeHandlerRegistry _registry;
    private readonly ILogger<WorkflowExecutionEngine> _logger;

    public WorkflowExecutionEngine(IServiceScopeFactory scopeFactory, INodeHandlerRegistry registry, ILogger<WorkflowExecutionEngine> logger)
    {
        _scopeFactory = scopeFactory;
        _registry = registry;
        _logger = logger;
    }

    public async Task<Guid> StartRunFromNodeAsync(
        Guid workflowId,
        Guid triggerNodeId,
        TriggerKind triggerKind,
        JsonNode? payload,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeaverDbContext>();

        var workflow = await db.Workflows
            .Include(w => w.Nodes)
            .Include(w => w.Edges)
            .FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found.");

        var triggerNode = workflow.Nodes.FirstOrDefault(n => n.Id == triggerNodeId)
            ?? throw new InvalidOperationException($"Node {triggerNodeId} not found on workflow {workflowId}.");

        var run = new WorkflowRun
        {
            WorkflowId = workflow.Id,
            Status = RunStatus.Running,
            TriggerKind = triggerKind,
            TriggerNodeType = triggerNode.Type,
            TriggerPayloadJson = payload?.ToJsonString() ?? "null",
            StartedAt = DateTimeOffset.UtcNow,
        };
        db.WorkflowRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        var runFailed = false;

        try
        {
            runFailed = await ExecuteGraphAsync(scope.ServiceProvider, db, workflow, triggerNode, payload, run, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow {WorkflowId} run {RunId} threw unexpectedly", workflowId, run.Id);
            run.ErrorMessage = ex.Message;
            runFailed = true;
        }

        run.Status = runFailed ? RunStatus.Failed : RunStatus.Succeeded;
        run.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return run.Id;
    }

    private async Task<bool> ExecuteGraphAsync(
        IServiceProvider services,
        WeaverDbContext db,
        Workflow workflow,
        WorkflowNode triggerNode,
        JsonNode? triggerPayload,
        WorkflowRun run,
        CancellationToken cancellationToken)
    {
        var executed = new HashSet<Guid>();
        var queue = new Queue<(WorkflowNode Node, JsonNode? Input)>();
        queue.Enqueue((triggerNode, triggerPayload));

        var anyFailure = false;

        while (queue.Count > 0)
        {
            var (node, input) = queue.Dequeue();
            if (!executed.Add(node.Id))
            {
                continue;
            }

            var nodeRun = new NodeRun
            {
                WorkflowRunId = run.Id,
                WorkflowNodeId = node.Id,
                NodeType = node.Type,
                Status = RunStatus.Running,
                InputJson = input?.ToJsonString() ?? "null",
                StartedAt = DateTimeOffset.UtcNow,
            };
            db.NodeRuns.Add(nodeRun);
            await db.SaveChangesAsync(cancellationToken);

            NodeExecutionResult result;
            var logLines = new List<string>();

            if (!_registry.TryResolve(node.Type, out var handler))
            {
                result = NodeExecutionResult.Fail($"No handler registered for node type '{node.Type}'.");
            }
            else if (node.Type.StartsWith("trigger.", StringComparison.OrdinalIgnoreCase) && node.Id == triggerNode.Id)
            {
                // The external trigger already fired; just carry the payload forward as this node's output.
                result = NodeExecutionResult.Ok(input);
            }
            else
            {
                try
                {
                    var config = string.IsNullOrWhiteSpace(node.ConfigJson) ? null : JsonNode.Parse(node.ConfigJson);
                    var context = new NodeExecutionContext
                    {
                        WorkflowRunId = run.Id,
                        WorkflowNodeId = node.Id,
                        NodeType = node.Type,
                        Config = config,
                        Input = input,
                        Services = services,
                        Log = line => logLines.Add(line),
                        CancellationToken = cancellationToken,
                    };
                    result = await handler.ExecuteAsync(context);
                }
                catch (Exception ex)
                {
                    result = NodeExecutionResult.Fail(ex.Message);
                }
            }

            nodeRun.Status = result.Success ? RunStatus.Succeeded : RunStatus.Failed;
            nodeRun.OutputJson = result.Output?.ToJsonString();
            nodeRun.ErrorMessage = result.ErrorMessage;
            nodeRun.LogText = logLines.Count > 0 ? string.Join('\n', logLines) : null;
            nodeRun.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            var outgoing = workflow.Edges.Where(e => e.SourceNodeId == node.Id).ToList();

            if (!result.Success)
            {
                var errorEdges = outgoing.Where(e => string.Equals(e.SourceHandle, "error", StringComparison.OrdinalIgnoreCase)).ToList();
                if (errorEdges.Count == 0)
                {
                    anyFailure = true;
                    continue;
                }

                foreach (var edge in errorEdges)
                {
                    var target = workflow.Nodes.First(n => n.Id == edge.TargetNodeId);
                    queue.Enqueue((target, result.Output));
                }
                continue;
            }

            var matching = outgoing.Where(e =>
                string.IsNullOrEmpty(e.SourceHandle)
                || string.Equals(e.SourceHandle, "success", StringComparison.OrdinalIgnoreCase)
                || (result.Branch is not null && string.Equals(e.SourceHandle, result.Branch, StringComparison.OrdinalIgnoreCase)));

            foreach (var edge in matching)
            {
                var target = workflow.Nodes.FirstOrDefault(n => n.Id == edge.TargetNodeId);
                if (target is not null)
                {
                    queue.Enqueue((target, result.Output));
                }
            }
        }

        return anyFailure;
    }
}
