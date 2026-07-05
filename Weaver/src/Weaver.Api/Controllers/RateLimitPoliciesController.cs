using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Weaver.Domain;
using Weaver.Infrastructure.Auditing;
using Weaver.Infrastructure.Persistence;

namespace Weaver.Api.Controllers;

public record RateLimitPolicyDto(Guid Id, string Name, RateLimitKeyScope KeyScope, string? CustomKeyTemplate, int PermitLimit, int WindowSeconds, int BurstCapacity, int UsedByProjects = 0)
{
    public static RateLimitPolicyDto FromEntity(RateLimitPolicy p, int usedByProjects = 0) =>
        new(p.Id, p.Name, p.KeyScope, p.CustomKeyTemplate, p.PermitLimit, p.WindowSeconds, p.BurstCapacity, usedByProjects);
}

public record UpsertRateLimitPolicyRequest(string Name, RateLimitKeyScope KeyScope, string? CustomKeyTemplate, int PermitLimit, int WindowSeconds, int BurstCapacity);

[ApiController]
[Route("api/rate-limit-policies")]
public class RateLimitPoliciesController : ControllerBase
{
    private readonly WeaverDbContext _db;
    private readonly IAuditLogger _auditLogger;

    public RateLimitPoliciesController(WeaverDbContext db, IAuditLogger auditLogger)
    {
        _db = db;
        _auditLogger = auditLogger;
    }

    private Guid UserId => User.GetUserId();

    [HttpGet]
    public async Task<ActionResult<List<RateLimitPolicyDto>>> List(CancellationToken ct)
    {
        var policies = await _db.RateLimitPolicies.Where(p => p.OwnerUserId == UserId).OrderBy(p => p.Name).ToListAsync(ct);
        var usage = await _db.ScrapingProjects
            .Where(p => p.OwnerUserId == UserId && p.RateLimitPolicyId != null)
            .GroupBy(p => p.RateLimitPolicyId!.Value)
            .Select(g => new { PolicyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PolicyId, x => x.Count, ct);
        return policies.Select(p => RateLimitPolicyDto.FromEntity(p, usage.GetValueOrDefault(p.Id))).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<RateLimitPolicyDto>> Create(UpsertRateLimitPolicyRequest request, CancellationToken ct)
    {
        var policy = new RateLimitPolicy
        {
            OwnerUserId = UserId,
            Name = request.Name,
            KeyScope = request.KeyScope,
            CustomKeyTemplate = request.CustomKeyTemplate,
            PermitLimit = request.PermitLimit,
            WindowSeconds = request.WindowSeconds,
            BurstCapacity = request.BurstCapacity,
        };
        _db.RateLimitPolicies.Add(policy);
        _auditLogger.Record(UserId, AuditAction.Created, "RateLimitPolicy", policy.Id, policy.Name);
        await _db.SaveChangesAsync(ct);
        return RateLimitPolicyDto.FromEntity(policy);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RateLimitPolicyDto>> Update(Guid id, UpsertRateLimitPolicyRequest request, CancellationToken ct)
    {
        var policy = await _db.RateLimitPolicies.FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == UserId, ct);
        if (policy is null)
        {
            return NotFound();
        }

        policy.Name = request.Name;
        policy.KeyScope = request.KeyScope;
        policy.CustomKeyTemplate = request.CustomKeyTemplate;
        policy.PermitLimit = request.PermitLimit;
        policy.WindowSeconds = request.WindowSeconds;
        policy.BurstCapacity = request.BurstCapacity;

        _auditLogger.Record(UserId, AuditAction.Updated, "RateLimitPolicy", policy.Id, policy.Name);
        await _db.SaveChangesAsync(ct);
        return RateLimitPolicyDto.FromEntity(policy);
    }

    [HttpPost("{id:guid}/duplicate")]
    public async Task<ActionResult<RateLimitPolicyDto>> Duplicate(Guid id, CancellationToken ct)
    {
        var source = await _db.RateLimitPolicies.FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == UserId, ct);
        if (source is null)
        {
            return NotFound();
        }

        var copy = new RateLimitPolicy
        {
            OwnerUserId = UserId,
            Name = $"{source.Name} (Copy)",
            KeyScope = source.KeyScope,
            CustomKeyTemplate = source.CustomKeyTemplate,
            PermitLimit = source.PermitLimit,
            WindowSeconds = source.WindowSeconds,
            BurstCapacity = source.BurstCapacity,
        };
        _db.RateLimitPolicies.Add(copy);
        _auditLogger.Record(UserId, AuditAction.Created, "RateLimitPolicy", copy.Id, copy.Name);
        await _db.SaveChangesAsync(ct);
        return RateLimitPolicyDto.FromEntity(copy);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var policy = await _db.RateLimitPolicies.FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == UserId, ct);
        if (policy is null)
        {
            return NotFound();
        }

        _db.RateLimitPolicies.Remove(policy);
        _auditLogger.Record(UserId, AuditAction.Deleted, "RateLimitPolicy", policy.Id, policy.Name);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
