using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Weaver.Domain;
using Weaver.Infrastructure.Persistence;
using Weaver.Infrastructure.Security;
using Weaver.Scraping;
using Weaver.Scraping.Proxy;

namespace Weaver.Api.Controllers;

/// <summary>Backs the scraping project builder's "browse the target page" panel: fetches server-side and returns an instrumented copy the frontend can safely iframe (see PageProxyService for why the direct site can't just be framed).</summary>
[ApiController]
[Route("api/proxy")]
public class ProxyController : ControllerBase
{
    private readonly PageProxyService _proxyService;
    private readonly WeaverDbContext _db;
    private readonly ISensitiveConfigProtector _protector;

    public ProxyController(PageProxyService proxyService, WeaverDbContext db, ISensitiveConfigProtector protector)
    {
        _proxyService = proxyService;
        _db = db;
        _protector = protector;
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

        // Only a previously-saved project's own headers are available here (there's no request
        // body on a GET the iframe can load from); a brand-new, not-yet-saved project previews
        // without them until the first save.
        Dictionary<string, string>? customHeaders = null;
        ProxyConfig? proxy = null;
        if (scrapingProjectId is not null)
        {
            var project = await _db.ScrapingProjects.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == scrapingProjectId && p.OwnerUserId == userId, ct);
            if (project is not null)
            {
                customHeaders = CustomHeadersParser.Parse(project.CustomHeadersJson);
                var decryptedProxyJson = string.IsNullOrWhiteSpace(project.ProxyConfigJson)
                    ? null
                    : _protector.DecryptForUse("scrapingProject.proxy", project.ProxyConfigJson);
                var parsedProxy = ProxyConfigParser.Parse(decryptedProxyJson);
                proxy = parsedProxy.Enabled ? parsedProxy : null;
            }
        }

        var page = await _proxyService.LoadForPickingAsync(url, policy, scrapingProjectId ?? Guid.Empty, renderMode, customHeaders, proxy, ct);
        return Content(page.Html, "text/html");
    }
}
