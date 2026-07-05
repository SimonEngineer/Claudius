using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Weaver.Domain;
using Weaver.Infrastructure.Persistence;
using Weaver.Infrastructure.Security;
using Weaver.Workflows;

namespace Weaver.Api.Controllers;

/// <summary>
/// Generic inbound endpoint for "trigger.http" nodes. Config: { "secret": "...", "hmacSignature":
/// bool }. With hmacSignature false (the default), "secret" is compared directly against the
/// X-Weaver-Secret header. With it true, "secret" is instead used as an HMAC-SHA256 key: the
/// sender signs the raw request body and presents it via X-Weaver-Signature as "sha256=&lt;hex&gt;"
/// (GitHub/Stripe-style) -- proving they hold the secret without ever putting it on the wire.
/// </summary>
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
    public async Task<IActionResult> Invoke(Guid workflowId, Guid nodeId, CancellationToken ct)
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

        // No [FromBody] parameter here on purpose: HMAC verification needs the exact bytes as sent,
        // and re-serializing an already-bound JsonNode wouldn't reliably reproduce them byte-for-byte.
        using var bodyStream = new MemoryStream();
        await Request.Body.CopyToAsync(bodyStream, ct);
        var rawBody = bodyStream.ToArray();

        var decryptedConfigJson = string.IsNullOrWhiteSpace(node.ConfigJson) ? null : _protector.DecryptForUse(node.Type, node.ConfigJson);
        var config = decryptedConfigJson is null ? null : JsonNode.Parse(decryptedConfigJson);
        var expectedSecret = config?["secret"]?.GetValue<string>();
        var useHmacSignature = config?["hmacSignature"]?.GetValue<bool>() ?? false;
        var ipAllowlist = config?["ipAllowlist"]?.AsArray().Select(n => n?.GetValue<string>() ?? string.Empty).ToList();

        if (!IpAllowlist.IsAllowed(HttpContext.Connection.RemoteIpAddress, ipAllowlist))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!string.IsNullOrEmpty(expectedSecret))
        {
            if (useHmacSignature)
            {
                var providedSignature = Request.Headers["X-Weaver-Signature"].FirstOrDefault();
                if (!VerifyHmacSignature(rawBody, expectedSecret, providedSignature))
                {
                    return Unauthorized();
                }
            }
            else
            {
                var providedSecret = Request.Headers["X-Weaver-Secret"].FirstOrDefault();
                if (!string.Equals(providedSecret, expectedSecret, StringComparison.Ordinal))
                {
                    return Unauthorized();
                }
            }
        }

        JsonNode? body = null;
        if (rawBody.Length > 0)
        {
            try
            {
                body = JsonNode.Parse(rawBody);
            }
            catch (System.Text.Json.JsonException)
            {
                return BadRequest("Request body must be valid JSON (or empty).");
            }
        }

        var runId = await _engine.StartRunFromNodeAsync(workflowId, nodeId, TriggerKind.Http, body, ct);

        // responseMode "lastNode" turns the webhook synchronous: the engine has already executed
        // the whole run inline by this point, so we can hand the caller the terminal output --
        // the pattern for "call this workflow like an API".
        var responseMode = config?["responseMode"]?.GetValue<string>() ?? "async";
        if (!string.Equals(responseMode, "lastNode", StringComparison.OrdinalIgnoreCase))
        {
            return Accepted(new { runId });
        }

        var run = await _db.WorkflowRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, ct);
        var lastNodeRun = await _db.NodeRuns.AsNoTracking()
            .Where(n => n.WorkflowRunId == runId && n.CompletedAt != null)
            .OrderByDescending(n => n.CompletedAt)
            .FirstOrDefaultAsync(ct);

        JsonNode? output = null;
        if (lastNodeRun?.OutputJson is { } outputJson && !string.IsNullOrWhiteSpace(outputJson))
        {
            try
            {
                output = JsonNode.Parse(outputJson);
            }
            catch (System.Text.Json.JsonException)
            {
                // Leave output null; status still tells the caller what happened.
            }
        }

        var status = run?.Status.ToString() ?? "Unknown";
        var payload = new JsonObject
        {
            ["runId"] = runId,
            ["status"] = status,
            ["error"] = run?.ErrorMessage,
            ["output"] = output,
        };

        return run?.Status == RunStatus.Succeeded ? Ok(payload) : StatusCode(StatusCodes.Status502BadGateway, payload);
    }

    private static bool VerifyHmacSignature(byte[] rawBody, string secret, string? providedHeader)
    {
        if (string.IsNullOrEmpty(providedHeader))
        {
            return false;
        }

        var providedHex = providedHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
            ? providedHeader["sha256=".Length..]
            : providedHeader;

        var computed = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), rawBody);
        byte[] provided;
        try
        {
            provided = Convert.FromHexString(providedHex);
        }
        catch (FormatException)
        {
            return false;
        }

        return provided.Length == computed.Length && CryptographicOperations.FixedTimeEquals(computed, provided);
    }
}
