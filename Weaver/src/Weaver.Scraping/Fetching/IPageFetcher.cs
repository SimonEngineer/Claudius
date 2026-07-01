namespace Weaver.Scraping.Fetching;

public record FetchedPage(string Url, string Html, int StatusCode);

public interface IPageFetcher
{
    Task<FetchedPage> FetchAsync(string url, CancellationToken cancellationToken = default);
}
