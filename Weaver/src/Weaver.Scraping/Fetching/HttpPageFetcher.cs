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

    public async Task<FetchedPage> FetchAsync(string url, IReadOnlyDictionary<string, string>? customHeaders = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (customHeaders is not null)
        {
            // Per-request, not on the shared HttpClient's default headers: this fetcher instance
            // is reused across every project, so one project's headers must never leak into
            // another's requests.
            foreach (var (name, value) in customHeaders)
            {
                request.Headers.TryAddWithoutValidation(name, value);
            }
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return new FetchedPage(url, html, (int)response.StatusCode);
    }
}
