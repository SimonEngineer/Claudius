using Microsoft.EntityFrameworkCore;
using Weaver.Domain;
using Weaver.Infrastructure.Persistence;

namespace Weaver.Workflows.Scripting;

public class ScriptDbAccess : IScriptDbAccess
{
    private readonly WeaverDbContext _db;

    public ScriptDbAccess(WeaverDbContext db)
    {
        _db = db;
    }

    public Task<ScrapingProject?> GetScrapingProjectAsync(Guid scrapingProjectId) =>
        _db.ScrapingProjects.AsNoTracking().Include(p => p.Fields).FirstOrDefaultAsync(p => p.Id == scrapingProjectId);

    public Task<List<ScrapedItem>> GetRecentItemsAsync(Guid scrapingProjectId, int count = 50) =>
        _db.ScrapedItems.AsNoTracking()
            .Where(i => i.ScrapingProjectId == scrapingProjectId)
            .OrderByDescending(i => i.CreatedAt)
            .Take(count)
            .ToListAsync();

    public Task<List<ScrapedItem>> GetItemHistoryAsync(Guid scrapingProjectId, string itemKey, int count = 50) =>
        _db.ScrapedItems.AsNoTracking()
            .Where(i => i.ScrapingProjectId == scrapingProjectId && i.ItemKey == itemKey)
            .OrderByDescending(i => i.CreatedAt)
            .Take(count)
            .ToListAsync();

    public Task<ScrapeRun?> GetLatestRunAsync(Guid scrapingProjectId) =>
        _db.ScrapeRuns.AsNoTracking()
            .Where(r => r.ScrapingProjectId == scrapingProjectId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();
}
