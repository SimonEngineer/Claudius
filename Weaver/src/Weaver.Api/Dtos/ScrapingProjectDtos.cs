using Weaver.Domain;

namespace Weaver.Api.Dtos;

public record FieldSelectorDto(
    Guid? Id,
    string Name,
    string Selector,
    FieldAttribute Attribute,
    string? AttributeName,
    bool ResolveUrl,
    bool IsKey,
    bool Required,
    int Order);

public record ScrapingProjectDto(
    Guid Id,
    string Name,
    string? Description,
    string StartUrl,
    ScrapeMode Mode,
    RenderMode RenderMode,
    string? ItemSelector,
    PaginationStrategy PaginationStrategy,
    string? NextPageSelector,
    string? PageUrlTemplate,
    int MaxPages,
    Dictionary<string, string> CustomHeaders,
    int? DataRetentionDays,
    Guid? RateLimitPolicyId,
    bool IsEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    List<FieldSelectorDto> Fields,
    RunStatus? LastRunStatus = null,
    DateTimeOffset? LastRunAt = null)
{
    public static ScrapingProjectDto FromEntity(ScrapingProject p) => new(
        p.Id, p.Name, p.Description, p.StartUrl, p.Mode, p.RenderMode, p.ItemSelector, p.PaginationStrategy,
        p.NextPageSelector, p.PageUrlTemplate, p.MaxPages, Weaver.Scraping.CustomHeadersParser.Parse(p.CustomHeadersJson),
        p.DataRetentionDays, p.RateLimitPolicyId, p.IsEnabled,
        p.CreatedAt, p.UpdatedAt,
        p.Fields.OrderBy(f => f.Order).Select(f => new FieldSelectorDto(
            f.Id, f.Name, f.Selector, f.Attribute, f.AttributeName, f.ResolveUrl, f.IsKey, f.Required, f.Order)).ToList());
}

public record UpsertScrapingProjectRequest(
    string Name,
    string? Description,
    string StartUrl,
    ScrapeMode Mode,
    RenderMode RenderMode,
    string? ItemSelector,
    PaginationStrategy PaginationStrategy,
    string? NextPageSelector,
    string? PageUrlTemplate,
    int MaxPages,
    Dictionary<string, string>? CustomHeaders,
    int? DataRetentionDays,
    Guid? RateLimitPolicyId,
    bool IsEnabled,
    List<FieldSelectorDto> Fields);

public record ScrapeRunDto(
    Guid Id,
    Guid ScrapingProjectId,
    RunStatus Status,
    TriggerKind TriggeredBy,
    int PagesCrawled,
    int ItemsFound,
    int ItemsChanged,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt)
{
    public static ScrapeRunDto FromEntity(ScrapeRun r) => new(
        r.Id, r.ScrapingProjectId, r.Status, r.TriggeredBy, r.PagesCrawled, r.ItemsFound,
        r.ItemsChanged, r.ErrorMessage, r.CreatedAt, r.StartedAt, r.CompletedAt);
}

public record ScrapedItemDto(Guid Id, string SourceUrl, string ItemKey, Dictionary<string, string?> Data, DateTimeOffset CreatedAt)
{
    public static ScrapedItemDto FromEntity(ScrapedItem i) => new(i.Id, i.SourceUrl, i.ItemKey, i.Data, i.CreatedAt);
}

public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);

public record FieldDiffDto(string? Before, string? After);

public record ItemSnapshotDto(Guid Id, DateTimeOffset CreatedAt, Dictionary<string, string?> Data, Dictionary<string, FieldDiffDto>? Changes);

public record TestExtractionRequest(
    string Url,
    ScrapeMode Mode,
    string? ItemSelector,
    List<FieldSelectorDto> Fields,
    Guid? RateLimitPolicyId,
    Guid? ScrapingProjectId,
    RenderMode RenderMode,
    Dictionary<string, string>? CustomHeaders);
