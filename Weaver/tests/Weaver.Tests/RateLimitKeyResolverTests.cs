using Weaver.Domain;
using Weaver.Infrastructure.RateLimiting;
using Xunit;

namespace Weaver.Tests;

public class RateLimitKeyResolverTests
{
    private static RateLimitPolicy Policy(RateLimitKeyScope scope, string? customTemplate = null) => new()
    {
        Name = "p",
        KeyScope = scope,
        CustomKeyTemplate = customTemplate,
        PermitLimit = 10,
        WindowSeconds = 60,
        BurstCapacity = 10,
    };

    [Fact]
    public void PerHost_IgnoresPathAndProject()
    {
        var policy = Policy(RateLimitKeyScope.PerHost);
        var keyA = RateLimitKeyResolver.Resolve(policy, Guid.NewGuid(), new Uri("https://Example.com/a"));
        var keyB = RateLimitKeyResolver.Resolve(policy, Guid.NewGuid(), new Uri("https://example.com/b"));

        Assert.Equal(keyA, keyB);
        Assert.Equal("example.com", keyA);
    }

    [Fact]
    public void PerUrl_DistinguishesDifferentPaths()
    {
        var policy = Policy(RateLimitKeyScope.PerUrl);
        var keyA = RateLimitKeyResolver.Resolve(policy, Guid.Empty, new Uri("https://example.com/a"));
        var keyB = RateLimitKeyResolver.Resolve(policy, Guid.Empty, new Uri("https://example.com/b"));

        Assert.NotEqual(keyA, keyB);
    }

    [Fact]
    public void PerProject_SameHostDifferentProjects_ProducesDifferentKeys()
    {
        var policy = Policy(RateLimitKeyScope.PerProject);
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();

        var keyA = RateLimitKeyResolver.Resolve(policy, projectA, new Uri("https://example.com/a"));
        var keyB = RateLimitKeyResolver.Resolve(policy, projectB, new Uri("https://example.com/a"));

        Assert.NotEqual(keyA, keyB);
        Assert.Contains(projectA.ToString(), keyA);
    }

    [Fact]
    public void Custom_SubstitutesAllTemplateTokens()
    {
        var projectId = Guid.NewGuid();
        var policy = Policy(RateLimitKeyScope.Custom, "{host}:{path}:{project}");

        var key = RateLimitKeyResolver.Resolve(policy, projectId, new Uri("https://Example.com/Search"));

        Assert.Equal($"example.com:/search:{projectId}".ToLowerInvariant(), key);
    }

    [Fact]
    public void Custom_MissingTemplate_FallsBackToHost()
    {
        var policy = Policy(RateLimitKeyScope.Custom, null);
        var key = RateLimitKeyResolver.Resolve(policy, Guid.Empty, new Uri("https://example.com/a"));

        Assert.Equal("example.com", key);
    }
}
