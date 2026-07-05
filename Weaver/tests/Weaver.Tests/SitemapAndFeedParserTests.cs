using Weaver.Scraping;
using Weaver.Workflows;
using Xunit;

namespace Weaver.Tests;

public class SitemapAndFeedParserTests
{
    [Fact]
    public void Sitemap_Urlset_ExtractsPageUrls()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
              <url><loc>https://example.com/a</loc></url>
              <url><loc>https://example.com/b</loc><lastmod>2026-01-01</lastmod></url>
              <url><loc>not a url</loc></url>
            </urlset>
            """;
        var result = SitemapParser.Parse(xml);
        Assert.Equal(new[] { "https://example.com/a", "https://example.com/b" }, result.PageUrls);
        Assert.Empty(result.ChildSitemapUrls);
    }

    [Fact]
    public void Sitemap_Index_ReturnsChildSitemaps()
    {
        var xml = """
            <sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
              <sitemap><loc>https://example.com/sitemap-1.xml</loc></sitemap>
              <sitemap><loc>https://example.com/sitemap-2.xml</loc></sitemap>
            </sitemapindex>
            """;
        var result = SitemapParser.Parse(xml);
        Assert.Empty(result.PageUrls);
        Assert.Equal(2, result.ChildSitemapUrls.Count);
    }

    [Fact]
    public void Sitemap_MalformedXml_YieldsEmpty()
    {
        var result = SitemapParser.Parse("<urlset><url>");
        Assert.Empty(result.PageUrls);
        Assert.Empty(result.ChildSitemapUrls);
    }

    [Fact]
    public void Feed_Rss_ParsesItems()
    {
        var xml = """
            <rss version="2.0"><channel><title>Blog</title>
              <item><title>First</title><link>https://example.com/1</link><guid>post-1</guid><pubDate>Mon, 01 Jun 2026 00:00:00 GMT</pubDate><description>Hi</description></item>
              <item><title>Second</title><link>https://example.com/2</link></item>
            </channel></rss>
            """;
        var entries = FeedParser.Parse(xml);
        Assert.Equal(2, entries.Count);
        Assert.Equal("post-1", entries[0].Id);
        Assert.Equal("First", entries[0].Title);
        Assert.Equal("https://example.com/2", entries[1].Id); // falls back to link
    }

    [Fact]
    public void Feed_Atom_ParsesEntries()
    {
        var xml = """
            <feed xmlns="http://www.w3.org/2005/Atom"><title>Blog</title>
              <entry><id>urn:1</id><title>Hello</title><link href="https://example.com/hello"/><updated>2026-06-01T00:00:00Z</updated><summary>hey</summary></entry>
            </feed>
            """;
        var entries = FeedParser.Parse(xml);
        var entry = Assert.Single(entries);
        Assert.Equal("urn:1", entry.Id);
        Assert.Equal("Hello", entry.Title);
        Assert.Equal("https://example.com/hello", entry.Link);
    }

    [Fact]
    public void Feed_MalformedXml_YieldsEmpty()
    {
        Assert.Empty(FeedParser.Parse("<rss><channel>"));
        Assert.Empty(FeedParser.Parse("<html></html>"));
    }
}
