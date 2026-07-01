using Weaver.Domain;

namespace Weaver.Infrastructure.RateLimiting;

/// <summary>
/// Turns a policy + target URL into the actual bucket key shared across processes.
/// PerHost means two different scraping projects hitting the same site share one bucket
/// (so they can't gang up to exceed the intended per-site rate), PerProject isolates a
/// project's own pacing regardless of host, and Custom lets a policy target something
/// narrower, like a specific API path.
/// </summary>
public static class RateLimitKeyResolver
{
    public static string Resolve(RateLimitPolicy policy, Guid scrapingProjectId, Uri targetUrl)
    {
        return policy.KeyScope switch
        {
            RateLimitKeyScope.PerHost => targetUrl.Host.ToLowerInvariant(),
            RateLimitKeyScope.PerUrl => $"{targetUrl.Host}{targetUrl.AbsolutePath}".ToLowerInvariant(),
            RateLimitKeyScope.PerProject => $"project:{scrapingProjectId}",
            RateLimitKeyScope.Custom => ApplyTemplate(policy.CustomKeyTemplate, scrapingProjectId, targetUrl),
            _ => targetUrl.Host.ToLowerInvariant()
        };
    }

    private static string ApplyTemplate(string? template, Guid scrapingProjectId, Uri targetUrl)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return targetUrl.Host.ToLowerInvariant();
        }

        return template
            .Replace("{host}", targetUrl.Host, StringComparison.OrdinalIgnoreCase)
            .Replace("{path}", targetUrl.AbsolutePath, StringComparison.OrdinalIgnoreCase)
            .Replace("{project}", scrapingProjectId.ToString(), StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();
    }
}
