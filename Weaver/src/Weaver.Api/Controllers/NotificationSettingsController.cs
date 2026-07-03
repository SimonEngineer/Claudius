using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Weaver.Domain;
using Weaver.Infrastructure.Persistence;
using Weaver.Infrastructure.Security;

namespace Weaver.Api.Controllers;

public record NotificationSettingsDto(bool NotifyOnScrapeFailure, bool NotifyOnWorkflowFailure, bool EmailEnabled, string? WebhookUrl);

/// <summary>Per-user failure notification preferences. The webhook URL is stored encrypted (it may
/// embed a token) and decrypted back for its owner to edit, same as proxy credentials.</summary>
[ApiController]
[Route("api/notification-settings")]
public class NotificationSettingsController : ControllerBase
{
    private readonly WeaverDbContext _db;
    private readonly ICredentialProtector _protector;

    public NotificationSettingsController(WeaverDbContext db, ICredentialProtector protector)
    {
        _db = db;
        _protector = protector;
    }

    private Guid UserId => User.GetUserId();

    [HttpGet]
    public async Task<ActionResult<NotificationSettingsDto>> Get(CancellationToken ct)
    {
        var settings = await _db.NotificationSettings.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == UserId, ct);
        if (settings is null)
        {
            return new NotificationSettingsDto(false, false, false, null);
        }

        var webhookUrl = string.IsNullOrEmpty(settings.WebhookUrl) ? null : _protector.Decrypt(settings.WebhookUrl);
        return new NotificationSettingsDto(settings.NotifyOnScrapeFailure, settings.NotifyOnWorkflowFailure, settings.EmailEnabled, webhookUrl);
    }

    [HttpPut]
    public async Task<ActionResult<NotificationSettingsDto>> Update(NotificationSettingsDto request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.WebhookUrl) &&
            (!Uri.TryCreate(request.WebhookUrl, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https")))
        {
            return BadRequest("Webhook URL must be an absolute http(s) URL.");
        }

        var settings = await _db.NotificationSettings.FirstOrDefaultAsync(s => s.UserId == UserId, ct);
        if (settings is null)
        {
            settings = new NotificationSettings { UserId = UserId };
            _db.NotificationSettings.Add(settings);
        }

        settings.NotifyOnScrapeFailure = request.NotifyOnScrapeFailure;
        settings.NotifyOnWorkflowFailure = request.NotifyOnWorkflowFailure;
        settings.EmailEnabled = request.EmailEnabled;
        settings.WebhookUrl = string.IsNullOrWhiteSpace(request.WebhookUrl) ? null : _protector.Encrypt(request.WebhookUrl.Trim());
        settings.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return request;
    }
}
