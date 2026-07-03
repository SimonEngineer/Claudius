using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Weaver.Infrastructure.Security;

/// <summary>Encrypts/decrypts stored credential values (the {{secrets.NAME}} store). Unlike node
/// config fields these are ALWAYS encrypted -- there's no plaintext passthrough case.</summary>
public interface ICredentialProtector
{
    string Encrypt(string plaintext);

    /// <summary>Returns null if the value can't be decrypted (key ring rotated/unavailable) rather
    /// than throwing -- callers treat that as "secret unavailable".</summary>
    string? Decrypt(string encrypted);
}

public class CredentialProtector : ICredentialProtector
{
    private readonly IDataProtector _protector;

    public CredentialProtector(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("Weaver.Credentials.v1");
    }

    public string Encrypt(string plaintext) => _protector.Protect(plaintext);

    public string? Decrypt(string encrypted)
    {
        try
        {
            return _protector.Unprotect(encrypted);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
