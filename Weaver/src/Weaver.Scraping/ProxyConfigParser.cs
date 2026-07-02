using System.Text.Json;

namespace Weaver.Scraping;

/// <summary>An outbound proxy a scraping project's requests are routed through.</summary>
public record ProxyConfig(bool Enabled, string Protocol, string Host, int Port, string? Username, string? Password)
{
    public static readonly ProxyConfig Disabled = new(false, "http", string.Empty, 0, null, null);

    public string BuildUri()
    {
        var scheme = string.Equals(Protocol, "socks5", StringComparison.OrdinalIgnoreCase) ? "socks5" : "http";
        return $"{scheme}://{Host}:{Port}";
    }
}

public static class ProxyConfigParser
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static ProxyConfig Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return ProxyConfig.Disabled;
        }

        try
        {
            return JsonSerializer.Deserialize<ProxyConfig>(json, Options) ?? ProxyConfig.Disabled;
        }
        catch (JsonException)
        {
            return ProxyConfig.Disabled;
        }
    }

    public static string ToJson(ProxyConfig config) => JsonSerializer.Serialize(config, Options);
}
