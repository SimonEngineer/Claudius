using System.Net.Http.Json;

namespace Weaver.Workflows.Nodes;

/// <summary>Config: { "webhookUrl": "https://discord.com/api/webhooks/...", "message": "Price dropped to {{current.price}}!" }.</summary>
public class SendDiscordNode : INodeHandler
{
    public string Type => "action.sendDiscord";

    private readonly IHttpClientFactory _httpClientFactory;

    public SendDiscordNode(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var webhookUrl = context.Config?["webhookUrl"]?.GetValue<string>();
        var messageTemplate = context.Config?["message"]?.GetValue<string>() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            return NodeExecutionResult.Fail("Send Discord node is missing 'webhookUrl' in config.");
        }

        var content = TemplateEngine.Render(messageTemplate, context.Input);
        var client = _httpClientFactory.CreateClient("discord-webhook");

        var response = await client.PostAsJsonAsync(webhookUrl, new { content }, context.CancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(context.CancellationToken);
            return NodeExecutionResult.Fail($"Discord webhook returned {(int)response.StatusCode}: {body}");
        }

        context.Log($"sendDiscord: message=\"{content}\"");
        return NodeExecutionResult.Ok(context.Input);
    }
}
