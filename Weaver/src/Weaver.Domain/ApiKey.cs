namespace Weaver.Domain;

/// <summary>
/// A long-lived bearer credential for server-to-server automation (triggering scrapes/workflows
/// from an external script or cron job) as an alternative to a short-lived JWT that requires a
/// login flow. Only a SHA-256 hash of the key is ever stored -- the plaintext is shown to the user
/// exactly once, at creation time, the same way GitHub/Stripe personal access tokens work.
/// </summary>
public class ApiKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>First few characters of the key, shown alongside Name so a user can tell keys apart
    /// in the list without ever seeing the rest of it again.</summary>
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>SHA-256 hash (hex) of the full key. A fast hash is fine here, unlike a password --
    /// the key itself is a high-entropy random token, not something a dictionary attack can guess.</summary>
    public string HashedKey { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
