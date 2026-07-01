using Weaver.Domain;
using Weaver.Infrastructure.RateLimiting;

namespace Weaver.Scraping;

/// <summary>
/// Blocks the caller until the distributed bucket for the target URL has a free token.
/// Because acquisition goes through Redis, one instance backing off here directly reduces
/// how many tokens are left for every other instance sharing the same bucket key.
/// </summary>
public class RateLimitGate
{
    private const int MaxWaitAttempts = 100;

    /// <summary>
    /// Applied to interactive, ad-hoc fetches (the page picker, test-extraction) whenever the
    /// project has no rate limit policy of its own assigned -- so browsing a site while building
    /// selectors is never fully unthrottled, even before a project has been configured or saved.
    /// </summary>
    public static readonly RateLimitPolicy DefaultInteractivePolicy = new()
    {
        Id = Guid.Empty,
        Name = "default-interactive",
        KeyScope = RateLimitKeyScope.PerHost,
        PermitLimit = 30,
        WindowSeconds = 60,
        BurstCapacity = 30,
    };

    private readonly IDistributedRateLimiter _rateLimiter;

    public RateLimitGate(IDistributedRateLimiter rateLimiter)
    {
        _rateLimiter = rateLimiter;
    }

    public async Task WaitForSlotAsync(RateLimitPolicy? policy, Guid scrapingProjectId, Uri targetUrl, CancellationToken cancellationToken)
    {
        if (policy is null)
        {
            return;
        }

        var key = RateLimitKeyResolver.Resolve(policy, scrapingProjectId, targetUrl);

        for (var attempt = 0; attempt < MaxWaitAttempts; attempt++)
        {
            var decision = await _rateLimiter.TryAcquireAsync(
                key, policy.PermitLimit, policy.WindowSeconds, policy.BurstCapacity, cancellationToken);

            if (decision.Allowed)
            {
                return;
            }

            var wait = decision.RetryAfter < TimeSpan.FromMilliseconds(50)
                ? TimeSpan.FromMilliseconds(50)
                : decision.RetryAfter;
            await Task.Delay(wait, cancellationToken);
        }

        throw new TimeoutException($"Rate limit for key '{key}' did not free up after {MaxWaitAttempts} attempts.");
    }
}
