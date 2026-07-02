using System.Net;

namespace Weaver.Infrastructure.Security;

/// <summary>Matches a client IP against an allowlist of exact addresses and/or CIDR ranges, used to
/// restrict which sources may invoke an inbound webhook. An empty/missing allowlist means
/// unrestricted, preserving today's behavior for triggers that don't configure one.</summary>
public static class IpAllowlist
{
    public static bool IsAllowed(IPAddress? remoteIp, IReadOnlyList<string>? entries)
    {
        var normalizedEntries = entries?.Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
        if (normalizedEntries is null || normalizedEntries.Count == 0)
        {
            return true;
        }

        if (remoteIp is null)
        {
            return false;
        }

        var remote = Normalize(remoteIp);
        return normalizedEntries.Any(e => Matches(remote, e.Trim()));
    }

    private static bool Matches(IPAddress remoteIp, string entry)
    {
        var slashIndex = entry.IndexOf('/');
        if (slashIndex < 0)
        {
            return IPAddress.TryParse(entry, out var exact) && Normalize(exact).Equals(remoteIp);
        }

        var baseAddressText = entry[..slashIndex];
        var prefixText = entry[(slashIndex + 1)..];
        if (!IPAddress.TryParse(baseAddressText, out var baseAddress) || !int.TryParse(prefixText, out var prefixLength))
        {
            return false;
        }

        baseAddress = Normalize(baseAddress);
        if (baseAddress.AddressFamily != remoteIp.AddressFamily)
        {
            return false;
        }

        var baseBytes = baseAddress.GetAddressBytes();
        var remoteBytes = remoteIp.GetAddressBytes();
        if (prefixLength < 0 || prefixLength > baseBytes.Length * 8)
        {
            return false;
        }

        var fullBytes = prefixLength / 8;
        for (var i = 0; i < fullBytes; i++)
        {
            if (baseBytes[i] != remoteBytes[i])
            {
                return false;
            }
        }

        var remainingBits = prefixLength % 8;
        if (remainingBits == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - remainingBits));
        return (baseBytes[fullBytes] & mask) == (remoteBytes[fullBytes] & mask);
    }

    private static IPAddress Normalize(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}
