using System.Text;
using System.Text.Json.Nodes;

namespace Weaver.Workflows.Nodes;

/// <summary>
/// Config: { "method": "GET", "url": "https://api.example.com/{{Input.id}}", "headers": {"Authorization":"Bearer {{...}}"},
/// "body": "{{...}}" (optional, template-rendered), "timeoutSeconds": 30 }.
/// The general-purpose escape hatch for calling any HTTP API mid-workflow -- everything else
/// (Scrape Action, Send Discord, Send Email) is really just this with the target baked in.
/// Succeeds on any HTTP response (even a 4xx/5xx -- that's the server answering, not this node
/// failing) and only fails on a genuine transport error (DNS, connect, timeout), so a Condition
/// node downstream can branch on the returned statusCode however the workflow wants.
/// </summary>
public class HttpRequestNode : INodeHandler
{
    public string Type => "action.httpRequest";

    private readonly IHttpClientFactory _httpClientFactory;

    public HttpRequestNode(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var methodName = context.Config?["method"]?.GetValue<string>() ?? "GET";
        var urlTemplate = context.Config?["url"]?.GetValue<string>();
        var bodyTemplate = context.Config?["body"]?.GetValue<string>();
        var timeoutSeconds = context.Config?["timeoutSeconds"]?.GetValue<int>() ?? 30;
        var headersConfig = context.Config?["headers"] as JsonObject;

        if (string.IsNullOrWhiteSpace(urlTemplate))
        {
            return NodeExecutionResult.Fail("HTTP Request node is missing 'url' in config.");
        }

        HttpMethod method;
        try
        {
            method = new HttpMethod(methodName);
        }
        catch (ArgumentException)
        {
            return NodeExecutionResult.Fail($"HTTP Request node has an invalid 'method': '{methodName}'.");
        }

        var url = TemplateEngine.Render(urlTemplate, context.Input, context.AllNodeOutputs);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return NodeExecutionResult.Fail($"HTTP Request node's resolved url '{url}' is not a valid absolute http(s) URL.");
        }

        using var request = new HttpRequestMessage(method, uri);

        // A header named Content-Type is handled specially: it has to be set on HttpContent, not
        // HttpRequestMessage.Headers (.NET rejects it there even via TryAddWithoutValidation), and
        // StringContent's constructor is the only clean way to set it without leaving the default
        // "text/plain" value sitting alongside it as a second, conflicting value.
        var contentType = headersConfig?.FirstOrDefault(kv => string.Equals(kv.Key, "content-type", StringComparison.OrdinalIgnoreCase)).Value
            ?.GetValue<string>();
        if (!string.IsNullOrEmpty(bodyTemplate) && method != HttpMethod.Get && method != HttpMethod.Head)
        {
            var renderedBody = TemplateEngine.Render(bodyTemplate, context.Input, context.AllNodeOutputs);
            var renderedContentType = contentType is null ? "application/json" : TemplateEngine.Render(contentType, context.Input, context.AllNodeOutputs);
            request.Content = new StringContent(renderedBody, Encoding.UTF8, renderedContentType);
        }

        if (headersConfig is not null)
        {
            foreach (var (name, value) in headersConfig)
            {
                if (string.Equals(name, "content-type", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // already applied to request.Content above
                }

                var renderedValue = TemplateEngine.Render(value?.GetValue<string>() ?? string.Empty, context.Input, context.AllNodeOutputs);
                request.Headers.TryAddWithoutValidation(name, renderedValue);
            }
        }

        var client = _httpClientFactory.CreateClient("http-request-node");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 300)));

        HttpResponseMessage response;
        string responseText;
        try
        {
            response = await client.SendAsync(request, cts.Token);
            responseText = await response.Content.ReadAsStringAsync(context.CancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return NodeExecutionResult.Fail($"HTTP Request to {uri} failed: {ex.Message}");
        }

        JsonNode? parsedBody;
        try
        {
            parsedBody = string.IsNullOrWhiteSpace(responseText) ? null : JsonNode.Parse(responseText);
        }
        catch (System.Text.Json.JsonException)
        {
            parsedBody = JsonValue.Create(responseText);
        }

        var responseHeaders = new JsonObject();
        foreach (var header in response.Headers.Concat(response.Content.Headers))
        {
            responseHeaders[header.Key] = string.Join(", ", header.Value);
        }

        var output = new JsonObject
        {
            ["statusCode"] = (int)response.StatusCode,
            ["headers"] = responseHeaders,
            ["body"] = parsedBody,
        };

        context.Log($"httpRequest: {method} {uri} -> {(int)response.StatusCode}");
        return NodeExecutionResult.Ok(output);
    }
}
