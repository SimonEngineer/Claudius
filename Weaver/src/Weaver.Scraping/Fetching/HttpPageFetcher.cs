using System.Net;
using Weaver.Scraping;

namespace Weaver.Scraping.Fetching;

public class HttpPageFetcher : IPageFetcher
{
    private const string UserAgent = "WeaverScraper/1.0 (+https://github.com/weaver)";

    private readonly HttpClient _httpClient;

    public HttpPageFetcher(HttpClient httpClient)
    {
        _httpClient = httpClient;
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        }
    }

    public async Task<FetchedPage> FetchAsync(
        string url,
        IReadOnlyDictionary<string, string>? customHeaders = null,
        ProxyConfig? proxy = null,
        CancellationToken cancellationToken = default)
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

        // The shared HttpClient's proxy is fixed at construction, so a project-specific proxy
        // needs its own throwaway client for just this one request.
        using var proxyClient = proxy is { Enabled: true } ? BuildProxyClient(proxy) : null;
        var client = proxyClient ?? _httpClient;

        using var response = await client.SendAsync(request, cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return new FetchedPage(url, html, (int)response.StatusCode);
    }

    private static HttpClient BuildProxyClient(ProxyConfig proxy)
    {
        var webProxy = new WebProxy(proxy.BuildUri());
        if (!string.IsNullOrEmpty(proxy.Username))
        {
            webProxy.Credentials = new NetworkCredential(proxy.Username, proxy.Password);
        }

        var client = new HttpClient(new HttpClientHandler { Proxy = webProxy, UseProxy = true });
        client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        return client;
    }
}
