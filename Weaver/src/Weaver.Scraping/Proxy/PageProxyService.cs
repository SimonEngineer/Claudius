using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace Weaver.Scraping.Proxy;

public record ProxiedPage(string Html, int StatusCode);

/// <summary>
/// Fetches a target page server-side and rewrites it so it can be safely framed and instrumented
/// by our own picker UI: a &lt;base&gt; tag is added so relative links/images still resolve against
/// the original site, any CSP/X-Frame-Options the page itself declares (via &lt;meta&gt;) is stripped
/// (the HTTP-header versions are simply never forwarded by the controller), and the click-to-select
/// overlay script is appended.
/// </summary>
public class PageProxyService
{
    private readonly HttpClient _httpClient;

    public PageProxyService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "WeaverScraper/1.0 (+https://github.com/weaver)");
        }
    }

    public async Task<ProxiedPage> LoadForPickingAsync(string url, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        var config = Configuration.Default;
        using var context = BrowsingContext.New(config);
        using var document = await context.OpenAsync(req => req.Content(html).Address(url), cancellationToken);

        StripFramingRestrictions(document);
        EnsureBaseHref(document, url);
        InjectOverlay(document);

        return new ProxiedPage(document.DocumentElement.OuterHtml, (int)response.StatusCode);
    }

    private static void StripFramingRestrictions(IDocument document)
    {
        foreach (var meta in document.QuerySelectorAll("meta[http-equiv]").ToList())
        {
            var httpEquiv = meta.GetAttribute("http-equiv") ?? string.Empty;
            if (httpEquiv.Equals("Content-Security-Policy", StringComparison.OrdinalIgnoreCase)
                || httpEquiv.Equals("X-Frame-Options", StringComparison.OrdinalIgnoreCase))
            {
                meta.Remove();
            }
        }
    }

    private static void EnsureBaseHref(IDocument document, string pageUrl)
    {
        var head = document.Head ?? document.CreateElement("head");
        if (document.Head is null && document.DocumentElement is not null)
        {
            document.DocumentElement.Prepend(head);
        }

        var existingBase = document.QuerySelector("base") as IHtmlBaseElement;
        if (existingBase is not null)
        {
            existingBase.Remove();
        }

        var baseTag = document.CreateElement("base");
        baseTag.SetAttribute("href", pageUrl);
        head.Prepend(baseTag);
    }

    private static void InjectOverlay(IDocument document)
    {
        var script = document.CreateElement("script");
        script.TextContent = OverlayScript.Source;
        (document.Body ?? document.DocumentElement).AppendChild(script);
    }
}
