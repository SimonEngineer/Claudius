using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Orchestrator.Infrastructure.Workflows;

/// <summary>Thin wrapper around SmtpClient so the "send email" node can be unit tested without a
/// real mail server.</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct);
}

public class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        var opts = options.Value;
        using var client = new SmtpClient(opts.SmtpHost, opts.SmtpPort)
        {
            EnableSsl = opts.UseSsl,
            Credentials = string.IsNullOrEmpty(opts.Username) ? null : new NetworkCredential(opts.Username, opts.Password)
        };
        using var message = new MailMessage(opts.FromAddress, to, subject, body);
        await client.SendMailAsync(message, ct);
    }
}
