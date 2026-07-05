using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
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
    private readonly IConnectionMultiplexer _redis;

    public DashboardController(WeaverDbContext db, IConnectionMultiplexer redis)
    {
        _db = db;
        _redis = redis;
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

        var runningScrapes = await _db.ScrapeRuns
            .CountAsync(r => r.ScrapingProject!.OwnerUserId == UserId && (r.Status == RunStatus.Running || r.Status == RunStatus.Pending), ct);
        var runningWorkflows = await _db.WorkflowRuns
            .CountAsync(r => r.Workflow!.OwnerUserId == UserId && (r.Status == RunStatus.Running || r.Status == RunStatus.Pending), ct);

        var recentActivity = await _db.AuditLogEntries
            .Where(e => e.OwnerUserId == UserId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(5)
            .ToListAsync(ct);

        var failedScrapes = await _db.ScrapeRuns
            .Where(r => r.ScrapingProject!.OwnerUserId == UserId && r.Status == RunStatus.Failed)
            .OrderByDescending(r => r.CompletedAt)
            .Take(5)
            .Select(r => new FailedRunDto("scrape", r.ScrapingProjectId, r.ScrapingProject!.Name, r.ErrorMessage, r.CompletedAt ?? r.CreatedAt))
            .ToListAsync(ct);
        var failedWorkflows = await _db.WorkflowRuns
            .Where(r => r.Workflow!.OwnerUserId == UserId && r.Status == RunStatus.Failed)
            .OrderByDescending(r => r.CompletedAt)
            .Take(5)
            .Select(r => new FailedRunDto("workflow", r.WorkflowId, r.Workflow!.Name, r.ErrorMessage, r.CompletedAt ?? r.CreatedAt))
            .ToListAsync(ct);
        var recentFailures = failedScrapes.Concat(failedWorkflows).OrderByDescending(f => f.At).Take(5).ToList();

        return new DashboardStatsDto(
            scrapeRunsToday.Count(s => s == RunStatus.Succeeded),
            scrapeRunsToday.Count(s => s == RunStatus.Failed),
            workflowRunsToday.Count(s => s == RunStatus.Succeeded),
            workflowRunsToday.Count(s => s == RunStatus.Failed),
            runningScrapes,
            runningWorkflows,
            recentActivity.Select(AuditLogEntryDto.FromEntity).ToList(),
            recentFailures);
    }

    /// <summary>Succeeded/failed run counts per day for the last 14 days (UTC), for the usage chart.</summary>
    [HttpGet("runs-per-day")]
    public async Task<ActionResult<RunsPerDayDto>> RunsPerDay(CancellationToken ct)
    {
        var since = DateTimeOffset.UtcNow.Date.AddDays(-13);

        var scrapeRuns = await _db.ScrapeRuns
            .Where(r => r.ScrapingProject!.OwnerUserId == UserId && r.CreatedAt >= since
                && (r.Status == RunStatus.Succeeded || r.Status == RunStatus.Failed))
            .Select(r => new { r.CreatedAt, r.Status })
            .ToListAsync(ct);
        var workflowRuns = await _db.WorkflowRuns
            .Where(r => r.Workflow!.OwnerUserId == UserId && r.CreatedAt >= since
                && (r.Status == RunStatus.Succeeded || r.Status == RunStatus.Failed))
            .Select(r => new { r.CreatedAt, r.Status })
            .ToListAsync(ct);

        List<DailyRunCountDto> Bucket(IEnumerable<(DateTimeOffset At, RunStatus Status)> runs)
        {
            var byDay = runs.GroupBy(r => DateOnly.FromDateTime(r.At.UtcDateTime))
                .ToDictionary(g => g.Key, g => (Ok: g.Count(r => r.Status == RunStatus.Succeeded), Bad: g.Count(r => r.Status == RunStatus.Failed)));
            return Enumerable.Range(0, 14)
                .Select(offset => DateOnly.FromDateTime(since.AddDays(offset)))
                .Select(day => new DailyRunCountDto(day, byDay.GetValueOrDefault(day).Ok, byDay.GetValueOrDefault(day).Bad))
                .ToList();
        }

        return new RunsPerDayDto(
            Bucket(scrapeRuns.Select(r => (r.CreatedAt, r.Status))),
            Bucket(workflowRuns.Select(r => (r.CreatedAt, r.Status))));
    }

    /// <summary>Live worker instances, read from their self-expiring Redis heartbeats.</summary>
    [HttpGet("workers")]
    public ActionResult<List<WorkerInfoDto>> Workers()
    {
        var workers = new List<WorkerInfoDto>();
        var db = _redis.GetDatabase();
        foreach (var endpoint in _redis.GetEndPoints())
        {
            var server = _redis.GetServer(endpoint);
            foreach (var key in server.Keys(pattern: "weaver:worker:*"))
            {
                var value = db.StringGet(key);
                if (!value.HasValue)
                {
                    continue;
                }

                try
                {
                    var record = JsonSerializer.Deserialize<WorkerInfoDto>(value!, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                    if (record is not null)
                    {
                        workers.Add(record);
                    }
                }
                catch (JsonException)
                {
                    // Skip malformed records.
                }
            }
        }

        return workers.OrderBy(w => w.Name).ToList();
    }
}
