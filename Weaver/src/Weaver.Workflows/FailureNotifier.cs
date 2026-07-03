using System.Net.Http.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Weaver.Infrastructure.Persistence;
using Weaver.Infrastructure.Realtime;
using Weaver.Infrastructure.Security;
using Weaver.Workflows.Nodes;

namespace Weaver.Workflows;

public class FailureNotifier : IFailureNotifier
{
    private readonly WeaverDbContext _db;
    private readonly IOptions<SmtpOptions> _smtpOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICredentialProtector _protector;
    private readonly ILogger<FailureNotifier> _logger;

    public FailureNotifier(
        WeaverDbContext db,
        IOptions<SmtpOptions> smtpOptions,
        IHttpClientFactory httpClientFactory,
        ICredentialProtector protector,
        ILogger<FailureNotifier> logger)
    {
        _db = db;
        _smtpOptions = smtpOptions;
        _httpClientFactory = httpClientFactory;
        _protector = protector;
        _logger = logger;
    }

    public Task NotifyScrapeFailureAsync(Guid ownerUserId, string projectName, Guid runId, string? error, CancellationToken cancellationToken = default) =>
        NotifyAsync(ownerUserId, "scrape", projectName, runId, error, s => s.NotifyOnScrapeFailure, cancellationToken);

    public Task NotifyWorkflowFailureAsync(Guid ownerUserId, string workflowName, Guid runId, string? error, CancellationToken cancellationToken = default) =>
        NotifyAsync(ownerUserId, "workflow", workflowName, runId, error, s => s.NotifyOnWorkflowFailure, cancellationToken);

    private async Task NotifyAsync(
        Guid ownerUserId, string kind, string resourceName, Guid runId, string? error,
        Func<Domain.NotificationSettings, bool> isEnabled, CancellationToken cancellationToken)
    {
        try
        {
            var settings = await _db.NotificationSettings.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == ownerUserId, cancellationToken);
            if (settings is null || !isEnabled(settings))
            {
                return;
            }

            var subject = $"Weaver: {kind} \"{resourceName}\" failed";
            var body = $"{char.ToUpperInvariant(kind[0])}{kind[1..]} \"{resourceName}\" failed at {DateTimeOffset.UtcNow:u}.\n\nRun id: {runId}\nError: {error ?? "(no error message)"}";

            if (settings.EmailEnabled)
            {
                await TrySendEmailAsync(ownerUserId, subject, body, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(settings.WebhookUrl))
            {
                await TryPostWebhookAsync(settings.WebhookUrl, kind, resourceName, runId, error, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failure notification for {Kind} run {RunId} could not be dispatched", kind, runId);
        }
    }

    private async Task TrySendEmailAsync(Guid ownerUserId, string subject, string body, CancellationToken cancellationToken)
    {
        try
        {
            var email = await _db.Users.Where(u => u.Id == ownerUserId).Select(u => u.Email).FirstOrDefaultAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(email))
            {
                return;
            }

            var options = _smtpOptions.Value;
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
            message.To.Add(MailboxAddress.Parse(email));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient();
            await client.ConnectAsync(options.Host, options.Port, options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, cancellationToken);
            if (!string.IsNullOrEmpty(options.Username))
            {
                await client.AuthenticateAsync(options.Username, options.Password, cancellationToken);
            }
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failure notification email could not be sent");
        }
    }

    private async Task TryPostWebhookAsync(string encryptedUrl, string kind, string resourceName, Guid runId, string? error, CancellationToken cancellationToken)
    {
        try
        {
            var url = _protector.Decrypt(encryptedUrl);
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            var client = _httpClientFactory.CreateClient("failure-notification");
            await client.PostAsJsonAsync(url, new
            {
                kind,
                name = resourceName,
                runId,
                error,
                at = DateTimeOffset.UtcNow,
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failure notification webhook could not be posted");
        }
    }
}
