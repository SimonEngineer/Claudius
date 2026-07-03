namespace Weaver.Domain;

/// <summary>
/// A named secret (API key, token, password) a user stores once and references from workflow node
/// configs as {{secrets.NAME}}. The value is encrypted at rest and never returned by the read API
/// after creation -- only the name and metadata are.
/// </summary>
public class Credential
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; set; }

    /// <summary>Reference key used in templates; unique per user.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Data Protection-encrypted secret value.</summary>
    public string EncryptedValue { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
