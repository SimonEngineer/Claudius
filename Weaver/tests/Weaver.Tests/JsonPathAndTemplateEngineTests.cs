using System.Text.Json.Nodes;
using Weaver.Workflows;
using Xunit;

namespace Weaver.Tests;

public class JsonPathAndTemplateEngineTests
{
    [Fact]
    public void Resolve_SimpleDotPath_ReturnsNestedValue()
    {
        var root = JsonNode.Parse("""{"current":{"price":9.99}}""");
        var value = JsonPathHelper.ResolveAsString(root, "current.price");
        Assert.Equal("9.99", value);
    }

    [Fact]
    public void Resolve_MissingPath_ReturnsNull()
    {
        var root = JsonNode.Parse("""{"current":{"price":9.99}}""");
        Assert.Null(JsonPathHelper.Resolve(root, "current.missing"));
    }

    [Fact]
    public void ResolveWithContext_FirstSegmentMatchesNodeName_ReachesThatNodesOutput()
    {
        var input = JsonNode.Parse("""{"unrelated": true}""");
        var allOutputs = new Dictionary<string, JsonNode?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Scrape Fixture"] = JsonNode.Parse("""{"itemsChanged": 3}"""),
        };

        var value = JsonPathHelper.ResolveAsStringWithContext(input, allOutputs, "Scrape Fixture.itemsChanged");

        Assert.Equal("3", value);
    }

    [Fact]
    public void ResolveWithContext_NoMatchingNodeName_FallsBackToInput()
    {
        var input = JsonNode.Parse("""{"price": 5}""");
        var value = JsonPathHelper.ResolveAsStringWithContext(input, JsonPathHelper.EmptyContext, "price");
        Assert.Equal("5", value);
    }

    [Fact]
    public void TemplateEngine_Render_ReplacesTokenFromInput()
    {
        var input = JsonNode.Parse("""{"price": 42}""");
        var result = TemplateEngine.Render("Price is {{price}}!", input);
        Assert.Equal("Price is 42!", result);
    }

    [Fact]
    public void TemplateEngine_Render_ReplacesMultipleTokensAndReachesBackToNamedNode()
    {
        var input = JsonNode.Parse("""{"price": 42}""");
        var allOutputs = new Dictionary<string, JsonNode?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Get Threshold"] = JsonNode.Parse("""{"maxPrice": 100}"""),
        };

        var result = TemplateEngine.Render("{{price}} vs {{Get Threshold.maxPrice}}", input, allOutputs);

        Assert.Equal("42 vs 100", result);
    }

    [Fact]
    public void TemplateEngine_Render_UnresolvableToken_BecomesEmptyString()
    {
        var input = JsonNode.Parse("""{"price": 42}""");
        var result = TemplateEngine.Render("value={{does.not.exist}}", input);
        Assert.Equal("value=", result);
    }

    [Fact]
    public void TemplateEngine_Render_TokenWithSpacesInNodeName_Works()
    {
        var allOutputs = new Dictionary<string, JsonNode?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Scrape Fixture"] = JsonNode.Parse("""{"items": 7}"""),
        };

        var result = TemplateEngine.Render("count={{ Scrape Fixture.items }}", null, allOutputs);

        Assert.Equal("count=7", result);
    }
}
