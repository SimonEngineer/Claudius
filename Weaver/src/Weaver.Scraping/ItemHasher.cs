using System.Security.Cryptography;
using System.Text;
using Weaver.Domain;

namespace Weaver.Scraping;

public static class ItemHasher
{
    /// <summary>Stable identity for an item: the concatenated IsKey field values, or a hash of everything if none are marked.</summary>
    public static string ComputeItemKey(Dictionary<string, string?> data, IReadOnlyList<FieldSelector> fields)
    {
        var keyFields = fields.Where(f => f.IsKey).OrderBy(f => f.Order).ToList();
        if (keyFields.Count == 0)
        {
            return ComputeContentHash(data);
        }

        var raw = string.Join("|", keyFields.Select(f => data.GetValueOrDefault(f.Name) ?? string.Empty));
        return Hash(raw);
    }

    /// <summary>Hash of every field value, used to detect whether an item's data changed since last seen.</summary>
    public static string ComputeContentHash(Dictionary<string, string?> data)
    {
        var raw = string.Join("|", data.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value}"));
        return Hash(raw);
    }

    private static string Hash(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}
