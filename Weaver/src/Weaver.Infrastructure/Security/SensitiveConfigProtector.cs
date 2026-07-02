using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.DataProtection;

namespace Weaver.Infrastructure.Security;

/// <summary>
/// Encrypts the handful of workflow node config fields that hold live credentials (a Discord
/// webhook URL, an inbound webhook shared secret) before they're written to Postgres, and
/// decrypts them back on the way out. Everything else in a node's config is left alone -- this
/// isn't a general-purpose config encryptor, just cover for the specific fields that are
/// effectively bearer credentials.
/// </summary>
public interface ISensitiveConfigProtector
{
    string EncryptForStorage(string nodeType, string configJson);
    string DecryptForUse(string nodeType, string configJson);

    /// <summary>Blanks out sensitive fields entirely rather than decrypting them -- for a portable
    /// workflow export file, which might be shared or committed to source control, and must never
    /// carry a live credential in plaintext regardless of who ends up with a copy of it.</summary>
    string RedactForExport(string nodeType, string configJson);
}

public class SensitiveConfigProtector : ISensitiveConfigProtector
{
    private const string Prefix = "enc:v1:";

    private static readonly Dictionary<string, string[]> SensitiveFieldsByNodeType = new()
    {
        ["action.sendDiscord"] = new[] { "webhookUrl" },
        ["action.sendSlack"] = new[] { "webhookUrl" },
        ["trigger.http"] = new[] { "secret" },
        ["scrapingProject.proxy"] = new[] { "password" },
    };

    private readonly IDataProtector _protector;

    public SensitiveConfigProtector(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("Weaver.NodeConfig.v1");
    }

    public string EncryptForStorage(string nodeType, string configJson) => Transform(nodeType, configJson, Encrypt);

    public string DecryptForUse(string nodeType, string configJson) => Transform(nodeType, configJson, Decrypt);

    public string RedactForExport(string nodeType, string configJson) => Transform(nodeType, configJson, _ => string.Empty);

    private static string Transform(string nodeType, string configJson, Func<string, string> fn)
    {
        if (string.IsNullOrWhiteSpace(configJson) || !SensitiveFieldsByNodeType.TryGetValue(nodeType, out var fields))
        {
            return configJson;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(configJson);
        }
        catch (JsonException)
        {
            return configJson;
        }

        if (node is not JsonObject obj)
        {
            return configJson;
        }

        var changed = false;
        foreach (var field in fields)
        {
            if (obj[field] is JsonValue value && value.TryGetValue<string>(out var s) && !string.IsNullOrEmpty(s))
            {
                obj[field] = fn(s);
                changed = true;
            }
        }

        return changed ? obj.ToJsonString() : configJson;
    }

    private string Encrypt(string plaintext) =>
        plaintext.StartsWith(Prefix, StringComparison.Ordinal) ? plaintext : Prefix + _protector.Protect(plaintext);

    private string Decrypt(string value)
    {
        if (!value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return value;
        }

        try
        {
            return _protector.Unprotect(value[Prefix.Length..]);
        }
        catch (CryptographicException)
        {
            // Key ring unavailable or rotated out from under us -- fail safe by returning the
            // (unusable) ciphertext rather than throwing and taking the whole node run down.
            return value;
        }
    }
}
