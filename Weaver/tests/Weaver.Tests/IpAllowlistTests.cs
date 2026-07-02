using System.Net;
using Weaver.Infrastructure.Security;
using Xunit;

namespace Weaver.Tests;

public class IpAllowlistTests
{
    [Fact]
    public void IsAllowed_EmptyOrNullList_AllowsAnything()
    {
        Assert.True(IpAllowlist.IsAllowed(IPAddress.Parse("203.0.113.5"), null));
        Assert.True(IpAllowlist.IsAllowed(IPAddress.Parse("203.0.113.5"), Array.Empty<string>()));
    }

    [Fact]
    public void IsAllowed_ExactMatch_Allows()
    {
        Assert.True(IpAllowlist.IsAllowed(IPAddress.Parse("203.0.113.5"), new[] { "203.0.113.5" }));
    }

    [Fact]
    public void IsAllowed_ExactMismatch_Denies()
    {
        Assert.False(IpAllowlist.IsAllowed(IPAddress.Parse("203.0.113.6"), new[] { "203.0.113.5" }));
    }

    [Fact]
    public void IsAllowed_CidrMatch_Allows()
    {
        Assert.True(IpAllowlist.IsAllowed(IPAddress.Parse("10.20.30.40"), new[] { "10.0.0.0/8" }));
    }

    [Fact]
    public void IsAllowed_CidrMismatch_Denies()
    {
        Assert.False(IpAllowlist.IsAllowed(IPAddress.Parse("11.20.30.40"), new[] { "10.0.0.0/8" }));
    }

    [Fact]
    public void IsAllowed_MultipleEntries_MatchesAny()
    {
        var entries = new[] { "192.168.1.1", "10.0.0.0/8" };
        Assert.True(IpAllowlist.IsAllowed(IPAddress.Parse("10.5.5.5"), entries));
        Assert.True(IpAllowlist.IsAllowed(IPAddress.Parse("192.168.1.1"), entries));
        Assert.False(IpAllowlist.IsAllowed(IPAddress.Parse("172.16.0.1"), entries));
    }

    [Fact]
    public void IsAllowed_NullRemoteIpWithConfiguredList_Denies()
    {
        Assert.False(IpAllowlist.IsAllowed(null, new[] { "10.0.0.0/8" }));
    }

    [Fact]
    public void IsAllowed_Ipv4MappedIpv6_MatchesIpv4Cidr()
    {
        var mapped = IPAddress.Parse("::ffff:10.1.2.3");
        Assert.True(IpAllowlist.IsAllowed(mapped, new[] { "10.0.0.0/8" }));
    }

    [Fact]
    public void IsAllowed_Ipv6CidrMatch_Allows()
    {
        Assert.True(IpAllowlist.IsAllowed(IPAddress.Parse("2001:db8::1"), new[] { "2001:db8::/32" }));
        Assert.False(IpAllowlist.IsAllowed(IPAddress.Parse("2001:db9::1"), new[] { "2001:db8::/32" }));
    }
}
