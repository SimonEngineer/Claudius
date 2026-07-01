using AngleSharp;
using Weaver.Domain;
using Weaver.Scraping.Fetching;

namespace Weaver.Scraping;

public record TestExtractionResult(int ItemsFound, IReadOnlyList<Dictionary<string, string?>> Items, string? ErrorMessage);

/// <summary>
/// Runs the extraction logic for a single page, without persisting anything, so the project
/// builder UI can show "here's what these selectors would produce" while the user is still
/// iterating -- before committing to a real (rate-limited, queued, persisted) scrape run.
/// </summary>
public class TestExtractionService
{
    private const int MaxPreviewItems = 20;

    private readonly IPageFetcherFactory _fetcherFactory;
    private readonly RateLimitGate _rateLimitGate;

    public TestExtractionService(IPageFetcherFactory fetcherFactory, RateLimitGate rateLimitGate)
    {
        _fetcherFactory = fetcherFactory;
        _rateLimitGate = rateLimitGate;
    }

    public async Task<TestExtractionResult> ExtractAsync(
        string url,
        ScrapeMode mode,
        string? itemSelector,
        IReadOnlyList<FieldSelector> fields,
        RateLimitPolicy? policy,
        Guid scrapingProjectId,
        RenderMode renderMode = RenderMode.Http,
        CancellationToken cancellationToken = default)
    {
        var targetUri = new Uri(url);
        await _rateLimitGate.WaitForSlotAsync(policy ?? RateLimitGate.DefaultInteractivePolicy, scrapingProjectId, targetUri, cancellationToken);

        Fetching.FetchedPage fetched;
        try
        {
            fetched = await _fetcherFactory.GetFetcher(renderMode).FetchAsync(url, cancellationToken);
        }
        catch (Exception ex)
        {
            return new TestExtractionResult(0, [], $"Failed to fetch {url}: {ex.Message}");
        }

        if (fetched.StatusCode is < 200 or >= 300)
        {
            return new TestExtractionResult(0, [], $"Received HTTP {fetched.StatusCode} from {url}");
        }

        using var browsingContext = BrowsingContext.New(Configuration.Default);
        using var document = await browsingContext.OpenAsync(req => req.Content(fetched.Html).Address(url), cancellationToken);

        var project = new ScrapingProject
        {
            Id = scrapingProjectId,
            Mode = mode,
            ItemSelector = itemSelector,
            Fields = fields.ToList(),
        };

        try
        {
            var items = ScraperEngine.ExtractItemsFromPage(document, project, targetUri)
                .Take(MaxPreviewItems)
                .Select(i => i.Data)
                .ToList();
            return new TestExtractionResult(items.Count, items, null);
        }
        catch (FieldExtractionException ex)
        {
            return new TestExtractionResult(0, [], ex.Message);
        }
    }
}
