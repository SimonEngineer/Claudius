using AngleSharp;
using AngleSharp.Dom;
using Microsoft.Extensions.Logging;
using Weaver.Domain;
using Weaver.Infrastructure.Security;
using Weaver.Scraping.Fetching;

namespace Weaver.Scraping;

public class ScraperEngine
{
    private readonly IPageFetcherFactory _fetcherFactory;
    private readonly RateLimitGate _rateLimitGate;
    private readonly ISensitiveConfigProtector _protector;
    private readonly IBrowsingContext _browsingContext;
    private readonly ILogger<ScraperEngine> _logger;

    public ScraperEngine(IPageFetcherFactory fetcherFactory, RateLimitGate rateLimitGate, ISensitiveConfigProtector protector, ILogger<ScraperEngine> logger)
    {
        _fetcherFactory = fetcherFactory;
        _rateLimitGate = rateLimitGate;
        _protector = protector;
        _logger = logger;
        _browsingContext = BrowsingContext.New(Configuration.Default);
    }

    public async Task<ScrapeResult> RunAsync(ScrapingProject project, CancellationToken cancellationToken = default)
    {
        var fetcher = _fetcherFactory.GetFetcher(project.RenderMode);
        var customHeaders = CustomHeadersParser.Parse(project.CustomHeadersJson);
        if (!string.IsNullOrWhiteSpace(project.UserAgent))
        {
            // Both fetchers honor a User-Agent custom header (Playwright maps it onto the browser
            // context), so the override rides the existing per-request header channel.
            customHeaders["User-Agent"] = project.UserAgent.Trim();
        }

        var proxy = ResolveProxy(project.ProxyConfigJson);
        var items = new List<ExtractedItem>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var robotsByHost = new Dictionary<string, RobotsRules>(StringComparer.OrdinalIgnoreCase);
        var pagesCrawled = 0;

        var seeds = ResolveSeedUrls(project);
        await AppendSitemapSeedsAsync(project, seeds, customHeaders, proxy, cancellationToken);

        foreach (var seedUrl in seeds)
        {
            var currentUrl = seedUrl;

            // Each seed gets its own pagination budget; the shared visited set stops seeds whose
            // pagination converges on the same pages from re-scraping (and double-counting) them.
            for (var page = 1; page <= Math.Max(1, project.MaxPages); page++)
            {
                if (string.IsNullOrWhiteSpace(currentUrl) || !visited.Add(currentUrl))
                {
                    break;
                }

                var targetUri = new Uri(currentUrl);

                if (project.RespectRobotsTxt)
                {
                    // Always fetch robots.txt over plain HTTP -- rendering it in a browser would
                    // wrap the plain-text body in an HTML shell and break parsing.
                    var robotsFetcher = _fetcherFactory.GetFetcher(RenderMode.Http);
                    var robots = await GetRobotsRulesAsync(targetUri, robotsFetcher, customHeaders, proxy, robotsByHost, cancellationToken);
                    if (!robots.IsAllowed(targetUri.PathAndQuery))
                    {
                        _logger.LogInformation("Skipping {Url}: disallowed by robots.txt", currentUrl);
                        break;
                    }
                }

                if (project.CrawlDelayMs > 0 && pagesCrawled > 0)
                {
                    // Politeness pause between successive fetches, on top of any rate limit policy.
                    await Task.Delay(project.CrawlDelayMs, cancellationToken);
                }

                await _rateLimitGate.WaitForSlotAsync(project.RateLimitPolicy, project.Id, targetUri, cancellationToken);

                _logger.LogInformation("Scraping {Url} (page {Page}) for project {ProjectId}", currentUrl, page, project.Id);

                FetchedPage fetched;
                try
                {
                    fetched = await fetcher.FetchAsync(currentUrl, customHeaders, proxy, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // A cancelled run must surface as Cancelled, not as a "failed to fetch" error.
                    throw;
                }
                catch (Exception ex)
                {
                    return new ScrapeResult(pagesCrawled, items, $"Failed to fetch {currentUrl}: {ex.Message}");
                }

                if (fetched.StatusCode is < 200 or >= 300)
                {
                    return new ScrapeResult(pagesCrawled, items, $"Received HTTP {fetched.StatusCode} from {currentUrl}");
                }

                using var document = await _browsingContext.OpenAsync(req => req.Content(fetched.Html).Address(currentUrl), cancellationToken);
                pagesCrawled++;

                try
                {
                    items.AddRange(ExtractItemsFromPage(document, project, targetUri));
                }
                catch (FieldExtractionException ex)
                {
                    return new ScrapeResult(pagesCrawled, items, ex.Message);
                }

                currentUrl = ResolveNextPageUrl(document, project, targetUri, page);
            }
        }

        return new ScrapeResult(pagesCrawled, items, null);
    }

    /// <summary>StartUrl plus any extra seeds from StartUrlsJson, de-duplicated, invalid entries dropped.</summary>
    internal static List<string> ResolveSeedUrls(ScrapingProject project)
    {
        var seeds = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? url)
        {
            if (!string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out _) && seen.Add(url))
            {
                seeds.Add(url);
            }
        }

        Add(project.StartUrl);
        try
        {
            var extras = System.Text.Json.JsonSerializer.Deserialize<List<string>>(project.StartUrlsJson ?? "[]");
            foreach (var url in extras ?? new List<string>())
            {
                Add(url);
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Malformed extra-seeds JSON: fall back to StartUrl only.
        }

        return seeds;
    }

