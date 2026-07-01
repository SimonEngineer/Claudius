using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Weaver.Domain;
using Weaver.Infrastructure.Persistence;
using Weaver.Workflows;

namespace Weaver.Api.Controllers;

/// <summary>Generic inbound endpoint for "trigger.http" nodes -- config: { "secret": "..." } (optional) checked against the X-Weaver-Secret header.</summary>
[ApiController]
[Route("api/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly WeaverDbContext _db;
    private readonly IWorkflowExecutionEngine _engine;

    public WebhooksController(WeaverDbContext db, IWorkflowExecutionEngine engine)
    {
        _db = db;
        _engine = engine;
    }

    [HttpPost("{workflowId:guid}/{nodeId:guid}")]
    public async Task<IActionResult> Invoke(Guid workflowId, Guid nodeId, [FromBody] JsonNode? body, CancellationToken ct)
    {
        var workflow = await _db.Workflows.FirstOrDefaultAsync(w => w.Id == workflowId, ct);
        if (workflow is null || !workflow.IsEnabled)
        {
            return NotFound();
        }

        var node = await _db.WorkflowNodes.FirstOrDefaultAsync(n => n.Id == nodeId && n.WorkflowId == workflowId, ct);
        if (node is null || node.Type != "trigger.http")
        {
            return NotFound();
        }

        var config = string.IsNullOrWhiteSpace(node.ConfigJson) ? null : JsonNode.Parse(node.ConfigJson);
        var expectedSecret = config?["secret"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(expectedSecret))
        {
            var providedSecret = Request.Headers["X-Weaver-Secret"].FirstOrDefault();
            if (!string.Equals(providedSecret, expectedSecret, StringComparison.Ordinal))
            {
                return Unauthorized();
            }
        }

        var runId = await _engine.StartRunFromNodeAsync(workflowId, nodeId, TriggerKind.Http, body, ct);
        return Accepted(new { runId });
    }
}
