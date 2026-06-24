using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Api.Dtos;
using Orchestrator.Infrastructure.Persistence;

namespace Orchestrator.Api.Controllers;

/// <summary>
/// Rolls up the CostUsd/TokensIn/TokensOut that Run already records per engine call into
/// per-project totals and a daily trend, so spend is visible without querying the DB by hand.
/// </summary>
[ApiController]
[Route("api/costs")]
public class CostsController(OrchestratorDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CostSummaryDto>> Get(CancellationToken ct)
    {
        var byProject = (await db.Runs
            .Join(db.Tasks, r => r.TaskId, t => t.Id, (r, t) => new { r, t.ProjectId })
            .Join(db.Projects, x => x.ProjectId, p => p.Id, (x, p) => new { x.r, p.Id, p.Name })
            .GroupBy(x => new { x.Id, x.Name })
            .Select(g => new
            {
                g.Key.Id,
                g.Key.Name,
                CostUsd = g.Sum(x => x.r.CostUsd ?? 0),
                TokensIn = g.Sum(x => x.r.TokensIn ?? 0),
                TokensOut = g.Sum(x => x.r.TokensOut ?? 0),
                RunCount = g.Count()
            })
            .ToListAsync(ct))
            .Select(x => new ProjectCostDto(x.Id, x.Name, x.CostUsd, x.TokensIn, x.TokensOut, x.RunCount))
            .OrderByDescending(x => x.CostUsd)
            .ToList();

        var cutoff = DateTimeOffset.UtcNow.Date.AddDays(-29);
        var recentRuns = await db.Runs
            .Where(r => r.StartedAt >= cutoff)
            .Select(r => new { r.StartedAt, r.CostUsd })
            .ToListAsync(ct);

        var last30Days = recentRuns
            .GroupBy(r => DateOnly.FromDateTime(r.StartedAt.UtcDateTime.Date))
            .Select(g => new DailyCostDto(g.Key, g.Sum(r => r.CostUsd ?? 0)))
            .OrderBy(d => d.Date)
            .ToList();

        return Ok(new CostSummaryDto(
            byProject.Sum(p => p.CostUsd),
            byProject.Sum(p => p.TokensIn),
            byProject.Sum(p => p.TokensOut),
            byProject,
            last30Days));
    }
}
