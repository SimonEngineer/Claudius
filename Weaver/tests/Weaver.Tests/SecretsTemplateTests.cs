using System.Text.Json.Nodes;
using Weaver.Workflows;
using Xunit;

namespace Weaver.Tests;

public class SecretsTemplateTests
{
    private static string? Lookup(string name) => name switch
    {
        "apiKey" => "sk-12345",
        "slack.token" => "xoxb-abc",
        _ => null,
    };

    [Fact]
    public void Resolve_ReplacesTokenInsideStringValue()
    {
        var config = JsonNode.Parse("""{"headers": "Authorization: Bearer {{secrets.apiKey}}"}""");
        var resolved = SecretsTemplate.Resolve(config, Lookup);
        Assert.Equal("Authorization: Bearer sk-12345", resolved?["headers"]?.GetValue<string>());
    }

    [Fact]
    public void Resolve_UnknownSecret_LeftUntouched()
    {
        var config = JsonNode.Parse("""{"url": "{{secrets.missing}}"}""");
        var resolved = SecretsTemplate.Resolve(config, Lookup);
        Assert.Equal("{{secrets.missing}}", resolved?["url"]?.GetValue<string>());
    }

    [Fact]
    public void Resolve_HandlesNestedObjectsAndArrays()
    {
        var config = JsonNode.Parse("""{"outer": {"list": ["{{secrets.apiKey}}", 42, {"deep": "{{ secrets.slack.token }}"}]}}""");
        var resolved = SecretsTemplate.Resolve(config, Lookup);
        var list = resolved?["outer"]?["list"]?.AsArray();
        Assert.Equal("sk-12345", list?[0]?.GetValue<string>());
        Assert.Equal(42, list?[1]?.GetValue<int>());
        Assert.Equal("xoxb-abc", list?[2]?["deep"]?.GetValue<string>());
    }

    [Fact]
    public void Resolve_NonStringValues_Unchanged()
    {
        var config = JsonNode.Parse("""{"count": 3, "enabled": true, "nothing": null}""");
        var resolved = SecretsTemplate.Resolve(config, Lookup);
        Assert.Equal(3, resolved?["count"]?.GetValue<int>());
        Assert.True(resolved?["enabled"]?.GetValue<bool>());
    }

    [Fact]
    public void ContainsSecretTokens_DetectsPresenceOnly()
    {
        Assert.True(SecretsTemplate.ContainsSecretTokens("""{"x": "{{secrets.a}}"}"""));
        Assert.False(SecretsTemplate.ContainsSecretTokens("""{"x": "{{current.price}}"}"""));
        Assert.False(SecretsTemplate.ContainsSecretTokens(null));
    }
}
