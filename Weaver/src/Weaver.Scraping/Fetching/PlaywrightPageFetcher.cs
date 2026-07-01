using Microsoft.Playwright;

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

    public async Task<FetchedPage> FetchAsync(string url, CancellationToken cancellationToken = default)
    {
        var browser = await GetBrowserAsync();

        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions { UserAgent = UserAgent });
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
