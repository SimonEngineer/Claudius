using Microsoft.Extensions.Logging;

namespace Weaver.Scraping.Fetching;

/// <summary>
/// Installs Playwright's Chromium binary if it isn't already present. Opt-in via the
/// WEAVER_ENABLE_PLAYWRIGHT env var, since most installs never use RenderMode.Playwright and
/// downloading/checking for a ~150MB browser on every startup would otherwise slow all of them
/// down for no benefit. Safe to call repeatedly -- Playwright's installer no-ops quickly once
/// the browser is already present.
/// </summary>
public static class PlaywrightBootstrap
{
    public static void EnsureBrowsersInstalledIfEnabled(ILogger logger)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("WEAVER_ENABLE_PLAYWRIGHT"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium", "--with-deps"]);
            if (exitCode != 0)
            {
                logger.LogWarning("Playwright browser install exited with code {ExitCode}; RenderMode.Playwright scraping may not work.", exitCode);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to install Playwright's Chromium browser; RenderMode.Playwright scraping will not work until this is resolved.");
        }
    }
}
