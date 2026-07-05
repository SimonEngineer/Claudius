using System.Net.Http.Json;

namespace Weaver.Workflows.Nodes;

/// <summary>Config: { "botToken": "123:ABC...", "chatId": "-100123", "message": "Price: {{current.price}}" }.
/// botToken is encrypted at rest (or referenced via {{secrets.NAME}}).</summary>
public class SendTelegramNode : INodeHandler
{
    public string Type => "action.sendTelegram";

    private readonly IHttpClientFactory _httpClientFactory;

    public SendTelegramNode(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var botToken = context.Config?["botToken"]?.GetValue<string>();
        var chatId = context.Config?["chatId"]?.GetValue<string>();
        var messageTemplate = context.Config?["message"]?.GetValue<string>() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
        {
            return NodeExecutionResult.Fail("Send Telegram node is missing 'botToken' or 'chatId' in config.");
        }

        var text = TemplateEngine.Render(messageTemplate, context.Input, context.AllNodeOutputs);
        var client = _httpClientFactory.CreateClient("telegram");

        var response = await client.PostAsJsonAsync(
            $"https://api.telegram.org/bot{botToken}/sendMessage",
            new { chat_id = chatId, text },
            context.CancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(context.CancellationToken);
            return NodeExecutionResult.Fail($"Telegram API returned {(int)response.StatusCode}: {body}");
        }

        context.Log($"sendTelegram: chatId={chatId} message=\"{text}\"");
        return NodeExecutionResult.Ok(context.Input);
    }
}
