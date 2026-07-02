using System.Net.Http.Json;

namespace Weaver.Workflows.Nodes;

/// <summary>Config: { "webhookUrl": "https://hooks.slack.com/services/...", "message": "Price dropped to {{current.price}}!" }.</summary>
public class SendSlackNode : INodeHandler
{
    public string Type => "action.sendSlack";

    private readonly IHttpClientFactory _httpClientFactory;

    public SendSlackNode(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var webhookUrl = context.Config?["webhookUrl"]?.GetValue<string>();
        var messageTemplate = context.Config?["message"]?.GetValue<string>() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            return NodeExecutionResult.Fail("Send Slack node is missing 'webhookUrl' in config.");
        }

        var text = TemplateEngine.Render(messageTemplate, context.Input, context.AllNodeOutputs);
        var client = _httpClientFactory.CreateClient("slack-webhook");

        var response = await client.PostAsJsonAsync(webhookUrl, new { text }, context.CancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(context.CancellationToken);
            return NodeExecutionResult.Fail($"Slack webhook returned {(int)response.StatusCode}: {body}");
        }

        context.Log($"sendSlack: message=\"{text}\"");
        return NodeExecutionResult.Ok(context.Input);
    }
}
