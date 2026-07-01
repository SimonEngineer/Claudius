using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Weaver.Api.Dtos;
using Weaver.Domain;
using Weaver.Infrastructure.Persistence;
using Weaver.Workflows;

namespace Weaver.Api.Controllers;

[ApiController]
[Route("api/workflows")]
public class WorkflowsController : ControllerBase
{
    private readonly WeaverDbContext _db;
    private readonly IWorkflowExecutionEngine _engine;
    private readonly INodeHandlerRegistry _registry;

    public WorkflowsController(WeaverDbContext db, IWorkflowExecutionEngine engine, INodeHandlerRegistry registry)
    {
        _db = db;
        _engine = engine;
        _registry = registry;
    }

    private Guid UserId => User.GetUserId();

    [HttpGet]
    public async Task<ActionResult<List<WorkflowDto>>> List(CancellationToken ct)
    {
        var workflows = await _db.Workflows.Include(w => w.Nodes).Include(w => w.Edges)
            .Where(w => w.OwnerUserId == UserId)
            .OrderByDescending(w => w.UpdatedAt).ToListAsync(ct);
        return workflows.Select(WorkflowDto.FromEntity).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkflowDto>> Get(Guid id, CancellationToken ct)
    {
        var workflow = await _db.Workflows.Include(w => w.Nodes).Include(w => w.Edges).FirstOrDefaultAsync(w => w.Id == id && w.OwnerUserId == UserId, ct);
        return workflow is null ? NotFound() : WorkflowDto.FromEntity(workflow);
    }

    [HttpPost]
    public async Task<ActionResult<WorkflowDto>> Create(UpsertWorkflowRequest request, CancellationToken ct)
    {
        var workflow = new Workflow { OwnerUserId = UserId, Name = request.Name, Description = request.Description, IsEnabled = request.IsEnabled };
        ApplyGraph(workflow, request);

        _db.Workflows.Add(workflow);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = workflow.Id }, WorkflowDto.FromEntity(workflow));
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

        workflow.Name = request.Name;
        workflow.Description = request.Description;
        workflow.IsEnabled = request.IsEnabled;
        workflow.UpdatedAt = DateTimeOffset.UtcNow;

        _db.WorkflowEdges.RemoveRange(workflow.Edges);
        _db.WorkflowNodes.RemoveRange(workflow.Nodes);
        workflow.Nodes.Clear();
        workflow.Edges.Clear();
        ApplyGraph(workflow, request);

        await _db.SaveChangesAsync(ct);
        return WorkflowDto.FromEntity(workflow);
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
        await _db.SaveChangesAsync(ct);
        return NoContent();
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

    [HttpGet("~/api/node-types")]
    public ActionResult<List<string>> NodeTypes() => _registry.RegisteredTypes.OrderBy(t => t).ToList();

    private static void ApplyGraph(Workflow workflow, UpsertWorkflowRequest request)
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
                ConfigJson = n.Config?.ToJsonString() ?? "{}",
                MaxRetries = n.MaxRetries,
                RetryDelayMs = n.RetryDelayMs <= 0 ? 1000 : n.RetryDelayMs,
                PositionX = n.PositionX,
                PositionY = n.PositionY,
            };
            idMap[n.Id] = node.Id;
            workflow.Nodes.Add(node);
        }

        foreach (var e in request.Edges)
        {
            workflow.Edges.Add(new WorkflowEdge
            {
                Id = e.Id == Guid.Empty ? Guid.NewGuid() : e.Id,
                WorkflowId = workflow.Id,
                SourceNodeId = idMap.GetValueOrDefault(e.SourceNodeId, e.SourceNodeId),
                SourceHandle = e.SourceHandle,
                TargetNodeId = idMap.GetValueOrDefault(e.TargetNodeId, e.TargetNodeId),
                TargetHandle = e.TargetHandle,
            });
        }
    }
}
