using AngleSharp;
using AngleSharp.Dom;
using Microsoft.Extensions.Logging;
using Weaver.Domain;
using Weaver.Scraping.Fetching;

namespace Weaver.Scraping;

public class ScraperEngine
{
    private readonly IPageFetcherFactory _fetcherFactory;
    private readonly RateLimitGate _rateLimitGate;
    private readonly IBrowsingContext _browsingContext;
    private readonly ILogger<ScraperEngine> _logger;

    public ScraperEngine(IPageFetcherFactory fetcherFactory, RateLimitGate rateLimitGate, ILogger<ScraperEngine> logger)
    {
        _fetcherFactory = fetcherFactory;
        _rateLimitGate = rateLimitGate;
        _logger = logger;
        _browsingContext = BrowsingContext.New(Configuration.Default);
    }

    public async Task<ScrapeResult> RunAsync(ScrapingProject project, CancellationToken cancellationToken = default)
    {
        var fetcher = _fetcherFactory.GetFetcher(project.RenderMode);
        var items = new List<ExtractedItem>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentUrl = project.StartUrl;
        var pagesCrawled = 0;

        for (var page = 1; page <= Math.Max(1, project.MaxPages); page++)
        {
            if (string.IsNullOrWhiteSpace(currentUrl) || !visited.Add(currentUrl))
            {
                break;
            }

            var targetUri = new Uri(currentUrl);
            await _rateLimitGate.WaitForSlotAsync(project.RateLimitPolicy, project.Id, targetUri, cancellationToken);

            _logger.LogInformation("Scraping {Url} (page {Page}) for project {ProjectId}", currentUrl, page, project.Id);

            FetchedPage fetched;
            try
            {
                fetched = await fetcher.FetchAsync(currentUrl, cancellationToken);
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

        return new ScrapeResult(pagesCrawled, items, null);
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
