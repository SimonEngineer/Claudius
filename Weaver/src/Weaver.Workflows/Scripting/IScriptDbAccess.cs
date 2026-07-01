using Weaver.Domain;

namespace Weaver.Workflows.Scripting;

/// <summary>
/// Read-only surface handed to Code Block scripts. Deliberately narrow (no raw DbContext, no
/// write methods) so a script can pull context about a project's history without being able to
/// corrupt the database it's running alongside.
/// </summary>
public interface IScriptDbAccess
{
    Task<ScrapingProject?> GetScrapingProjectAsync(Guid scrapingProjectId);
    Task<List<ScrapedItem>> GetRecentItemsAsync(Guid scrapingProjectId, int count = 50);
    Task<List<ScrapedItem>> GetItemHistoryAsync(Guid scrapingProjectId, string itemKey, int count = 50);
    Task<ScrapeRun?> GetLatestRunAsync(Guid scrapingProjectId);
}
