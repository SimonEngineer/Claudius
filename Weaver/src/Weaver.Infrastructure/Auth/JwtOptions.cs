namespace Weaver.Infrastructure.Auth;

public class JwtOptions
{
    /// <summary>Symmetric signing key. Must be set (and kept secret) in production -- see appsettings.json's "Jwt:SigningKey".</summary>
    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "Weaver";
    public string Audience { get; set; } = "Weaver";
    public int ExpiryMinutes { get; set; } = 60 * 24 * 7;
}
