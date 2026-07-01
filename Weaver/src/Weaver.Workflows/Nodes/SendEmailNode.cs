using System.Text.Json.Nodes;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Weaver.Workflows.Nodes;

public class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "weaver@localhost";
    public string FromName { get; set; } = "Weaver";
    public bool UseStartTls { get; set; } = true;
}

/// <summary>Config: { "to": "you@example.com", "subject": "{{title}} changed!", "body": "New price: {{current.price}}" }. Templates are rendered against Input.</summary>
public class SendEmailNode : INodeHandler
{
    public string Type => "action.sendEmail";

    private readonly IOptions<SmtpOptions> _options;

    public SendEmailNode(IOptions<SmtpOptions> options)
    {
        _options = options;
    }

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var to = context.Config?["to"]?.GetValue<string>();
        var subjectTemplate = context.Config?["subject"]?.GetValue<string>() ?? "Weaver notification";
        var bodyTemplate = context.Config?["body"]?.GetValue<string>() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(to))
        {
            return NodeExecutionResult.Fail("Send Email node is missing 'to' in config.");
        }

        var options = _options.Value;
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = TemplateEngine.Render(subjectTemplate, context.Input);
        message.Body = new TextPart("plain") { Text = TemplateEngine.Render(bodyTemplate, context.Input) };

        using var client = new SmtpClient();
        await client.ConnectAsync(options.Host, options.Port, options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, context.CancellationToken);
        if (!string.IsNullOrEmpty(options.Username))
        {
            await client.AuthenticateAsync(options.Username, options.Password, context.CancellationToken);
        }
        await client.SendAsync(message, context.CancellationToken);
        await client.DisconnectAsync(true, context.CancellationToken);

        context.Log($"sendEmail: to={to} subject=\"{message.Subject}\"");
        return NodeExecutionResult.Ok(context.Input);
    }
}
