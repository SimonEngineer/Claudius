using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Domain;
using Orchestrator.Infrastructure.Persistence;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/audit-log")]
public class AuditLogController(OrchestratorDbContext db) : ControllerBase
{
    private const int MaxEntries = 200;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AuditLogEntry>>> List([FromQuery] Guid? projectId, CancellationToken ct)
    {
        var query = db.AuditLogEntries.AsQueryable();
        if (projectId is { } pid)
        {
            query = query.Where(a => a.ProjectId == pid);
        }

        var entries = await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(MaxEntries)
            .ToListAsync(ct);
        return Ok(entries);
    }
}
