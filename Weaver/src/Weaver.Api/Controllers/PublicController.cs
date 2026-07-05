using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Weaver.Api.Dtos;
using Weaver.Infrastructure.Persistence;

namespace Weaver.Api.Controllers;

/// <summary>
/// Anonymous, read-only access to a project's scraped items via an unguessable share token. Only
/// the token's SHA-256 hash is stored, so a database leak doesn't leak working share links; the
/// raw token exists only in the link the owner copied.
/// </summary>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
public class PublicController : ControllerBase
{
    private readonly WeaverDbContext _db;

    public PublicController(WeaverDbContext db)
    {
        _db = db;
    }

    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    [HttpGet("items/{token}")]
    public async Task<ActionResult<object>> Items(string token, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128)
        {
            return NotFound();
        }

        var hash = HashToken(token);
        var project = await _db.ScrapingProjects.AsNoTracking().FirstOrDefaultAsync(p => p.ShareTokenHash == hash, ct);
        if (project is null)
        {
            return NotFound();
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.ScrapedItems.AsNoTracking().Where(i => i.ScrapingProjectId == project.Id);
        var totalCount = await query.CountAsync(ct);
        var items = await query.OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        // Intentionally a trimmed shape: no project config, no ids beyond what a viewer needs.
        return new
        {
            projectName = project.Name,
            totalCount,
            page,
            pageSize,
            items = items.Select(i => new { i.SourceUrl, i.Data, i.CreatedAt }).ToList(),
        };
    }
}
