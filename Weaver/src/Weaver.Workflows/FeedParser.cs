using System.Xml.Linq;

namespace Weaver.Workflows;

public record FeedEntry(string Id, string Title, string? Link, string? Published, string? Summary);

/// <summary>
/// Minimal RSS 2.0 / Atom parser for the feed trigger: enough to give each entry a stable identity
/// (guid/id, falling back to link, falling back to title) plus the fields a workflow payload wants.
/// Malformed XML yields an empty list rather than throwing.
/// </summary>
public static class FeedParser
{
    public static List<FeedEntry> Parse(string xml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return new List<FeedEntry>();
        }

        var root = document.Root;
        if (root is null)
        {
            return new List<FeedEntry>();
        }

        return root.Name.LocalName switch
        {
            "rss" => ParseRss(root),
            "feed" => ParseAtom(root),
            _ => new List<FeedEntry>(),
        };
    }

    private static List<FeedEntry> ParseRss(XElement rss)
    {
        var channel = rss.Elements().FirstOrDefault(e => e.Name.LocalName == "channel");
        if (channel is null)
        {
            return new List<FeedEntry>();
        }

        return channel.Elements().Where(e => e.Name.LocalName == "item").Select(item =>
        {
            string? Get(string name) => item.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value.Trim();
            var link = Get("link");
            var title = Get("title") ?? string.Empty;
            var id = Get("guid") ?? link ?? title;
            return new FeedEntry(id, title, link, Get("pubDate"), Get("description"));
        }).Where(e => !string.IsNullOrEmpty(e.Id)).ToList();
    }

    private static List<FeedEntry> ParseAtom(XElement feed)
    {
        return feed.Elements().Where(e => e.Name.LocalName == "entry").Select(entry =>
        {
            string? Get(string name) => entry.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value.Trim();
            var link = entry.Elements().FirstOrDefault(e => e.Name.LocalName == "link")?.Attribute("href")?.Value;
            var title = Get("title") ?? string.Empty;
            var id = Get("id") ?? link ?? title;
            return new FeedEntry(id, title, link, Get("updated") ?? Get("published"), Get("summary") ?? Get("content"));
        }).Where(e => !string.IsNullOrEmpty(e.Id)).ToList();
    }
}
