namespace Weaver.Domain;

public enum ScrapeMode
{
    SingleItem,
    List
}

public enum RenderMode
{
    /// <summary>Plain HTTP GET + HTML parse. Fast, no browser required, but sees only server-rendered markup.</summary>
    Http,

    /// <summary>Renders the page in headless Chromium (via Playwright) first, so client-side/JS-rendered content is present.</summary>
    Playwright
}

public enum FieldAttribute
{
    Text,
    Html,
    Href,
    Src,
    Attribute
}

public enum PaginationStrategy
{
    None,
    NextLinkSelector,
    UrlPattern
}

public enum RateLimitKeyScope
{
    PerHost,
    PerUrl,
    PerProject,
    Custom
}

public enum RunStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Cancelled
}

public enum TriggerKind
{
    Manual,
    Schedule,
    Http,
    Event,
    Code
}

public enum ScrapeJobStatus
{
    Queued,
    Leased,
    Completed,
    Failed
}
