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

/// <summary>
/// Config: { "to": "you@example.com, {{current.ownerEmail}}", "subject": "{{title}} changed!",
/// "body": "New price: {{current.price}}" }. "to" is templated like subject/body and accepts a
/// comma-separated list of recipients (each rendered independently, so a template can expand to
/// a variable number of addresses).
/// </summary>
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
        var toTemplate = context.Config?["to"]?.GetValue<string>();
        var subjectTemplate = context.Config?["subject"]?.GetValue<string>() ?? "Weaver notification";
        var bodyTemplate = context.Config?["body"]?.GetValue<string>() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(toTemplate))
        {
            return NodeExecutionResult.Fail("Send Email node is missing 'to' in config.");
        }

        var recipients = TemplateEngine.Render(toTemplate, context.Input, context.AllNodeOutputs)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        if (recipients.Count == 0)
        {
            return NodeExecutionResult.Fail($"Send Email node's 'to' template rendered no recipients (template: '{toTemplate}').");
        }

        var options = _options.Value;
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        foreach (var recipient in recipients)
        {
            message.To.Add(MailboxAddress.Parse(recipient));
        }
        message.Subject = TemplateEngine.Render(subjectTemplate, context.Input, context.AllNodeOutputs);
        message.Body = new TextPart("plain") { Text = TemplateEngine.Render(bodyTemplate, context.Input, context.AllNodeOutputs) };

        using var client = new SmtpClient();
        await client.ConnectAsync(options.Host, options.Port, options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, context.CancellationToken);
        if (!string.IsNullOrEmpty(options.Username))
        {
            await client.AuthenticateAsync(options.Username, options.Password, context.CancellationToken);
        }
        await client.SendAsync(message, context.CancellationToken);
        await client.DisconnectAsync(true, context.CancellationToken);

        context.Log($"sendEmail: to={string.Join(", ", recipients)} subject=\"{message.Subject}\"");
        return NodeExecutionResult.Ok(context.Input);
    }
}
