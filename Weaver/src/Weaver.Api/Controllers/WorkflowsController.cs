using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Weaver.Api.Dtos;
using Weaver.Domain;
using Weaver.Infrastructure.Auditing;
using Weaver.Infrastructure.Persistence;
using Weaver.Infrastructure.Security;
using Weaver.Workflows;

namespace Weaver.Api.Controllers;

[ApiController]
[Route("api/workflows")]
public class WorkflowsController : ControllerBase
{
    private readonly WeaverDbContext _db;
    private readonly IWorkflowExecutionEngine _engine;
    private readonly INodeHandlerRegistry _registry;
    private readonly ISensitiveConfigProtector _protector;
    private readonly IAuditLogger _auditLogger;

    public WorkflowsController(WeaverDbContext db, IWorkflowExecutionEngine engine, INodeHandlerRegistry registry, ISensitiveConfigProtector protector, IAuditLogger auditLogger)
    {
        _db = db;
        _engine = engine;
        _registry = registry;
        _protector = protector;
        _auditLogger = auditLogger;
    }

    private Guid UserId => User.GetUserId();

    [HttpGet]
    public async Task<ActionResult<List<WorkflowDto>>> List(CancellationToken ct)
    {
        var workflows = await _db.Workflows.Include(w => w.Nodes).Include(w => w.Edges)
            .Where(w => w.OwnerUserId == UserId)
            .OrderByDescending(w => w.UpdatedAt).ToListAsync(ct);

        var workflowIds = workflows.Select(w => w.Id).ToArray();
        var lastRuns = await _db.WorkflowRuns
            .FromSqlInterpolated($@"SELECT DISTINCT ON (""WorkflowId"") * FROM workflow_runs WHERE ""WorkflowId"" = ANY({workflowIds}) ORDER BY ""WorkflowId"", ""CreatedAt"" DESC")
            .ToListAsync(ct);
        var lastRunByWorkflow = lastRuns.ToDictionary(r => r.WorkflowId);

        return workflows.Select(w =>
        {
            var dto = WorkflowDto.FromEntity(w, _protector);
            return lastRunByWorkflow.TryGetValue(w.Id, out var run) ? dto with { LastRunStatus = run.Status, LastRunAt = run.CreatedAt } : dto;
        }).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkflowDto>> Get(Guid id, CancellationToken ct)
    {
        var workflow = await _db.Workflows.Include(w => w.Nodes).Include(w => w.Edges).FirstOrDefaultAsync(w => w.Id == id && w.OwnerUserId == UserId, ct);
        return workflow is null ? NotFound() : WorkflowDto.FromEntity(workflow, _protector);
    }

    [HttpPost]
    public async Task<ActionResult<WorkflowDto>> Create(UpsertWorkflowRequest request, CancellationToken ct)
    {
        var workflow = new Workflow
        {
            OwnerUserId = UserId,
            Name = request.Name,
            Description = request.Description,
            IsEnabled = request.IsEnabled,
            RunRetentionDays = request.RunRetentionDays,
        };
        ApplyGraph(workflow, request);

        _db.Workflows.Add(workflow);
        _auditLogger.Record(UserId, AuditAction.Created, "Workflow", workflow.Id, workflow.Name);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = workflow.Id }, WorkflowDto.FromEntity(workflow, _protector));
    }

    /// <summary>Full-graph replace: the workflow builder always saves nodes+edges together, matching how React Flow hands back its whole canvas state.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkflowDto>> Update(Guid id, UpsertWorkflowRequest request, CancellationToken ct)
    {
        var workflow = await _db.Workflows.Include(w => w.Nodes).Include(w => w.Edges).FirstOrDefaultAsync(w => w.Id == id && w.OwnerUserId == UserId, ct);
        if (workflow is null)
        {
            return NotFound();
        }

        // Snapshot the pre-edit graph so this save can be rolled back from the Revisions panel.
        await CaptureRevisionAsync(workflow, ct);

        workflow.Name = request.Name;
        workflow.Description = request.Description;
        workflow.IsEnabled = request.IsEnabled;
        workflow.RunRetentionDays = request.RunRetentionDays;
        workflow.UpdatedAt = DateTimeOffset.UtcNow;

        _db.WorkflowEdges.RemoveRange(workflow.Edges);
        _db.WorkflowNodes.RemoveRange(workflow.Nodes);
        workflow.Nodes.Clear();
        workflow.Edges.Clear();
        ApplyGraph(workflow, request);

        _auditLogger.Record(UserId, AuditAction.Updated, "Workflow", workflow.Id, workflow.Name);
        await _db.SaveChangesAsync(ct);
        return WorkflowDto.FromEntity(workflow, _protector);
    }

    private const int MaxRevisionsPerWorkflow = 20;

    private async Task CaptureRevisionAsync(Workflow workflow, CancellationToken ct)
    {
        _db.WorkflowRevisions.Add(new WorkflowRevision
        {
            WorkflowId = workflow.Id,
            OwnerUserId = UserId,
            WorkflowName = workflow.Name,
            SnapshotJson = System.Text.Json.JsonSerializer.Serialize(WorkflowRevisionSnapshot.FromEntity(workflow)),
        });

        var excess = await _db.WorkflowRevisions
            .Where(r => r.WorkflowId == workflow.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Skip(MaxRevisionsPerWorkflow - 1)
            .ToListAsync(ct);
        _db.WorkflowRevisions.RemoveRange(excess);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var workflow = await _db.Workflows.FirstOrDefaultAsync(w => w.Id == id && w.OwnerUserId == UserId, ct);
        if (workflow is null)
        {
            return NotFound();
        }

        _db.Workflows.Remove(workflow);
        _auditLogger.Record(UserId, AuditAction.Deleted, "Workflow", workflow.Id, workflow.Name);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Clones a workflow's whole graph under a new id. The clone is always created disabled --
    /// otherwise duplicating a workflow with an active cron/webhook trigger would silently start
    /// a second copy running on the same schedule the moment it's saved.
    /// </summary>
    [HttpPost("{id:guid}/duplicate")]
    public async Task<ActionResult<WorkflowDto>> Duplicate(Guid id, CancellationToken ct)
    {
        var source = await _db.Workflows.Include(w => w.Nodes).Include(w => w.Edges).FirstOrDefaultAsync(w => w.Id == id && w.OwnerUserId == UserId, ct);
        if (source is null)
        {
            return NotFound();
        }

        var copy = new Workflow
        {
            OwnerUserId = UserId,
            Name = $"{source.Name} (Copy)",
            Description = source.Description,
            IsEnabled = false,
        };

        var idMap = new Dictionary<Guid, Guid>();
        foreach (var n in source.Nodes)
        {
            var newId = Guid.NewGuid();
            idMap[n.Id] = newId;
            copy.Nodes.Add(new WorkflowNode
            {
                Id = newId,
                WorkflowId = copy.Id,
                Type = n.Type,
                Name = n.Name,
                ConfigJson = n.ConfigJson,
                IsDisabled = n.IsDisabled,
                MaxRetries = n.MaxRetries,
                RetryDelayMs = n.RetryDelayMs,
                PositionX = n.PositionX,
                PositionY = n.PositionY,
            });
        }
        foreach (var e in source.Edges)
        {
            copy.Edges.Add(new WorkflowEdge
            {
                Id = Guid.NewGuid(),
                WorkflowId = copy.Id,
                SourceNodeId = idMap[e.SourceNodeId],
                SourceHandle = e.SourceHandle,
                TargetNodeId = idMap[e.TargetNodeId],
                TargetHandle = e.TargetHandle,
            });
        }

        _db.Workflows.Add(copy);
        _auditLogger.Record(UserId, AuditAction.Created, "Workflow", copy.Id, copy.Name);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = copy.Id }, WorkflowDto.FromEntity(copy, _protector));
    }

    /// <summary>Downloads a portable JSON snapshot of this workflow's graph -- sensitive config
    /// fields (a Discord webhook URL, a webhook secret) are blanked out rather than decrypted, so
    /// the file is safe to share or commit even though it does mean re-entering those after import.</summary>
    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken ct)
    {
        var workflow = await _db.Workflows.Include(w => w.Nodes).Include(w => w.Edges).FirstOrDefaultAsync(w => w.Id == id && w.OwnerUserId == UserId, ct);
        if (workflow is null)
        {
            return NotFound();
        }

        var export = WorkflowExportDto.FromEntity(workflow, _protector);
        var json = System.Text.Json.JsonSerializer.Serialize(export, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        });
        var fileNameStem = string.Join("-", workflow.Name.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", $"{fileNameStem}.weaver-workflow.json");
    }

    /// <summary>
    /// Creates a brand new workflow from a previously-exported file. Always created disabled (same
    /// reasoning as Duplicate) and with fresh ids throughout -- the file's node Refs only need to be
    /// unique within the file itself, so importing the same file twice, or into a different account,
    /// never collides with anything.
    /// </summary>
    [HttpPost("import")]
    public async Task<ActionResult<WorkflowDto>> Import(WorkflowExportDto request, CancellationToken ct)
    {
        if (request.WeaverExportVersion != WorkflowExportDto.CurrentVersion)
        {
            return BadRequest($"Unsupported export version {request.WeaverExportVersion} (expected {WorkflowExportDto.CurrentVersion}).");
        }

        var workflow = new Workflow
        {
            OwnerUserId = UserId,
            Name = request.Name,
            Description = request.Description,
            IsEnabled = false,
        };

        var refMap = new Dictionary<string, Guid>();
        foreach (var n in request.Nodes)
        {
            var newId = Guid.NewGuid();
            refMap[n.Ref] = newId;
            workflow.Nodes.Add(new WorkflowNode
            {
                Id = newId,
                WorkflowId = workflow.Id,
                Type = n.Type,
                Name = n.Name,
                ConfigJson = _protector.EncryptForStorage(n.Type, n.Config?.ToJsonString() ?? "{}"),
                IsDisabled = n.IsDisabled,
                MaxRetries = n.MaxRetries,
                RetryDelayMs = n.RetryDelayMs <= 0 ? 1000 : n.RetryDelayMs,
                PositionX = n.PositionX,
                PositionY = n.PositionY,
            });
        }

        foreach (var e in request.Edges)
        {
            if (!refMap.TryGetValue(e.SourceRef, out var sourceId) || !refMap.TryGetValue(e.TargetRef, out var targetId))
            {
                return BadRequest($"Edge references an unknown node ref ('{e.SourceRef}' -> '{e.TargetRef}').");
            }

            workflow.Edges.Add(new WorkflowEdge
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflow.Id,
                SourceNodeId = sourceId,
                SourceHandle = e.SourceHandle,
                TargetNodeId = targetId,
                TargetHandle = e.TargetHandle,
            });
        }

        _db.Workflows.Add(workflow);
        _auditLogger.Record(UserId, AuditAction.Created, "Workflow", workflow.Id, workflow.Name);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = workflow.Id }, WorkflowDto.FromEntity(workflow, _protector));
    }

    /// <summary>Manually fires a specific trigger node -- the workflow builder's "Run" button targets one node explicitly since a workflow may have several triggers.</summary>
    [HttpPost("{id:guid}/nodes/{nodeId:guid}/run")]
    public async Task<ActionResult<object>> RunFromNode(Guid id, Guid nodeId, [FromBody] System.Text.Json.Nodes.JsonNode? payload, CancellationToken ct)
    {
        var nodeExists = await _db.WorkflowNodes
            .Join(_db.Workflows.Where(w => w.OwnerUserId == UserId), n => n.WorkflowId, w => w.Id, (n, w) => n)
            .AnyAsync(n => n.Id == nodeId && n.WorkflowId == id, ct);
        if (!nodeExists)
        {
            return NotFound();
        }

        var runId = await _engine.StartRunFromNodeAsync(id, nodeId, TriggerKind.Manual, payload, ct);
        return Accepted(new { runId });
    }

    [HttpGet("{id:guid}/runs")]
    public async Task<ActionResult<List<WorkflowRunDto>>> Runs(Guid id, CancellationToken ct)
    {
        if (!await _db.Workflows.AnyAsync(w => w.Id == id && w.OwnerUserId == UserId, ct))
        {
            return NotFound();
        }

        var runs = await _db.WorkflowRuns.Where(r => r.WorkflowId == id).OrderByDescending(r => r.CreatedAt).Take(50).ToListAsync(ct);
        return runs.Select(WorkflowRunDto.FromEntity).ToList();
    }

    [HttpGet("runs/{runId:guid}")]
    public async Task<ActionResult<WorkflowRunDetailDto>> RunDetail(Guid runId, CancellationToken ct)
    {
        var run = await _db.WorkflowRuns
            .Join(_db.Workflows.Where(w => w.OwnerUserId == UserId), r => r.WorkflowId, w => w.Id, (r, w) => r)
            .FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null)
        {
            return NotFound();
        }

        var nodeRuns = await _db.NodeRuns.Where(n => n.WorkflowRunId == runId).OrderBy(n => n.StartedAt).ToListAsync(ct);
        return new WorkflowRunDetailDto(WorkflowRunDto.FromEntity(run), nodeRuns.Select(NodeRunDto.FromEntity).ToList());
    }

    /// <summary>Flips IsEnabled without a full graph round-trip (the list rows' quick toggle).</summary>
    [HttpPost("{id:guid}/toggle-enabled")]
    public async Task<ActionResult<object>> ToggleEnabled(Guid id, CancellationToken ct)
    {
        var workflow = await _db.Workflows.FirstOrDefaultAsync(w => w.Id == id && w.OwnerUserId == UserId, ct);
        if (workflow is null)
        {
            return NotFound();
        }

        workflow.IsEnabled = !workflow.IsEnabled;
        workflow.UpdatedAt = DateTimeOffset.UtcNow;
        _auditLogger.Record(UserId, AuditAction.Updated, "Workflow", workflow.Id, $"{workflow.Name} ({(workflow.IsEnabled ? "enabled" : "disabled")})");
        await _db.SaveChangesAsync(ct);
        return new { workflow.IsEnabled };
    }

    /// <summary>Deletes every run (and their node runs) for this workflow. Irreversible.</summary>
    [HttpDelete("{id:guid}/runs")]
    public async Task<IActionResult> ClearRunHistory(Guid id, CancellationToken ct)
    {
        var workflow = await _db.Workflows.FirstOrDefaultAsync(w => w.Id == id && w.OwnerUserId == UserId, ct);
        if (workflow is null)
        {
            return NotFound();
        }

        await _db.NodeRuns.Where(n => _db.WorkflowRuns.Any(r => r.Id == n.WorkflowRunId && r.WorkflowId == id)).ExecuteDeleteAsync(ct);
        await _db.WorkflowRuns.Where(r => r.WorkflowId == id).ExecuteDeleteAsync(ct);

        _auditLogger.Record(UserId, AuditAction.Deleted, "WorkflowRunHistory", workflow.Id, workflow.Name);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Starts a fresh run from the same trigger node with the same payload as a past run --
    /// for retrying a failure after fixing the workflow, or reproducing an odd result.</summary>
    [HttpPost("runs/{runId:guid}/replay")]
    public async Task<ActionResult<object>> ReplayRun(Guid runId, CancellationToken ct)
    {
        var run = await _db.WorkflowRuns.AsNoTracking()
            .Join(_db.Workflows.Where(w => w.OwnerUserId == UserId), r => r.WorkflowId, w => w.Id, (r, w) => r)
            .FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null)
        {
            return NotFound();
        }

        if (run.TriggerNodeId is null)
        {
            return Conflict("This run predates replay support (its trigger node wasn't recorded).");
        }

        if (!await _db.WorkflowNodes.AnyAsync(n => n.Id == run.TriggerNodeId && n.WorkflowId == run.WorkflowId, ct))
        {
            return Conflict("The trigger node that started this run no longer exists on the workflow.");
        }

        System.Text.Json.Nodes.JsonNode? payload = null;
        if (!string.IsNullOrWhiteSpace(run.TriggerPayloadJson) && run.TriggerPayloadJson != "null")
        {
            try
            {
                payload = System.Text.Json.Nodes.JsonNode.Parse(run.TriggerPayloadJson);
            }
            catch (System.Text.Json.JsonException)
            {
                // Replay with a null payload rather than refusing outright.
            }
        }

        var newRunId = await _engine.StartRunFromNodeAsync(run.WorkflowId, run.TriggerNodeId.Value, run.TriggerKind, payload, ct);
        return Accepted(new { runId = newRunId });
    }

    [HttpGet("{id:guid}/revisions")]
    public async Task<ActionResult<List<WorkflowRevisionDto>>> Revisions(Guid id, CancellationToken ct)
    {
        if (!await _db.Workflows.AnyAsync(w => w.Id == id && w.OwnerUserId == UserId, ct))
        {
            return NotFound();
        }

        var revisions = await _db.WorkflowRevisions
            .Where(r => r.WorkflowId == id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return revisions.Select(r =>
        {
            int nodeCount;
            try
            {
                nodeCount = System.Text.Json.JsonSerializer.Deserialize<WorkflowRevisionSnapshot>(r.SnapshotJson)?.Nodes.Count ?? 0;
            }
            catch (System.Text.Json.JsonException)
            {
                nodeCount = 0;
            }
            return new WorkflowRevisionDto(r.Id, r.WorkflowName, nodeCount, r.CreatedAt);
        }).ToList();
    }

    /// <summary>Replaces the workflow's current graph with a past revision's. The pre-restore state
    /// is snapshotted first, so a restore is itself undoable.</summary>
    [HttpPost("{id:guid}/revisions/{revisionId:guid}/restore")]
    public async Task<ActionResult<WorkflowDto>> RestoreRevision(Guid id, Guid revisionId, CancellationToken ct)
    {
        var workflow = await _db.Workflows.Include(w => w.Nodes).Include(w => w.Edges)
            .FirstOrDefaultAsync(w => w.Id == id && w.OwnerUserId == UserId, ct);
        if (workflow is null)
        {
            return NotFound();
        }

        var revision = await _db.WorkflowRevisions.FirstOrDefaultAsync(r => r.Id == revisionId && r.WorkflowId == id, ct);
        if (revision is null)
        {
            return NotFound();
        }

        WorkflowRevisionSnapshot? snapshot;
        try
        {
            snapshot = System.Text.Json.JsonSerializer.Deserialize<WorkflowRevisionSnapshot>(revision.SnapshotJson);
        }
        catch (System.Text.Json.JsonException)
        {
            snapshot = null;
        }

        if (snapshot is null)
        {
            return Conflict("This revision's snapshot could not be read.");
        }

        await CaptureRevisionAsync(workflow, ct);

        workflow.Name = snapshot.Name;
        workflow.Description = snapshot.Description;
        workflow.UpdatedAt = DateTimeOffset.UtcNow;

        _db.WorkflowEdges.RemoveRange(workflow.Edges);
        _db.WorkflowNodes.RemoveRange(workflow.Nodes);
        workflow.Nodes.Clear();
        workflow.Edges.Clear();

        var refMap = new Dictionary<string, Guid>();
        foreach (var n in snapshot.Nodes)
        {
            var newId = Guid.NewGuid();
            refMap[n.Ref] = newId;
            var node = new WorkflowNode
            {
                Id = newId,
                WorkflowId = workflow.Id,
                Type = n.Type,
                Name = n.Name,
                ConfigJson = n.ConfigJson,
                IsDisabled = n.IsDisabled,
                MaxRetries = n.MaxRetries,
                RetryDelayMs = n.RetryDelayMs,
                PositionX = n.PositionX,
                PositionY = n.PositionY,
            };
            // Collection first, then DbSet -- same order as ApplyGraph. DbSet.Add first would let
            // EF's relationship fixup insert it into workflow.Nodes, and the manual add after that
            // would duplicate it in the response.
            workflow.Nodes.Add(node);
            _db.WorkflowNodes.Add(node);
        }

        foreach (var e in snapshot.Edges)
        {
            if (!refMap.TryGetValue(e.SourceRef, out var sourceId) || !refMap.TryGetValue(e.TargetRef, out var targetId))
            {
                continue;
            }

            var edge = new WorkflowEdge
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflow.Id,
                SourceNodeId = sourceId,
                SourceHandle = e.SourceHandle,
                TargetNodeId = targetId,
                TargetHandle = e.TargetHandle,
            };
            workflow.Edges.Add(edge);
            _db.WorkflowEdges.Add(edge);
        }

        _auditLogger.Record(UserId, AuditAction.Updated, "Workflow", workflow.Id, $"{workflow.Name} (restored revision)");
        await _db.SaveChangesAsync(ct);
        return WorkflowDto.FromEntity(workflow, _protector);
    }

    /// <summary>Requests cancellation of a running workflow run. Broadcast to every process, so it
    /// works whether the run executes in the Api (manual/webhook) or a Worker (cron).</summary>
    [HttpPost("runs/{runId:guid}/cancel")]
    public async Task<IActionResult> CancelRun(Guid runId, [FromServices] Weaver.Infrastructure.Realtime.IRunCancellationService cancellation, CancellationToken ct)
    {
        var run = await _db.WorkflowRuns
            .Join(_db.Workflows.Where(w => w.OwnerUserId == UserId), r => r.WorkflowId, w => w.Id, (r, w) => r)
            .FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null)
        {
            return NotFound();
        }

        if (run.Status is not (RunStatus.Pending or RunStatus.Running))
        {
            return Conflict($"Run is already {run.Status}.");
        }

        await cancellation.RequestCancelAsync(runId, ct);
        return Accepted();
    }

    [HttpGet("~/api/node-types")]
    public ActionResult<List<string>> NodeTypes() => _registry.RegisteredTypes.OrderBy(t => t).ToList();

    private void ApplyGraph(Workflow workflow, UpsertWorkflowRequest request)
    {
        var idMap = new Dictionary<Guid, Guid>();

        foreach (var n in request.Nodes)
        {
            var node = new WorkflowNode
            {
                Id = n.Id == Guid.Empty ? Guid.NewGuid() : n.Id,
                WorkflowId = workflow.Id,
                Type = n.Type,
                Name = n.Name,
                ConfigJson = _protector.EncryptForStorage(n.Type, n.Config?.ToJsonString() ?? "{}"),
                IsDisabled = n.IsDisabled,
                MaxRetries = n.MaxRetries,
                RetryDelayMs = n.RetryDelayMs <= 0 ? 1000 : n.RetryDelayMs,
                PositionX = n.PositionX,
                PositionY = n.PositionY,
            };
            idMap[n.Id] = node.Id;
            workflow.Nodes.Add(node);
            // A node keeps its id across saves (the workflow builder always resends existing ids),
            // so its Guid key is never the CLR default -- reached only through this already-tracked
            // parent's navigation collection, EF's change tracker would otherwise guess Unchanged
            // instead of Added for a genuinely brand new row. Adding to the DbSet directly forces
            // the correct state regardless of what the key value looks like.
            _db.WorkflowNodes.Add(node);
        }

        foreach (var e in request.Edges)
        {
            var edge = new WorkflowEdge
            {
                Id = e.Id == Guid.Empty ? Guid.NewGuid() : e.Id,
                WorkflowId = workflow.Id,
                SourceNodeId = idMap.GetValueOrDefault(e.SourceNodeId, e.SourceNodeId),
                SourceHandle = e.SourceHandle,
                TargetNodeId = idMap.GetValueOrDefault(e.TargetNodeId, e.TargetNodeId),
                TargetHandle = e.TargetHandle,
            };
            workflow.Edges.Add(edge);
            _db.WorkflowEdges.Add(edge);
        }
    }
}
