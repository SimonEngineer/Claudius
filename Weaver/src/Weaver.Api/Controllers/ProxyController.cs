using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Weaver.Domain;
using Weaver.Infrastructure.Persistence;
using Weaver.Scraping.Proxy;

namespace Weaver.Api.Controllers;

/// <summary>Backs the scraping project builder's "browse the target page" panel: fetches server-side and returns an instrumented copy the frontend can safely iframe (see PageProxyService for why the direct site can't just be framed).</summary>
[ApiController]
[Route("api/proxy")]
public class ProxyController : ControllerBase
{
    private readonly PageProxyService _proxyService;
    private readonly WeaverDbContext _db;

    public ProxyController(PageProxyService proxyService, WeaverDbContext db)
    {
        _proxyService = proxyService;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string url,
        [FromQuery] Guid? rateLimitPolicyId,
        [FromQuery] Guid? scrapingProjectId,
        [FromQuery] RenderMode renderMode,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return BadRequest("A valid absolute http(s) url query parameter is required.");
        }

        var userId = User.GetUserId();
        var policy = rateLimitPolicyId is not null
            ? await _db.RateLimitPolicies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == rateLimitPolicyId && p.OwnerUserId == userId, ct)
            : null;

        var page = await _proxyService.LoadForPickingAsync(url, policy, scrapingProjectId ?? Guid.Empty, renderMode, ct);
        return Content(page.Html, "text/html");
    }
}
