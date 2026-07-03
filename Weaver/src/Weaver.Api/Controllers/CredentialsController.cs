using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Weaver.Domain;
using Weaver.Infrastructure.Auditing;
using Weaver.Infrastructure.Persistence;
using Weaver.Infrastructure.Security;

namespace Weaver.Api.Controllers;

public record CredentialDto(Guid Id, string Name, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static CredentialDto FromEntity(Credential c) => new(c.Id, c.Name, c.CreatedAt, c.UpdatedAt);
}

public record CreateCredentialRequest(string Name, string Value);

public record UpdateCredentialRequest(string Value);

/// <summary>
/// Named secrets referenced from workflow node configs as {{secrets.NAME}}. The value is
/// write-only: it's encrypted at rest and never returned by any endpoint after being set --
/// to change it you overwrite it.
/// </summary>
[ApiController]
[Route("api/credentials")]
public class CredentialsController : ControllerBase
{
    private readonly WeaverDbContext _db;
    private readonly ICredentialProtector _protector;
    private readonly IAuditLogger _auditLogger;

    public CredentialsController(WeaverDbContext db, ICredentialProtector protector, IAuditLogger auditLogger)
    {
        _db = db;
        _protector = protector;
        _auditLogger = auditLogger;
    }

    private Guid UserId => User.GetUserId();

    [HttpGet]
    public async Task<ActionResult<List<CredentialDto>>> List(CancellationToken ct)
    {
        var credentials = await _db.Credentials.Where(c => c.OwnerUserId == UserId).OrderBy(c => c.Name).ToListAsync(ct);
        return credentials.Select(CredentialDto.FromEntity).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<CredentialDto>> Create(CreateCredentialRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (name.Length == 0 || !name.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.'))
        {
            return BadRequest("Name must be non-empty and use only letters, digits, '_', '-' or '.' (it's referenced as {{secrets.NAME}}).");
        }

        if (string.IsNullOrEmpty(request.Value))
        {
            return BadRequest("Value must not be empty.");
        }

        if (await _db.Credentials.AnyAsync(c => c.OwnerUserId == UserId && c.Name == name, ct))
        {
            return Conflict($"A credential named '{name}' already exists.");
        }

        var credential = new Credential
        {
            OwnerUserId = UserId,
            Name = name,
            EncryptedValue = _protector.Encrypt(request.Value),
        };
        _db.Credentials.Add(credential);
        _auditLogger.Record(UserId, AuditAction.Created, "Credential", credential.Id, credential.Name);
        await _db.SaveChangesAsync(ct);
        return CredentialDto.FromEntity(credential);
    }

    /// <summary>Overwrites the secret value (the only way to "read back" a lost secret is to set a new one).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CredentialDto>> Update(Guid id, UpdateCredentialRequest request, CancellationToken ct)
    {
        var credential = await _db.Credentials.FirstOrDefaultAsync(c => c.Id == id && c.OwnerUserId == UserId, ct);
        if (credential is null)
        {
            return NotFound();
        }

        if (string.IsNullOrEmpty(request.Value))
        {
            return BadRequest("Value must not be empty.");
        }

        credential.EncryptedValue = _protector.Encrypt(request.Value);
        credential.UpdatedAt = DateTimeOffset.UtcNow;
        _auditLogger.Record(UserId, AuditAction.Updated, "Credential", credential.Id, credential.Name);
        await _db.SaveChangesAsync(ct);
        return CredentialDto.FromEntity(credential);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var credential = await _db.Credentials.FirstOrDefaultAsync(c => c.Id == id && c.OwnerUserId == UserId, ct);
        if (credential is null)
        {
            return NotFound();
        }

        _db.Credentials.Remove(credential);
        _auditLogger.Record(UserId, AuditAction.Deleted, "Credential", credential.Id, credential.Name);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
