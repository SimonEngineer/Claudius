using AngleSharp;
using AngleSharp.Dom;
using Weaver.Domain;
using Weaver.Scraping;
using Xunit;

namespace Weaver.Tests;

public class SelectorExtractorTests
{
    private static async Task<IDocument> ParseAsync(string html)
    {
        var context = BrowsingContext.New(Configuration.Default);
        return await context.OpenAsync(req => req.Content(html).Address("https://example.com/list"));
    }

    private static FieldSelector Field(string name, string selector, FieldAttribute attr = FieldAttribute.Text,
        string? attributeName = null, bool resolveUrl = false, bool required = false) => new()
    {
        Name = name,
        Selector = selector,
        Attribute = attr,
        AttributeName = attributeName,
        ResolveUrl = resolveUrl,
        Required = required,
    };

    [Fact]
    public async Task ExtractFields_Text_TrimsWhitespace()
    {
        var doc = await ParseAsync("<div class='item'><span class='title'>  Widget  </span></div>");
        var container = doc.QuerySelector(".item")!;

        var data = SelectorExtractor.ExtractFields(container, new List<FieldSelector> { Field("title", ".title") }, new Uri("https://example.com/list"));

        Assert.Equal("Widget", data["title"]);
    }

    [Fact]
    public async Task ExtractFields_Html_PreservesMarkup()
    {
        var doc = await ParseAsync("<div class='item'><span class='title'>Wi<b>d</b>get</span></div>");
        var container = doc.QuerySelector(".item")!;

        var data = SelectorExtractor.ExtractFields(container, new List<FieldSelector> { Field("title", ".title", FieldAttribute.Html) }, new Uri("https://example.com/list"));

        Assert.Contains("<b>d</b>", data["title"]);
    }

    [Fact]
    public async Task ExtractFields_Href_ResolvesRelativeUrlWhenRequested()
    {
        var doc = await ParseAsync("<div class='item'><a class='link' href='/product/42'>Buy</a></div>");
        var container = doc.QuerySelector(".item")!;

        var data = SelectorExtractor.ExtractFields(
            container,
            new List<FieldSelector> { Field("url", ".link", FieldAttribute.Href, resolveUrl: true) },
            new Uri("https://example.com/list"));

        Assert.Equal("https://example.com/product/42", data["url"]);
    }

    [Fact]
    public async Task ExtractFields_Src_ReturnsRawAttributeWithoutResolveUrl()
    {
        var doc = await ParseAsync("<div class='item'><img class='thumb' src='/img/1.jpg'/></div>");
        var container = doc.QuerySelector(".item")!;

        var data = SelectorExtractor.ExtractFields(container, new List<FieldSelector> { Field("thumb", ".thumb", FieldAttribute.Src) }, new Uri("https://example.com/list"));

        Assert.Equal("/img/1.jpg", data["thumb"]);
    }

    [Fact]
    public async Task ExtractFields_CustomAttribute_ReadsNamedAttribute()
    {
        var doc = await ParseAsync("<div class='item' data-id='987'></div>");
        var container = doc.QuerySelector(".item")!;

        var data = SelectorExtractor.ExtractFields(
            container,
            new List<FieldSelector> { Field("id", "", FieldAttribute.Attribute, attributeName: "data-id") },
            new Uri("https://example.com/list"));

        Assert.Equal("987", data["id"]);
    }

    [Fact]
    public async Task ExtractFields_MissingOptionalField_ReturnsNull()
    {
        var doc = await ParseAsync("<div class='item'></div>");
        var container = doc.QuerySelector(".item")!;

        var data = SelectorExtractor.ExtractFields(container, new List<FieldSelector> { Field("title", ".missing") }, new Uri("https://example.com/list"));

        Assert.Null(data["title"]);
    }

    [Fact]
    public async Task ExtractFields_MissingRequiredField_Throws()
    {
        var doc = await ParseAsync("<div class='item'></div>");
        var container = doc.QuerySelector(".item")!;

        Assert.Throws<FieldExtractionException>(() =>
            SelectorExtractor.ExtractFields(container, new List<FieldSelector> { Field("title", ".missing", required: true) }, new Uri("https://example.com/list")));
    }
}
