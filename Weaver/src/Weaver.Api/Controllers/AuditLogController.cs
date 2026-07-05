using System.Text;
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
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? resourceType = null, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.AuditLogEntries.Where(e => e.OwnerUserId == UserId);
        if (!string.IsNullOrWhiteSpace(resourceType))
        {
            query = query.Where(e => e.ResourceType == resourceType);
        }
        var totalCount = await query.CountAsync(ct);
        var entries = await query.OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<AuditLogEntryDto>(entries.Select(AuditLogEntryDto.FromEntity).ToList(), totalCount, page, pageSize);
    }

    /// <summary>Downloads the user's entire audit log (not just one page) as CSV.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var entries = await _db.AuditLogEntries.Where(e => e.OwnerUserId == UserId)
            .OrderByDescending(e => e.CreatedAt).ToListAsync(ct);

        var sb = new StringBuilder();
        sb.Append("CreatedAt,Action,ResourceType,ResourceId,ResourceName\n");
        foreach (var e in entries)
        {
            var values = new[] { e.CreatedAt.ToString("O"), e.Action.ToString(), e.ResourceType, e.ResourceId.ToString(), e.ResourceName };
            sb.Append(string.Join(',', values.Select(Escape)));
            sb.Append('\n');
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "activity.csv");
    }

    private static string Escape(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