    /// <summary>Loads the project's sitemap (and one level of child sitemaps) and appends its URLs
    /// as extra seeds, capped at MaxPages total appended so a huge sitemap can't explode a run.
    /// Sitemap fetch failures just leave the explicit seeds in place.</summary>
    private async Task AppendSitemapSeedsAsync(
        ScrapingProject project,
        List<string> seeds,
        IReadOnlyDictionary<string, string> customHeaders,
        ProxyConfig? proxy,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(project.SitemapUrl) || !Uri.TryCreate(project.SitemapUrl, UriKind.Absolute, out _))
        {
            return;
        }

        var cap = Math.Max(1, project.MaxPages);
        var seen = new HashSet<string>(seeds, StringComparer.OrdinalIgnoreCase);
        var httpFetcher = _fetcherFactory.GetFetcher(RenderMode.Http);
        var added = 0;

        async Task<SitemapParser.SitemapResult?> FetchAsync(string url)
        {
            try
            {
                var fetched = await httpFetcher.FetchAsync(url, customHeaders, proxy, cancellationToken);
                return fetched.StatusCode is >= 200 and < 300 ? SitemapParser.Parse(fetched.Html) : null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not fetch sitemap {Url}: {Error}", url, ex.Message);
                return null;
            }
        }

        var root = await FetchAsync(project.SitemapUrl.Trim());
        if (root is null)
        {
            return;
        }

        void Append(IEnumerable<string> urls)
        {
            foreach (var url in urls)
            {
                if (added >= cap)
                {
                    return;
                }

                if (seen.Add(url))
                {
                    seeds.Add(url);
                    added++;
                }
            }
        }

        Append(root.PageUrls);
        foreach (var childUrl in root.ChildSitemapUrls)
        {
            if (added >= cap)
            {
                break;
            }

            var child = await FetchAsync(childUrl);
            if (child is not null)
            {
                Append(child.PageUrls);
            }
        }

        _logger.LogInformation("Sitemap contributed {Count} seed URL(s) for project {ProjectId}", added, project.Id);
    }

    /// <summary>Fetches and caches robots.txt per host (per run). Any failure to fetch or a non-2xx
    /// response is treated as "no restrictions" -- same as most crawlers treat a missing file.</summary>
    private async Task<RobotsRules> GetRobotsRulesAsync(
        Uri target,
        IPageFetcher fetcher,
        IReadOnlyDictionary<string, string> customHeaders,
        ProxyConfig? proxy,
        Dictionary<string, RobotsRules> cache,
        CancellationToken cancellationToken)
    {
        var hostKey = $"{target.Scheme}://{target.Authority}";
        if (cache.TryGetValue(hostKey, out var cached))
        {
            return cached;
        }

        RobotsRules rules;
        try
        {
            var fetched = await fetcher.FetchAsync($"{hostKey}/robots.txt", customHeaders, proxy, cancellationToken);
            var userAgentToken = customHeaders.TryGetValue("User-Agent", out var ua) && !string.IsNullOrWhiteSpace(ua)
                ? ua
                : "WeaverScraper";
            rules = fetched.StatusCode is >= 200 and < 300
                ? RobotsTxtParser.Parse(fetched.Html, userAgentToken)
                : RobotsRules.AllowAll;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not fetch robots.txt for {Host}: {Error}", hostKey, ex.Message);
            rules = RobotsRules.AllowAll;
        }

        cache[hostKey] = rules;
        return rules;
    }

    private ProxyConfig? ResolveProxy(string? proxyConfigJson)
    {
        if (string.IsNullOrWhiteSpace(proxyConfigJson))
        {
            return null;
        }

        var decrypted = _protector.DecryptForUse("scrapingProject.proxy", proxyConfigJson);
        var config = ProxyConfigParser.Parse(decrypted);
        return config.Enabled ? config : null;
    }

    internal static IEnumerable<ExtractedItem> ExtractItemsFromPage(IDocument document, ScrapingProject project, Uri pageUrl)
    {
        if (project.Mode == ScrapeMode.SingleItem)
        {
            var data = SelectorExtractor.ExtractFields(document, project.Fields, pageUrl);
            yield return new ExtractedItem(pageUrl.ToString(), data);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(project.ItemSelector))
        {
            yield break;
        }

        foreach (var container in document.QuerySelectorAll(project.ItemSelector))
        {
            var data = SelectorExtractor.ExtractFields(container, project.Fields, pageUrl);
            yield return new ExtractedItem(pageUrl.ToString(), data);
        }
    }

    private static string? ResolveNextPageUrl(IDocument document, ScrapingProject project, Uri currentPageUrl, int currentPageNumber)
    {
        switch (project.PaginationStrategy)
        {
            case PaginationStrategy.NextLinkSelector:
                if (string.IsNullOrWhiteSpace(project.NextPageSelector))
                {
                    return null;
                }

                var nextLink = document.QuerySelector(project.NextPageSelector);
                var href = nextLink?.GetAttribute("href");
                if (string.IsNullOrWhiteSpace(href))
                {
                    return null;
                }

                return Uri.TryCreate(currentPageUrl, href, out var resolved) ? resolved.ToString() : null;

            case PaginationStrategy.UrlPattern:
                if (string.IsNullOrWhiteSpace(project.PageUrlTemplate))
                {
                    return null;
                }

                return project.PageUrlTemplate.Replace("{page}", (currentPageNumber + 1).ToString());

            default:
                return null;
        }
    }
}
