namespace Weaver.Scraping;

public record ExtractedItem(string SourceUrl, Dictionary<string, string?> Data);

public record ScrapeResult(int PagesCrawled, IReadOnlyList<ExtractedItem> Items, string? ErrorMessage);
