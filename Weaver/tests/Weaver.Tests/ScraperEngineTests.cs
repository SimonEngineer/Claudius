using Microsoft.Extensions.Logging.Abstractions;
using Weaver.Domain;
using Weaver.Infrastructure.RateLimiting;
using Weaver.Scraping;
using Weaver.Scraping.Fetching;
using Xunit;

namespace Weaver.Tests;

public class ScraperEngineTests
{
    private static ScrapingProject Project(Action<ScrapingProject>? configure = null)
    {
        var project = new ScrapingProject
        {
            Name = "Test",
            StartUrl = "https://example.com/page1",
            Mode = ScrapeMode.List,
            ItemSelector = ".item",
            MaxPages = 20,
            Fields = new List<FieldSelector>
            {
                new() { Name = "title", Selector = ".title", Attribute = FieldAttribute.Text, Order = 0 },
            },
        };
        configure?.Invoke(project);
        return project;
    }

    private static ScraperEngine BuildEngine(FakePageFetcher fetcher) =>
        new(new FakePageFetcherFactory(fetcher), new RateLimitGate(new AlwaysAllowRateLimiter()), NullLogger<ScraperEngine>.Instance);

    [Fact]
    public async Task RunAsync_UrlPatternPagination_StopsAtMaxPages()
    {
        var fetcher = new FakePageFetcher(_ => new FetchedPage("", Page("Item"), 200));
        var project = Project(p =>
        {
            p.PaginationStrategy = PaginationStrategy.UrlPattern;
            p.PageUrlTemplate = "https://example.com/page{page}";
            p.MaxPages = 3;
        });

        var result = await BuildEngine(fetcher).RunAsync(project);

        Assert.Null(result.ErrorMessage);
        Assert.Equal(3, result.PagesCrawled);
        Assert.Equal(3, result.Items.Count); // one item per page
        Assert.Equal(new[] { "https://example.com/page1", "https://example.com/page2", "https://example.com/page3" }, fetcher.RequestedUrls);
    }

    [Fact]
    public async Task RunAsync_NextLinkSelector_FollowsUntilNoNextLink()
    {
        var pages = new Dictionary<string, string>
        {
            ["https://example.com/page1"] = Page("A", nextHref: "/page2"),
            ["https://example.com/page2"] = Page("B", nextHref: "/page3"),
            ["https://example.com/page3"] = Page("C", nextHref: null),
        };
        var fetcher = new FakePageFetcher(url => new FetchedPage(url, pages[url], 200));
        var project = Project(p =>
        {
            p.PaginationStrategy = PaginationStrategy.NextLinkSelector;
            p.NextPageSelector = "a.next";
            p.MaxPages = 10;
        });

        var result = await BuildEngine(fetcher).RunAsync(project);

        Assert.Null(result.ErrorMessage);
        Assert.Equal(3, result.PagesCrawled);
        Assert.Equal(3, fetcher.RequestedUrls.Count);
    }

    [Fact]
    public async Task RunAsync_RevisitingSameUrl_StopsToAvoidInfiniteLoop()
    {
        // A next-link that points back at the page it's on would loop forever without the
        // visited-url guard; this proves the guard actually kicks in.
        var fetcher = new FakePageFetcher(_ => new FetchedPage("", Page("A", nextHref: "/page1"), 200));
        var project = Project(p =>
        {
            p.PaginationStrategy = PaginationStrategy.NextLinkSelector;
            p.NextPageSelector = "a.next";
            p.MaxPages = 50;
        });

        var result = await BuildEngine(fetcher).RunAsync(project);

        Assert.Null(result.ErrorMessage);
        Assert.Equal(1, result.PagesCrawled);
        Assert.Single(fetcher.RequestedUrls);
    }

    [Fact]
    public async Task RunAsync_NonSuccessStatusCode_ReturnsErrorAndStopsCrawling()
    {
        var fetcher = new FakePageFetcher(_ => new FetchedPage("", "<html></html>", 500));
        var project = Project();

        var result = await BuildEngine(fetcher).RunAsync(project);

        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("500", result.ErrorMessage);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task RunAsync_RequiredFieldMissing_ReturnsError()
    {
        var fetcher = new FakePageFetcher(_ => new FetchedPage("", "<div class='item'></div>", 200));
        var project = Project(p => p.Fields = new List<FieldSelector>
        {
            new() { Name = "title", Selector = ".title", Attribute = FieldAttribute.Text, Required = true },
        });

        var result = await BuildEngine(fetcher).RunAsync(project);

        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("title", result.ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_CustomHeaders_ArePassedToFetcher()
    {
        var fetcher = new FakePageFetcher(_ => new FetchedPage("", Page("A"), 200));
        var project = Project(p => p.CustomHeadersJson = """{"X-Api-Key":"secret"}""");

        await BuildEngine(fetcher).RunAsync(project);

        Assert.Equal("secret", fetcher.LastCustomHeaders?["X-Api-Key"]);
    }

    private static string Page(string itemTitle, string? nextHref = null)
    {
        var next = nextHref is null ? "" : $"<a class='next' href='{nextHref}'>Next</a>";
        return $"<html><body><div class='item'><span class='title'>{itemTitle}</span></div>{next}</body></html>";
    }

    private class FakePageFetcherFactory : IPageFetcherFactory
    {
        private readonly IPageFetcher _fetcher;
        public FakePageFetcherFactory(IPageFetcher fetcher) => _fetcher = fetcher;
        public IPageFetcher GetFetcher(RenderMode mode) => _fetcher;
    }

    private class FakePageFetcher : IPageFetcher
    {
        private readonly Func<string, FetchedPage> _respond;
        public List<string> RequestedUrls { get; } = new();
        public IReadOnlyDictionary<string, string>? LastCustomHeaders { get; private set; }

        public FakePageFetcher(Func<string, FetchedPage> respond) => _respond = respond;

        public Task<FetchedPage> FetchAsync(string url, IReadOnlyDictionary<string, string>? customHeaders = null, CancellationToken cancellationToken = default)
        {
            RequestedUrls.Add(url);
            LastCustomHeaders = customHeaders;
            return Task.FromResult(_respond(url) with { Url = url });
        }
    }

    private class AlwaysAllowRateLimiter : IDistributedRateLimiter
    {
        public Task<RateLimitDecision> TryAcquireAsync(string key, int permitLimit, int windowSeconds, int burstCapacity, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RateLimitDecision(true, TimeSpan.Zero));
    }
}
