using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Weaver.Api.Dtos;
using Weaver.Infrastructure.Persistence;

namespace Weaver.Api.Controllers;

/// <summary>Read-only view of what this user has created/updated/deleted -- scraping projects,
/// workflows, and rate limit policies -- most recent first.</summary>
[ApiController]
[Route("api/audit-log")]
public class AuditLogController : ControllerBase
{
    private readonly WeaverDbContext _db;

    public AuditLogController(WeaverDbContext db)
    {
        _db = db;
    }

    private Guid UserId => User.GetUserId();

    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditLogEntryDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.AuditLogEntries.Where(e => e.OwnerUserId == UserId);
        var totalCount = await query.CountAsync(ct);
        var entries = await query.OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<AuditLogEntryDto>(entries.Select(AuditLogEntryDto.FromEntity).ToList(), totalCount, page, pageSize);
    }
}
