using System.Security.Cryptography;
using System.Text;

namespace Weaver.Infrastructure.Auth;

public interface IApiKeyService
{
    /// <summary>Generates a new random key. Returns the plaintext (shown to the user exactly once),
    /// the short prefix stored for display, and the hash stored for lookup/verification.</summary>
    (string PlaintextKey, string Prefix, string Hash) GenerateKey();

    string Hash(string plaintextKey);

    /// <summary>Cheap shape check so request handling can quickly tell "this is an API key" apart
    /// from "this is a JWT" without doing a database lookup for every request.</summary>
    bool LooksLikeApiKey(string token);
}

public class ApiKeyService : IApiKeyService
{
    public const string KeyPrefixLiteral = "wvr_";

    public (string PlaintextKey, string Prefix, string Hash) GenerateKey()
    {
        var randomPart = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var plaintext = KeyPrefixLiteral + randomPart;
        var prefix = plaintext[..Math.Min(12, plaintext.Length)];
        return (plaintext, prefix, Hash(plaintext));
    }

    public string Hash(string plaintextKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plaintextKey)));

    public bool LooksLikeApiKey(string token) => token.StartsWith(KeyPrefixLiteral, StringComparison.Ordinal);
}
