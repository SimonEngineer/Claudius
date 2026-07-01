using System.Security.Cryptography;
using System.Text;
using Weaver.Domain;

namespace Weaver.Scraping;

public static class ItemHasher
{
    /// <summary>
    /// Stable identity for an item: the concatenated IsKey field values, or -- when no field is
    /// marked as a key -- the page URL plus the item's ordinal position on that page. The content
    /// hash is deliberately NOT used as a fallback key: the key must stay constant when an item's
    /// data changes, otherwise "changed" items are never matched against their previous snapshot
    /// and change-detection (e.g. price-drop alerts) silently never fires.
    /// </summary>
    public static string ComputeItemKey(Dictionary<string, string?> data, IReadOnlyList<FieldSelector> fields, string sourceUrl, int ordinal)
    {
        var keyFields = fields.Where(f => f.IsKey).OrderBy(f => f.Order).ToList();
        if (keyFields.Count == 0)
        {
            return Hash($"{sourceUrl}#{ordinal}");
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
