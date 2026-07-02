namespace Weaver.Domain;

public class ScrapingProject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string StartUrl { get; set; } = string.Empty;

    public ScrapeMode Mode { get; set; } = ScrapeMode.List;
    public RenderMode RenderMode { get; set; } = RenderMode.Http;

    /// <summary>CSS selector for the repeating item container. Required when Mode == List.</summary>
    public string? ItemSelector { get; set; }

    public PaginationStrategy PaginationStrategy { get; set; } = PaginationStrategy.None;

    /// <summary>CSS selector for the "next page" link/button, relative to the document.</summary>
    public string? NextPageSelector { get; set; }

    /// <summary>URL template with a {page} placeholder, used when PaginationStrategy == UrlPattern.</summary>
    public string? PageUrlTemplate { get; set; }

    public int MaxPages { get; set; } = 20;

    /// <summary>
    /// Extra HTTP headers sent with every request this project makes (JSON object of name/value
    /// pairs), for sites that need an API key, an auth token, or a specific Accept-Language --
    /// or, via a header literally named "Cookie", a fixed session for scraping behind a login.
    /// </summary>
    public string CustomHeadersJson { get; set; } = "{}";

    /// <summary>Outbound HTTP/SOCKS5 proxy this project's requests are routed through (JSON-encoded
    /// ProxyConfig; the password field is encrypted at rest). Null/disabled means no proxy.</summary>
    public string ProxyConfigJson { get; set; } = "{}";

    /// <summary>How many days to keep completed scrape runs and their items before a background job
    /// purges them. Null means keep forever.</summary>
    public int? DataRetentionDays { get; set; }

    public Guid? RateLimitPolicyId { get; set; }
    public RateLimitPolicy? RateLimitPolicy { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<FieldSelector> Fields { get; set; } = new();
    public List<ScrapeRun> Runs { get; set; } = new();
}

public class FieldSelector
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScrapingProjectId { get; set; }

    /// <summary>Output key, e.g. "price", "title", "detailUrl".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>CSS selector, relative to the item container (or document root in SingleItem mode).</summary>
    public string Selector { get; set; } = string.Empty;

    public FieldAttribute Attribute { get; set; } = FieldAttribute.Text;

    /// <summary>Only used when Attribute == Attribute, e.g. "data-id".</summary>
    public string? AttributeName { get; set; }

    /// <summary>If true, relative URLs are resolved to absolute using the page URL.</summary>
    public bool ResolveUrl { get; set; }

    /// <summary>If true, this field is used to compute the item's stable identity across runs (change detection).</summary>
    public bool IsKey { get; set; }

    public bool Required { get; set; }
    public int Order { get; set; }
}
