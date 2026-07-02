using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Weaver.Api.Dtos;
using Weaver.Domain;
using Weaver.Infrastructure.Persistence;

namespace Weaver.Api.Controllers;

/// <summary>Aggregate stats + recent activity for the landing page, scoped to the current user.</summary>
[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly WeaverDbContext _db;

    public DashboardController(WeaverDbContext db)
    {
        _db = db;
    }

    private Guid UserId => User.GetUserId();

    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> Stats(CancellationToken ct)
    {
        var todayStart = DateTimeOffset.UtcNow.Date;

        var scrapeRunsToday = await _db.ScrapeRuns
            .Where(r => r.ScrapingProject!.OwnerUserId == UserId && r.CreatedAt >= todayStart)
            .Select(r => r.Status)
            .ToListAsync(ct);

        var workflowRunsToday = await _db.WorkflowRuns
            .Where(r => r.Workflow!.OwnerUserId == UserId && r.CreatedAt >= todayStart)
            .Select(r => r.Status)
            .ToListAsync(ct);

        var recentActivity = await _db.AuditLogEntries
            .Where(e => e.OwnerUserId == UserId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(5)
            .ToListAsync(ct);

        return new DashboardStatsDto(
            scrapeRunsToday.Count(s => s == RunStatus.Succeeded),
            scrapeRunsToday.Count(s => s == RunStatus.Failed),
            workflowRunsToday.Count(s => s == RunStatus.Succeeded),
            workflowRunsToday.Count(s => s == RunStatus.Failed),
            recentActivity.Select(AuditLogEntryDto.FromEntity).ToList());
    }
}
