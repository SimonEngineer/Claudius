using Weaver.Scraping;
using Xunit;

namespace Weaver.Tests;

public class RobotsTxtParserTests
{
    [Fact]
    public void Parse_WildcardGroup_DisallowsMatchingPath()
    {
        var rules = RobotsTxtParser.Parse("User-agent: *\nDisallow: /private/", "WeaverScraper");
        Assert.False(rules.IsAllowed("/private/page"));
        Assert.True(rules.IsAllowed("/public/page"));
    }

    [Fact]
    public void Parse_SpecificGroupWins_OverWildcard()
    {
        var content = """
            User-agent: *
            Disallow: /

            User-agent: WeaverScraper
            Disallow: /admin/
            """;
        var rules = RobotsTxtParser.Parse(content, "WeaverScraper/1.0");
        Assert.True(rules.IsAllowed("/anything"));
        Assert.False(rules.IsAllowed("/admin/panel"));
    }

    [Fact]
    public void IsAllowed_AllowBeatsDisallow_WhenMoreSpecific()
    {
        var content = """
            User-agent: *
            Disallow: /shop/
            Allow: /shop/public/
            """;
        var rules = RobotsTxtParser.Parse(content, "WeaverScraper");
        Assert.False(rules.IsAllowed("/shop/cart"));
        Assert.True(rules.IsAllowed("/shop/public/list"));
    }

    [Fact]
    public void IsAllowed_EmptyDisallow_MeansEverythingAllowed()
    {
        var rules = RobotsTxtParser.Parse("User-agent: *\nDisallow:", "WeaverScraper");
        Assert.True(rules.IsAllowed("/anything"));
    }

    [Fact]
    public void IsAllowed_WildcardInPath_Matches()
    {
        var rules = RobotsTxtParser.Parse("User-agent: *\nDisallow: /*.pdf$", "WeaverScraper");
        Assert.False(rules.IsAllowed("/docs/file.pdf"));
        Assert.True(rules.IsAllowed("/docs/file.pdf.html"));
        Assert.True(rules.IsAllowed("/docs/file.html"));
    }

    [Fact]
    public void IsAllowed_CommentsAndBlankLines_Ignored()
    {
        var content = """
            # global rules
            User-agent: *
            Disallow: /tmp/ # scratch space
            """;
        var rules = RobotsTxtParser.Parse(content, "WeaverScraper");
        Assert.False(rules.IsAllowed("/tmp/x"));
        Assert.True(rules.IsAllowed("/tmpx"));
    }

    [Fact]
    public void Parse_MultipleAgentsShareGroup()
    {
        var content = """
            User-agent: OtherBot
            User-agent: WeaverScraper
            Disallow: /blocked/
            """;
        var rules = RobotsTxtParser.Parse(content, "WeaverScraper");
        Assert.False(rules.IsAllowed("/blocked/x"));
    }

    [Fact]
    public void Parse_NoMatchingGroup_AllowsEverything()
    {
        var rules = RobotsTxtParser.Parse("User-agent: OtherBot\nDisallow: /", "WeaverScraper");
        Assert.True(rules.IsAllowed("/anything"));
    }
}
