using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Weaver.Domain;
using Weaver.Infrastructure.Persistence;
using Weaver.Infrastructure.Security;
using Weaver.Workflows;

namespace Weaver.Api.Controllers;

/// <summary>Generic inbound endpoint for "trigger.http" nodes -- config: { "secret": "..." } (optional) checked against the X-Weaver-Secret header.</summary>
[ApiController]
[Route("api/webhooks")]
[AllowAnonymous]
public class WebhooksController : ControllerBase
{
    private readonly WeaverDbContext _db;
    private readonly IWorkflowExecutionEngine _engine;
    private readonly ISensitiveConfigProtector _protector;

    public WebhooksController(WeaverDbContext db, IWorkflowExecutionEngine engine, ISensitiveConfigProtector protector)
    {
        _db = db;
        _engine = engine;
        _protector = protector;
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

        var decryptedConfigJson = string.IsNullOrWhiteSpace(node.ConfigJson) ? null : _protector.DecryptForUse(node.Type, node.ConfigJson);
        var config = decryptedConfigJson is null ? null : JsonNode.Parse(decryptedConfigJson);
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
