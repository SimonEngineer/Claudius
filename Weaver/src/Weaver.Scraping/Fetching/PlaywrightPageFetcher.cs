using Microsoft.Playwright;
using Weaver.Scraping;

namespace Weaver.Scraping.Fetching;

/// <summary>
/// Renders the page in headless Chromium before returning its HTML, so scraping projects
/// targeting client-side-rendered (SPA) sites see the actual content instead of the empty
/// server-rendered shell. The browser process is expensive to start, so one is launched lazily
/// and shared across every fetch this instance handles; each fetch gets its own isolated
/// browser context/page so concurrent fetches don't share cookies or state.
/// </summary>
public class PlaywrightPageFetcher : IPageFetcher, IAsyncDisposable
{
    private const string UserAgent = "WeaverScraper/1.0 (+https://github.com/weaver)";

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public async Task<FetchedPage> FetchAsync(
        string url,
        IReadOnlyDictionary<string, string>? customHeaders = null,
        ProxyConfig? proxy = null,
        CancellationToken cancellationToken = default)
    {
        var browser = await GetBrowserAsync();

        // A custom User-Agent must be set on the browser context, not as an extra header --
        // Playwright doesn't reliably let extra headers override the context's UA.
        var customUserAgent = customHeaders?.FirstOrDefault(h => h.Key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase)).Value;
        var contextOptions = new BrowserNewContextOptions { UserAgent = string.IsNullOrWhiteSpace(customUserAgent) ? UserAgent : customUserAgent };
        if (proxy is { Enabled: true })
        {
            contextOptions.Proxy = new Microsoft.Playwright.Proxy
            {
                Server = proxy.BuildUri(),
                Username = proxy.Username,
                Password = proxy.Password,
            };
        }

        await using var context = await browser.NewContextAsync(contextOptions);
        await ApplyCustomHeadersAsync(context, url, customHeaders);
        var page = await context.NewPageAsync();

        IResponse? response;
        try
        {
            response = await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 30_000,
            });
        }
        catch (TimeoutException)
        {
            // Pages with long-polling/analytics connections never truly go idle; whatever
            // rendered by the timeout is still almost always usable content.
            response = null;
        }

        var html = await page.ContentAsync();
        return new FetchedPage(url, html, response?.Status ?? 200);
    }

    private static async Task ApplyCustomHeadersAsync(IBrowserContext context, string url, IReadOnlyDictionary<string, string>? customHeaders)
    {
        if (customHeaders is null || customHeaders.Count == 0)
        {
            return;
        }

        var plainHeaders = customHeaders
            .Where(h => !string.Equals(h.Key, "Cookie", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(h.Key, "User-Agent", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(h => h.Key, h => h.Value);
        if (plainHeaders.Count > 0)
        {
            await context.SetExtraHTTPHeadersAsync(plainHeaders);
        }

        // A raw "Cookie" extra header is unreliable in real browsers (the cookie jar can override
        // or strip it), so that one specific header is translated into real cookies instead.
        if (customHeaders.TryGetValue("Cookie", out var cookieHeader) && !string.IsNullOrWhiteSpace(cookieHeader))
        {
            var domain = new Uri(url).Host;
            var cookies = cookieHeader
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(pair => pair.Split('=', 2))
                .Where(parts => parts.Length == 2)
                .Select(parts => new Cookie { Name = parts[0].Trim(), Value = parts[1].Trim(), Domain = domain, Path = "/" })
                .ToArray();
            if (cookies.Length > 0)
            {
                await context.AddCookiesAsync(cookies);
            }
        }
    }

    private async Task<IBrowser> GetBrowserAsync()
    {
        if (_browser is not null)
        {
            return _browser;
        }

        await _initLock.WaitAsync();
        try
        {
            if (_browser is null)
            {
                _playwright = await Playwright.CreateAsync();
                var options = new BrowserTypeLaunchOptions { Headless = true };

                // Lets a deployment point at a system-installed or otherwise pre-provisioned
                // Chromium instead of the one Playwright's own installer manages.
                var executablePath = Environment.GetEnvironmentVariable("WEAVER_PLAYWRIGHT_EXECUTABLE_PATH");
                if (!string.IsNullOrWhiteSpace(executablePath))
                {
                    options.ExecutablePath = executablePath;
                }

                _browser = await _playwright.Chromium.LaunchAsync(options);
            }

            return _browser;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
    }
}
