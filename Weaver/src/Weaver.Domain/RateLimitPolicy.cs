namespace Weaver.Domain;

/// <summary>
/// A reusable, named rate-limit policy. The actual bucket key used at runtime is derived
/// from KeyScope against the target URL (e.g. host, full URL, or the owning project id),
/// so unrelated projects hitting the same host still share one distributed bucket.
/// </summary>
public class RateLimitPolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    public RateLimitKeyScope KeyScope { get; set; } = RateLimitKeyScope.PerHost;

    /// <summary>
    /// Only used when KeyScope == Custom. Supports {host}, {path}, {project} placeholders.
    /// </summary>
    public string? CustomKeyTemplate { get; set; }

    /// <summary>Max requests permitted per window (token bucket refill amount).</summary>
    public int PermitLimit { get; set; } = 60;

    /// <summary>Window length, in seconds, over which PermitLimit tokens refill.</summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>Max burst size (bucket capacity). Defaults to PermitLimit.</summary>
    public int BurstCapacity { get; set; } = 60;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
