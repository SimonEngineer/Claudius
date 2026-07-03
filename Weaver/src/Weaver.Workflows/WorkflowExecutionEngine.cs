using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Weaver.Domain;
using Weaver.Infrastructure.Persistence;
using Weaver.Infrastructure.Realtime;
using Weaver.Infrastructure.Security;

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
/// Walks the workflow graph starting from one trigger node, using a dataflow scheduler rather
/// than a simple queue: a node only runs once every one of its incoming edges has either
/// delivered a value or been pruned (its source ran but took a different branch), so a node with
/// two upstream paths waits for both instead of firing on whichever arrives first. A Condition
/// node's Branch result decides which outgoing edges are "taken" (delivering a value) vs
/// "pruned" (never will); a failed node routes to any "error" edges, or -- if none exist --
/// prunes everything downstream of it and fails the run (independent branches from the same
/// trigger keep going). Every node's output stays addressable by name for the rest of the run
/// (NodeExecutionContext.AllNodeOutputs), not just by its immediate successor.
/// </summary>
public class WorkflowExecutionEngine : IWorkflowExecutionEngine
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly INodeHandlerRegistry _registry;
    private readonly ILogger<WorkflowExecutionEngine> _logger;
    private readonly IRunStatusPublisher _runStatus;

    private readonly IRunCancellationService _cancellation;

    public WorkflowExecutionEngine(
        IServiceScopeFactory scopeFactory,
        INodeHandlerRegistry registry,
        ILogger<WorkflowExecutionEngine> logger,
        IRunStatusPublisher runStatus,
        IRunCancellationService cancellation)
    {
        _scopeFactory = scopeFactory;
        _registry = registry;
        _logger = logger;
        _runStatus = runStatus;
        _cancellation = cancellation;
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
        await _runStatus.PublishAsync(workflow.OwnerUserId, "workflow", run.Id, run.Status.ToString(), cancellationToken);

        var runFailed = false;
        var cancelled = false;

        using var cancelRegistration = _cancellation.Register(run.Id, cancellationToken);
        try
        {
            runFailed = await new GraphExecution(scope.ServiceProvider, db, workflow, _registry, _logger, run, cancelRegistration.Token)
                .RunAsync(triggerNode, payload);
        }
        catch (OperationCanceledException) when (cancelRegistration.Token.IsCancellationRequested)
        {
            cancelled = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow {WorkflowId} run {RunId} threw unexpectedly", workflowId, run.Id);
            run.ErrorMessage = ex.Message;
            runFailed = true;
        }

        run.Status = cancelled ? RunStatus.Cancelled : runFailed ? RunStatus.Failed : RunStatus.Succeeded;
        run.CompletedAt = DateTimeOffset.UtcNow;
        // Save with the outer token, not the run's -- a cancelled run must still persist its final state.
        await db.SaveChangesAsync(CancellationToken.None);
        await _runStatus.PublishAsync(workflow.OwnerUserId, "workflow", run.Id, run.Status.ToString(), CancellationToken.None);

        if (run.Status == RunStatus.Failed)
        {
            var failureNotifier = scope.ServiceProvider.GetRequiredService<IFailureNotifier>();
            await failureNotifier.NotifyWorkflowFailureAsync(workflow.OwnerUserId, workflow.Name, run.Id, run.ErrorMessage, CancellationToken.None);
        }

        return run.Id;
    }

    /// <summary>Scoped to a single run; holds all the dataflow bookkeeping so the engine itself stays stateless.</summary>
    private class GraphExecution
    {
        private readonly IServiceProvider _services;
        private readonly WeaverDbContext _db;
        private readonly Workflow _workflow;
        private readonly INodeHandlerRegistry _registry;
        private readonly ILogger _logger;
        private readonly WorkflowRun _run;
        private readonly CancellationToken _cancellationToken;

        private readonly Dictionary<Guid, WorkflowNode> _nodesById;
        private readonly Dictionary<Guid, List<WorkflowEdge>> _incomingEdges;
        private readonly Dictionary<Guid, Dictionary<Guid, JsonNode?>> _receivedBySourceNode = new();
        private readonly HashSet<Guid> _prunedEdgeIds = new();
        private readonly HashSet<Guid> _resolvedNodeIds = new();
        private readonly Dictionary<string, JsonNode?> _allNodeOutputs = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _secrets = new(StringComparer.OrdinalIgnoreCase);
        private bool _anyFailure;

        public GraphExecution(IServiceProvider services, WeaverDbContext db, Workflow workflow, INodeHandlerRegistry registry, ILogger logger, WorkflowRun run, CancellationToken cancellationToken)
        {
            _services = services;
            _db = db;
            _workflow = workflow;
            _registry = registry;
            _logger = logger;
            _run = run;
            _cancellationToken = cancellationToken;
            _nodesById = workflow.Nodes.ToDictionary(n => n.Id);
            _incomingEdges = workflow.Edges.GroupBy(e => e.TargetNodeId).ToDictionary(g => g.Key, g => g.ToList());
        }

        public async Task<bool> RunAsync(WorkflowNode triggerNode, JsonNode? triggerPayload)
        {
            // Decrypt the owner's stored credentials once per run, and only when some node config
            // actually references {{secrets.*}} -- most runs never touch the credentials table.
            if (_workflow.Nodes.Any(n => SecretsTemplate.ContainsSecretTokens(n.ConfigJson)))
            {
                var credentialProtector = _services.GetRequiredService<ICredentialProtector>();
                var credentials = await _db.Credentials
                    .Where(c => c.OwnerUserId == _workflow.OwnerUserId)
                    .ToListAsync(_cancellationToken);
                foreach (var credential in credentials)
                {
                    if (credentialProtector.Decrypt(credential.EncryptedValue) is { } value)
                    {
                        _secrets[credential.Name] = value;
                    }
                }
            }

            var queue = new Queue<Guid>();
            queue.Enqueue(triggerNode.Id);
            var scheduled = new HashSet<Guid> { triggerNode.Id };

            while (queue.Count > 0)
            {
                var nodeId = queue.Dequeue();
                if (!_nodesById.TryGetValue(nodeId, out var node))
                {
                    continue;
                }

                var isTriggerEntry = node.Id == triggerNode.Id;
                var contributions = _receivedBySourceNode.GetValueOrDefault(node.Id, new Dictionary<Guid, JsonNode?>());
                var hadPredecessors = _incomingEdges.GetValueOrDefault(node.Id)?.Count > 0;

                if (!isTriggerEntry && hadPredecessors && contributions.Count == 0)
                {
                    // Every incoming edge was pruned -- this node is unreachable, so it never runs,
                    // and everything downstream of it is unreachable too (propagate the pruning).
                    PropagatePruning(node, queue, scheduled);
                    continue;
                }

                var input = isTriggerEntry ? triggerPayload : MergeContributions(node, contributions);

                var result = await ExecuteWithRetryAsync(node, input, isTriggerEntry);
                _resolvedNodeIds.Add(node.Id);
                if (result.Success)
                {
                    _allNodeOutputs[node.Name] = result.Output;
                }

                DispatchOutgoingEdges(node, result, queue, scheduled);
            }

            return _anyFailure;
        }

        private JsonNode? MergeContributions(WorkflowNode node, Dictionary<Guid, JsonNode?> contributions)
        {
            if (contributions.Count <= 1)
            {
                return contributions.Values.FirstOrDefault();
            }

            // A genuine merge node (2+ predecessors both delivered): key the combined object by
            // each predecessor's Name so downstream templates/conditions can disambiguate, e.g.
            // "{{Check Stock.inStock}}" vs "{{Check Price.current}}".
            var merged = new JsonObject();
            foreach (var (sourceId, output) in contributions)
            {
                var name = _nodesById.TryGetValue(sourceId, out var sourceNode) ? sourceNode.Name : sourceId.ToString();
                merged[name] = output?.DeepClone();
            }

            return merged;
        }

        private async Task<NodeExecutionResult> ExecuteWithRetryAsync(WorkflowNode node, JsonNode? input, bool isTriggerEntry)
        {
            var nodeRun = new NodeRun
            {
                WorkflowRunId = _run.Id,
                WorkflowNodeId = node.Id,
                NodeType = node.Type,
                Status = RunStatus.Running,
                InputJson = input?.ToJsonString() ?? "null",
                StartedAt = DateTimeOffset.UtcNow,
            };
            _db.NodeRuns.Add(nodeRun);
            await _db.SaveChangesAsync(_cancellationToken);

            var logLines = new List<string>();
            NodeExecutionResult result;

            if (!isTriggerEntry && node.IsDisabled)
            {
                // Disabled nodes aren't executed at all -- their input passes straight through as
                // if this node weren't in the graph, and it's neither a success nor a failure.
                nodeRun.Status = RunStatus.Skipped;
                nodeRun.OutputJson = input?.ToJsonString();
                nodeRun.CompletedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(_cancellationToken);
                return NodeExecutionResult.Ok(input);
            }

            if (!_registry.TryResolve(node.Type, out var handler))
            {
                result = NodeExecutionResult.Fail($"No handler registered for node type '{node.Type}'.");
            }
            else if (node.Type.StartsWith("trigger.", StringComparison.OrdinalIgnoreCase))
            {
                // The external trigger already fired; just carry the payload forward as this node's output.
                result = NodeExecutionResult.Ok(input);
            }
            else
            {
                var maxAttempts = Math.Max(1, node.MaxRetries + 1);
                var retryDelay = TimeSpan.FromMilliseconds(Math.Max(0, node.RetryDelayMs));
                var attempt = 0;

                while (true)
                {
                    attempt++;
                    try
                    {
                        var protector = _services.GetRequiredService<ISensitiveConfigProtector>();
                        var decryptedConfigJson = string.IsNullOrWhiteSpace(node.ConfigJson) ? null : protector.DecryptForUse(node.Type, node.ConfigJson);
                        var config = decryptedConfigJson is null ? null : JsonNode.Parse(decryptedConfigJson);
                        config = SecretsTemplate.Resolve(config, name => _secrets.GetValueOrDefault(name));
                        var context = new NodeExecutionContext
                        {
                            WorkflowRunId = _run.Id,
                            WorkflowNodeId = node.Id,
                            NodeType = node.Type,
                            Config = config,
                            Input = input,
                            AllNodeOutputs = _allNodeOutputs,
                            Services = _services,
                            Log = line => logLines.Add(line),
                            CancellationToken = _cancellationToken,
                        };
                        result = await handler.ExecuteAsync(context);
                    }
                    catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
                    {
                        // Run was cancelled mid-node: record this node as Cancelled (not Failed)
                        // and let the cancellation propagate to end the whole run.
                        nodeRun.Status = RunStatus.Cancelled;
                        nodeRun.CompletedAt = DateTimeOffset.UtcNow;
                        nodeRun.LogText = logLines.Count > 0 ? string.Join('\n', logLines) : null;
                        await _db.SaveChangesAsync(CancellationToken.None);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        result = NodeExecutionResult.Fail(ex.Message);
                    }

                    if (result.Success || attempt >= maxAttempts)
                    {
                        break;
                    }

                    logLines.Add($"attempt {attempt}/{maxAttempts} failed: {result.ErrorMessage} -- retrying in {retryDelay.TotalSeconds:0.#}s");
                    await Task.Delay(retryDelay, _cancellationToken);
                }
            }

            nodeRun.Status = result.Success ? RunStatus.Succeeded : RunStatus.Failed;
            nodeRun.OutputJson = result.Output?.ToJsonString();
            nodeRun.ErrorMessage = result.ErrorMessage;
            nodeRun.LogText = logLines.Count > 0 ? string.Join('\n', logLines) : null;
            nodeRun.CompletedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(_cancellationToken);

            return result;
        }

        private void DispatchOutgoingEdges(WorkflowNode node, NodeExecutionResult result, Queue<Guid> queue, HashSet<Guid> scheduled)
        {
            var outgoing = _workflow.Edges.Where(e => e.SourceNodeId == node.Id).ToList();

            HashSet<Guid> takenEdgeIds;
            if (!result.Success)
            {
                var errorEdges = outgoing.Where(e => string.Equals(e.SourceHandle, "error", StringComparison.OrdinalIgnoreCase)).ToList();
                if (errorEdges.Count == 0)
                {
                    _anyFailure = true;
                }
                takenEdgeIds = errorEdges.Select(e => e.Id).ToHashSet();
            }
            else
            {
                takenEdgeIds = outgoing.Where(e =>
                        string.IsNullOrEmpty(e.SourceHandle)
                        || string.Equals(e.SourceHandle, "success", StringComparison.OrdinalIgnoreCase)
                        || (result.Branch is not null && string.Equals(e.SourceHandle, result.Branch, StringComparison.OrdinalIgnoreCase)))
                    .Select(e => e.Id)
                    .ToHashSet();
            }

            foreach (var edge in outgoing)
            {
                if (takenEdgeIds.Contains(edge.Id))
                {
                    var bucket = _receivedBySourceNode.GetValueOrDefault(edge.TargetNodeId) ?? new Dictionary<Guid, JsonNode?>();
                    bucket[node.Id] = result.Output;
                    _receivedBySourceNode[edge.TargetNodeId] = bucket;
                }
                else
                {
                    _prunedEdgeIds.Add(edge.Id);
                }

                TryScheduleIfReady(edge.TargetNodeId, queue, scheduled);
            }
        }

        private void PropagatePruning(WorkflowNode unreachableNode, Queue<Guid> queue, HashSet<Guid> scheduled)
        {
            _resolvedNodeIds.Add(unreachableNode.Id);
            foreach (var edge in _workflow.Edges.Where(e => e.SourceNodeId == unreachableNode.Id))
            {
                _prunedEdgeIds.Add(edge.Id);
                TryScheduleIfReady(edge.TargetNodeId, queue, scheduled);
            }
        }

        private void TryScheduleIfReady(Guid targetNodeId, Queue<Guid> queue, HashSet<Guid> scheduled)
        {
            if (scheduled.Contains(targetNodeId) || _resolvedNodeIds.Contains(targetNodeId) || !_nodesById.ContainsKey(targetNodeId))
            {
                return;
            }

            var incoming = _incomingEdges.GetValueOrDefault(targetNodeId) ?? new List<WorkflowEdge>();
            var received = _receivedBySourceNode.GetValueOrDefault(targetNodeId) ?? new Dictionary<Guid, JsonNode?>();

            var ready = incoming.All(e => _prunedEdgeIds.Contains(e.Id) || received.ContainsKey(e.SourceNodeId));
            if (!ready)
            {
                return;
            }

            scheduled.Add(targetNodeId);
            queue.Enqueue(targetNodeId);
        }
    }
}
