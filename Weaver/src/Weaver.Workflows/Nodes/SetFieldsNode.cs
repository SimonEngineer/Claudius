using System.Text.Json.Nodes;

namespace Weaver.Workflows.Nodes;

/// <summary>
/// Config: { "mappings": { "title": "{{current.title}}", "source": "weaver" } }. Outputs a fresh
/// object whose values are rendered through the same template engine as email/Slack bodies -- for
/// reshaping data before an HTTP request or notification instead of writing a Code node.
/// </summary>
public class SetFieldsNode : INodeHandler
{
    public string Type => "action.setFields";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        if (context.Config?["mappings"] is not JsonObject mappings || mappings.Count == 0)
        {
            return Task.FromResult(NodeExecutionResult.Fail("Set Fields node needs at least one mapping in config.mappings."));
        }

        var output = new JsonObject();
        foreach (var (name, templateNode) in mappings)
        {
            var template = templateNode?.GetValue<string>() ?? string.Empty;
            output[name] = TemplateEngine.Render(template, context.Input, context.AllNodeOutputs);
        }

        context.Log($"setFields: produced {output.Count} field(s)");
        return Task.FromResult(NodeExecutionResult.Ok(output));
    }
}
