using Microsoft.EntityFrameworkCore;
using Weaver.Domain;
using Weaver.Infrastructure.Persistence;

namespace Weaver.Worker;

/// <summary>
/// Ticks hourly and, for every project with a DataRetentionDays limit set, deletes finished scrape
/// runs older than that limit. Deleting a ScrapeRun cascades (at the database level) to its
/// ScrapedItems, so there's nothing extra to clean up there. Runs still Pending/Running are never
/// touched regardless of age.
/// </summary>
public class RetentionCleanupService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RetentionCleanupService> _logger;

    public RetentionCleanupService(IServiceScopeFactory scopeFactory, ILogger<RetentionCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Retention cleanup tick failed");
            }

            await Task.Delay(TickInterval, stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeaverDbContext>();

        var projects = await db.ScrapingProjects
            .Where(p => p.DataRetentionDays != null)
            .Select(p => new { p.Id, p.Name, p.DataRetentionDays })
            .ToListAsync(ct);

        foreach (var project in projects)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-project.DataRetentionDays!.Value);
            var deleted = await db.ScrapeRuns
                .Where(r => r.ScrapingProjectId == project.Id && r.CreatedAt < cutoff
                    && (r.Status == RunStatus.Succeeded || r.Status == RunStatus.Failed || r.Status == RunStatus.Cancelled))
                .ExecuteDeleteAsync(ct);

            if (deleted > 0)
            {
                _logger.LogInformation(
                    "Retention cleanup purged {Count} scrape run(s) (and their items) for project {ProjectName}, older than {Days}d",
                    deleted, project.Name, project.DataRetentionDays);
            }
        }
    }
}
