using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Weaver.Infrastructure.Persistence;

namespace Weaver.Api.Controllers;

[ApiController]
[Route("api")]
public class MetaController : ControllerBase
{
    private readonly WeaverDbContext _db;

    public MetaController(WeaverDbContext db)
    {
        _db = db;
    }

    private Guid UserId => User.GetUserId();

    [HttpGet("version")]
    [AllowAnonymous]
    public ActionResult<object> Version()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        return new { version };
    }

    /// <summary>Member-since date and resource counts for the Account page overview.</summary>
    [HttpGet("account/overview")]
    public async Task<ActionResult<object>> AccountOverview(CancellationToken ct)
    {
        var createdAt = await _db.Users.Where(u => u.Id == UserId).Select(u => (DateTimeOffset?)u.CreatedAt).FirstOrDefaultAsync(ct);
        if (createdAt is null)
        {
            return NotFound();
        }

        return new
        {
            createdAt,
            projects = await _db.ScrapingProjects.CountAsync(p => p.OwnerUserId == UserId, ct),
            workflows = await _db.Workflows.CountAsync(w => w.OwnerUserId == UserId, ct),
            credentials = await _db.Credentials.CountAsync(c => c.OwnerUserId == UserId, ct),
            apiKeys = await _db.ApiKeys.CountAsync(k => k.OwnerUserId == UserId, ct),
        };
    }
}
