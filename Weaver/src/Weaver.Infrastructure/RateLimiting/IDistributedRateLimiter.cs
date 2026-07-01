namespace Weaver.Infrastructure.RateLimiting;

public record RateLimitDecision(bool Allowed, TimeSpan RetryAfter);

/// <summary>
/// A token-bucket rate limiter whose state lives in Redis, so every process instance sharing
/// the same Redis sees the same bucket for a given key -- if instance A exhausts a bucket,
/// instance B is refused too, without either instance needing to know about the other.
/// </summary>
public interface IDistributedRateLimiter
{
    /// <summary>
    /// Attempts to take one token from the bucket identified by <paramref name="key"/>.
    /// The bucket refills at permitLimit tokens per windowSeconds, capped at burstCapacity.
    /// </summary>
    Task<RateLimitDecision> TryAcquireAsync(
        string key,
        int permitLimit,
        int windowSeconds,
        int burstCapacity,
        CancellationToken cancellationToken = default);
}
