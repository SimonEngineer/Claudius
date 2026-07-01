using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Weaver.Domain;
using Weaver.Infrastructure.Persistence;

namespace Weaver.Api.Controllers;

public record RateLimitPolicyDto(Guid Id, string Name, RateLimitKeyScope KeyScope, string? CustomKeyTemplate, int PermitLimit, int WindowSeconds, int BurstCapacity)
{
    public static RateLimitPolicyDto FromEntity(RateLimitPolicy p) => new(p.Id, p.Name, p.KeyScope, p.CustomKeyTemplate, p.PermitLimit, p.WindowSeconds, p.BurstCapacity);
}

public record UpsertRateLimitPolicyRequest(string Name, RateLimitKeyScope KeyScope, string? CustomKeyTemplate, int PermitLimit, int WindowSeconds, int BurstCapacity);

[ApiController]
[Route("api/rate-limit-policies")]
public class RateLimitPoliciesController : ControllerBase
{
    private readonly WeaverDbContext _db;

    public RateLimitPoliciesController(WeaverDbContext db)
    {
        _db = db;
    }

    private Guid UserId => User.GetUserId();

    [HttpGet]
    public async Task<ActionResult<List<RateLimitPolicyDto>>> List(CancellationToken ct)
    {
        var policies = await _db.RateLimitPolicies.Where(p => p.OwnerUserId == UserId).OrderBy(p => p.Name).ToListAsync(ct);
        return policies.Select(RateLimitPolicyDto.FromEntity).ToList();
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

        await _db.SaveChangesAsync(ct);
        return RateLimitPolicyDto.FromEntity(policy);
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
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
