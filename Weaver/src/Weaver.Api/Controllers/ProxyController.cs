using Microsoft.AspNetCore.Mvc;
using Weaver.Scraping.Proxy;

namespace Weaver.Api.Controllers;

/// <summary>Backs the scraping project builder's "browse the target page" panel: fetches server-side and returns an instrumented copy the frontend can safely iframe (see PageProxyService for why the direct site can't just be framed).</summary>
[ApiController]
[Route("api/proxy")]
public class ProxyController : ControllerBase
{
    private readonly PageProxyService _proxyService;

    public ProxyController(PageProxyService proxyService)
    {
        _proxyService = proxyService;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return BadRequest("A valid absolute http(s) url query parameter is required.");
        }

        var page = await _proxyService.LoadForPickingAsync(url, ct);
        return Content(page.Html, "text/html");
    }
}
