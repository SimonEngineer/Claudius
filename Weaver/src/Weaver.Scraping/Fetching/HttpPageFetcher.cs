namespace Weaver.Scraping.Fetching;

public class HttpPageFetcher : IPageFetcher
{
    private readonly HttpClient _httpClient;

    public HttpPageFetcher(HttpClient httpClient)
    {
        _httpClient = httpClient;
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "WeaverScraper/1.0 (+https://github.com/weaver)");
        }
    }

    public async Task<FetchedPage> FetchAsync(string url, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return new FetchedPage(url, html, (int)response.StatusCode);
    }
}
