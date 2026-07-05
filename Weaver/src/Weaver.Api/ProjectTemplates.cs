using Weaver.Api.Dtos;
using Weaver.Domain;

namespace Weaver.Api;

/// <summary>
/// Curated starter projects against public scraping sandboxes, expressed in the same portable
/// format as a project export so creation just goes through the import path.
/// </summary>
public static class ProjectTemplates
{
    public record Template(string Key, ScrapingProjectExportDto Definition);

    private static ScrapingProjectExportDto Define(
        string name, string description, string startUrl, string itemSelector,
        PaginationStrategy pagination, string? nextPageSelector, List<FieldSelectorDto> fields) => new(
        ScrapingProjectExportDto.CurrentVersion, name, description, startUrl, ScrapeMode.List, RenderMode.Http,
        itemSelector, pagination, nextPageSelector, null, 3,
        new Dictionary<string, string>(), ProxyConfigDto.Disabled,
        new List<string>(), true, null, 500, null, null, fields);

    private static FieldSelectorDto Field(string name, string selector, FieldAttribute attribute = FieldAttribute.Text,
        bool resolveUrl = false, bool isKey = false, int order = 0) =>
        new(null, name, selector, attribute, null, resolveUrl, isKey, false, order);

    public static readonly IReadOnlyList<Template> All = new List<Template>
    {
        new("quotes",
            Define(
                "Quotes to Scrape (example)",
                "Quotes with author and tags from quotes.toscrape.com, a site built for practicing scraping.",
                "https://quotes.toscrape.com/", ".quote",
                PaginationStrategy.NextLinkSelector, "li.next a",
                new List<FieldSelectorDto>
                {
                    Field("text", ".text", isKey: true, order: 0),
                    Field("author", ".author", order: 1),
                    Field("tags", ".tags", order: 2),
                })),
        new("books",
            Define(
                "Books to Scrape (example)",
                "Book titles and prices from books.toscrape.com, a site built for practicing scraping.",
                "https://books.toscrape.com/", "article.product_pod",
                PaginationStrategy.NextLinkSelector, "li.next a",
                new List<FieldSelectorDto>
                {
                    Field("title", "h3 a", FieldAttribute.Attribute, order: 0) with { AttributeName = "title", IsKey = true },
                    Field("price", ".price_color", order: 1),
                    Field("url", "h3 a", FieldAttribute.Href, resolveUrl: true, order: 2),
                })),
        new("hackernews",
            Define(
                "Hacker News front page (example)",
                "Story titles, links and scores from news.ycombinator.com. Be considerate: keep the schedule slow.",
                "https://news.ycombinator.com/", "tr.athing",
                PaginationStrategy.None, null,
                new List<FieldSelectorDto>
                {
                    Field("title", ".titleline > a", isKey: true, order: 0),
                    Field("url", ".titleline > a", FieldAttribute.Href, resolveUrl: true, order: 1),
                })),
    };
}
