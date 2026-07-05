using Weaver.Domain;
using Weaver.Infrastructure.Security;

namespace Weaver.Api.Dtos;

public record ProxyConfigDto(bool Enabled, string Protocol, string Host, int Port, string? Username, string? Password)
{
    public static ProxyConfigDto Disabled { get; } = new(false, "http", string.Empty, 0, null, null);

    /// <summary>Decrypts the stored (possibly-encrypted) proxy config back to plaintext for display/editing.</summary>
    public static ProxyConfigDto FromStoredJson(ISensitiveConfigProtector protector, string? proxyConfigJson)
    {
        var decrypted = string.IsNullOrWhiteSpace(proxyConfigJson) ? "{}" : protector.DecryptForUse("scrapingProject.proxy", proxyConfigJson);
        var config = Weaver.Scraping.ProxyConfigParser.Parse(decrypted);
        return new ProxyConfigDto(config.Enabled, config.Protocol, config.Host, config.Port, config.Username, config.Password);
    }

    /// <summary>Serializes and encrypts this DTO's password field for storage.</summary>
    public string ToEncryptedJson(ISensitiveConfigProtector protector)
    {
        var json = Weaver.Scraping.ProxyConfigParser.ToJson(new Weaver.Scraping.ProxyConfig(Enabled, Protocol, Host, Port, Username, Password));
        return protector.EncryptForStorage("scrapingProject.proxy", json);
    }
}

public record FieldTransformDto(string Kind, string? Pattern = null);

public record FieldSelectorDto(
    Guid? Id,
    string Name,
    string Selector,
    FieldAttribute Attribute,
    string? AttributeName,
    bool ResolveUrl,
    bool IsKey,
    bool Required,
    int Order,
    List<FieldTransformDto>? Transforms = null)
{
    public static List<FieldTransformDto> ParseTransforms(string? transformsJson) =>
        Weaver.Scraping.FieldTransforms.Parse(transformsJson).Select(t => new FieldTransformDto(t.Kind, t.Pattern)).ToList();

    public string TransformsToJson() =>
        System.Text.Json.JsonSerializer.Serialize(
            (Transforms ?? new List<FieldTransformDto>()).Select(t => new Weaver.Scraping.FieldTransform(t.Kind, t.Pattern)),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
}

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
    ProxyConfigDto Proxy,
    string? ScheduleCron,
    List<string> StartUrls,
    bool RespectRobotsTxt,
    string? SitemapUrl,
    int CrawlDelayMs,
    string? UserAgent,
    bool ShareEnabled,
    int? DataRetentionDays,
    Guid? RateLimitPolicyId,
    bool IsEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    List<FieldSelectorDto> Fields,
    RunStatus? LastRunStatus = null,
    DateTimeOffset? LastRunAt = null)
{
    public static ScrapingProjectDto FromEntity(ScrapingProject p, ISensitiveConfigProtector protector) => new(
        p.Id, p.Name, p.Description, p.StartUrl, p.Mode, p.RenderMode, p.ItemSelector, p.PaginationStrategy,
        p.NextPageSelector, p.PageUrlTemplate, p.MaxPages, Weaver.Scraping.CustomHeadersParser.Parse(p.CustomHeadersJson),
        ProxyConfigDto.FromStoredJson(protector, p.ProxyConfigJson),
        p.ScheduleCron, ParseStartUrls(p.StartUrlsJson), p.RespectRobotsTxt,
        p.SitemapUrl, p.CrawlDelayMs, p.UserAgent, p.ShareTokenHash != null,
        p.DataRetentionDays, p.RateLimitPolicyId, p.IsEnabled,
        p.CreatedAt, p.UpdatedAt,
        p.Fields.OrderBy(f => f.Order).Select(f => new FieldSelectorDto(
            f.Id, f.Name, f.Selector, f.Attribute, f.AttributeName, f.ResolveUrl, f.IsKey, f.Required, f.Order,
            FieldSelectorDto.ParseTransforms(f.TransformsJson))).ToList());

    private static List<string> ParseStartUrls(string? json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json ?? "[]") ?? new List<string>();
        }
        catch (System.Text.Json.JsonException)
        {
            return new List<string>();
        }
    }
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
    ProxyConfigDto? Proxy,
    int? DataRetentionDays,
    Guid? RateLimitPolicyId,
    bool IsEnabled,
    List<FieldSelectorDto> Fields,
    string? ScheduleCron = null,
    List<string>? StartUrls = null,
    bool RespectRobotsTxt = false,
    string? SitemapUrl = null,
    int CrawlDelayMs = 0,
    string? UserAgent = null);

/// <summary>Portable project definition. Credential-ish values (proxy password, Cookie /
/// Authorization header values) are blanked so the file is safe to share; schedule and share
/// state are deliberately not exported at all.</summary>
public record ScrapingProjectExportDto(
    int WeaverProjectExportVersion,
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
    ProxyConfigDto Proxy,
    List<string> StartUrls,
    bool RespectRobotsTxt,
    string? SitemapUrl,
    int CrawlDelayMs,
    string? UserAgent,
    int? DataRetentionDays,
    List<FieldSelectorDto> Fields)
{
    public const int CurrentVersion = 1;

    private static readonly string[] SensitiveHeaderNames = { "Cookie", "Authorization", "X-Api-Key" };

    public static ScrapingProjectExportDto FromDto(ScrapingProjectDto p)
    {
        var headers = p.CustomHeaders.ToDictionary(
            h => h.Key,
            h => SensitiveHeaderNames.Any(s => s.Equals(h.Key, StringComparison.OrdinalIgnoreCase)) ? string.Empty : h.Value);

        var proxy = p.Proxy with { Password = null };

        return new ScrapingProjectExportDto(
            CurrentVersion, p.Name, p.Description, p.StartUrl, p.Mode, p.RenderMode, p.ItemSelector,
            p.PaginationStrategy, p.NextPageSelector, p.PageUrlTemplate, p.MaxPages, headers, proxy,
            p.StartUrls, p.RespectRobotsTxt, p.SitemapUrl, p.CrawlDelayMs, p.UserAgent, p.DataRetentionDays,
            p.Fields.Select(f => f with { Id = null }).ToList());
    }
}

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
    Dictionary<string, string>? CustomHeaders,
    ProxyConfigDto? Proxy = null);
