using Weaver.Domain;

namespace Weaver.Scraping.Fetching;

public interface IPageFetcherFactory
{
    IPageFetcher GetFetcher(RenderMode mode);
}

public class PageFetcherFactory : IPageFetcherFactory
{
    private readonly HttpPageFetcher _httpFetcher;
    private readonly PlaywrightPageFetcher _playwrightFetcher;

    public PageFetcherFactory(HttpPageFetcher httpFetcher, PlaywrightPageFetcher playwrightFetcher)
    {
        _httpFetcher = httpFetcher;
        _playwrightFetcher = playwrightFetcher;
    }

    public IPageFetcher GetFetcher(RenderMode mode) => mode switch
    {
        RenderMode.Playwright => _playwrightFetcher,
        _ => _httpFetcher,
    };
}
