using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Weaver.Api.Dtos;
using Weaver.Domain;
using Weaver.Infrastructure.Auth;
using Weaver.Infrastructure.Persistence;

namespace Weaver.Api.Controllers;

/// <summary>Manages personal API keys -- long-lived bearer credentials for triggering scrapes and
/// workflows from external scripts/cron jobs without an interactive login flow.</summary>
[ApiController]
[Route("api/api-keys")]
public class ApiKeysController : ControllerBase
{
    private readonly WeaverDbContext _db;
    private readonly IApiKeyService _apiKeyService;

    public ApiKeysController(WeaverDbContext db, IApiKeyService apiKeyService)
    {
        _db = db;
        _apiKeyService = apiKeyService;
    }

    private Guid UserId => User.GetUserId();

    [HttpGet]
    public async Task<ActionResult<List<ApiKeyDto>>> List(CancellationToken ct)
    {
        var keys = await _db.ApiKeys.Where(k => k.OwnerUserId == UserId).OrderByDescending(k => k.CreatedAt).ToListAsync(ct);
        return keys.Select(ApiKeyDto.FromEntity).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<CreatedApiKeyDto>> Create(CreateApiKeyRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("A name is required.");
        }

        var (plaintext, prefix, hash) = _apiKeyService.GenerateKey();
        var apiKey = new ApiKey
        {
            OwnerUserId = UserId,
            Name = request.Name,
            KeyPrefix = prefix,
            HashedKey = hash,
            ExpiresAt = request.ExpiresAt,
        };

        _db.ApiKeys.Add(apiKey);
        await _db.SaveChangesAsync(ct);

        return new CreatedApiKeyDto(apiKey.Id, apiKey.Name, apiKey.KeyPrefix, plaintext, apiKey.CreatedAt, apiKey.ExpiresAt);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var apiKey = await _db.ApiKeys.FirstOrDefaultAsync(k => k.Id == id && k.OwnerUserId == UserId, ct);
        if (apiKey is null)
        {
            return NotFound();
        }

        _db.ApiKeys.Remove(apiKey);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
