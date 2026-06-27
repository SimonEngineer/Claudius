using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Api.Dtos;
using Orchestrator.Domain;
using Orchestrator.Infrastructure.Persistence;
using Orchestrator.Infrastructure.Workflows;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/workflows")]
public class WorkflowsController(OrchestratorDbContext db, IWorkflowEngine engine, WorkflowCronSync cronSync) : ControllerBase
{
    /// <summary>Static catalog of starter blocks the GUI builder renders as a node palette, plus
    /// the field schema it needs to render each node's config form generically.</summary>
    [HttpGet("node-types")]
    public ActionResult<IEnumerable<NodeTypeDescriptor>> NodeTypes() => Ok(NodeTypeCatalog.All);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Workflow>>> List(CancellationToken ct)
    {
        var workflows = await db.Workflows.OrderBy(w => w.Name).ToListAsync(ct);
        return Ok(workflows);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Workflow>> Get(Guid id, CancellationToken ct)
    {
        var workflow = await db.Workflows.FindAsync([id], ct);
        return workflow is null ? NotFound() : Ok(workflow);
    }

    [HttpPost]
    public async Task<ActionResult<Workflow>> Create(SaveWorkflowRequest request, CancellationToken ct)
    {
        var workflow = new Workflow
        {
            Name = request.Name,
            Description = request.Description,
            DefinitionJson = request.DefinitionJson,
            IsEnabled = request.IsEnabled
        };
        db.Workflows.Add(workflow);
        await db.SaveChangesAsync(ct);
        await cronSync.SyncAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = workflow.Id }, workflow);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SaveWorkflowRequest request, CancellationToken ct)
    {
        var workflow = await db.Workflows.FindAsync([id], ct);
        if (workflow is null)
        {
            return NotFound();
        }

        workflow.Name = request.Name;
        workflow.Description = request.Description;
        workflow.DefinitionJson = request.DefinitionJson;
        workflow.IsEnabled = request.IsEnabled;
        workflow.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await cronSync.SyncAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var workflow = await db.Workflows.FindAsync([id], ct);
        if (workflow is null)
        {
            return NotFound();
        }

        db.Workflows.Remove(workflow);
        await db.SaveChangesAsync(ct);
        await cronSync.SyncAsync(ct);
        return NoContent();
    }

    /// <summary>Runs the workflow immediately with an optional sample payload -- the GUI builder's
    /// "run now" / test button, and also usable as a plain manual trigger.</summary>
    [HttpPost("{id:guid}/run")]
    public async Task<ActionResult<WorkflowRun>> Run(Guid id, RunWorkflowRequest request, CancellationToken ct)
    {
        var exists = await db.Workflows.AnyAsync(w => w.Id == id, ct);
        if (!exists)
        {
            return NotFound();
        }

        var runId = await engine.ExecuteWorkflowAsync(id, request.PayloadJson ?? "{}", ct);
        var run = await db.WorkflowRuns.FindAsync([runId], ct);
        return Ok(run);
    }

    /// <summary>The trigger.http entry point: an inbound POST fires the workflow with the request
    /// body as the trigger payload. Requires a trigger.http node in the definition so a workflow
    /// not meant to be webhook-triggered can't be fired this way by guessing its id.</summary>
    [HttpPost("{id:guid}/webhook")]
    public async Task<IActionResult> Webhook(Guid id, CancellationToken ct)
    {
        var workflow = await db.Workflows.FindAsync([id], ct);
        if (workflow is null || !workflow.IsEnabled)
        {
            return NotFound();
        }

        var definition = WorkflowDefinition.Parse(workflow.DefinitionJson);
        if (!definition.TriggerNodesOfType("trigger.http").Any())
        {
            return Conflict($"Workflow {id} has no webhook trigger node.");
        }

        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);

        await engine.ExecuteWorkflowAsync(id, string.IsNullOrWhiteSpace(body) ? "{}" : body, ct);
        return Accepted();
    }

    [HttpGet("{id:guid}/runs")]
    public async Task<ActionResult<IEnumerable<WorkflowRun>>> Runs(Guid id, CancellationToken ct)
    {
        var runs = await db.WorkflowRuns
            .Where(r => r.WorkflowId == id)
            .OrderByDescending(r => r.StartedAt)
            .Take(50)
            .ToListAsync(ct);
        return Ok(runs);
    }
}
