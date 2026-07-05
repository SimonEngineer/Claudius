using System.Xml.Linq;

namespace Weaver.Scraping;

/// <summary>
/// Extracts page URLs from a sitemap.xml document. Handles plain urlset sitemaps and (one level
/// of) sitemap index files by returning the child sitemap URLs marked as indexes, which the caller
/// may fetch in turn. Malformed XML yields an empty list rather than throwing.
/// </summary>
public static class SitemapParser
{
    public record SitemapResult(List<string> PageUrls, List<string> ChildSitemapUrls);

    public static SitemapResult Parse(string xml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return new SitemapResult(new List<string>(), new List<string>());
        }

        var root = document.Root;
        if (root is null)
        {
            return new SitemapResult(new List<string>(), new List<string>());
        }

        // Match on local names so any sitemap namespace version works.
        var locs = root.Elements()
            .Where(e => e.Name.LocalName is "url" or "sitemap")
            .Select(e => (IsIndex: e.Name.LocalName == "sitemap",
                          Loc: e.Elements().FirstOrDefault(c => c.Name.LocalName == "loc")?.Value.Trim()))
            .Where(x => !string.IsNullOrWhiteSpace(x.Loc) && Uri.TryCreate(x.Loc, UriKind.Absolute, out _))
            .ToList();

        return new SitemapResult(
            locs.Where(x => !x.IsIndex).Select(x => x.Loc!).ToList(),
            locs.Where(x => x.IsIndex).Select(x => x.Loc!).ToList());
    }
}
