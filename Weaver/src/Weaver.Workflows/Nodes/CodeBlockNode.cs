using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.DependencyInjection;
using Weaver.Domain;
using Weaver.Scraping;
using Weaver.Workflows.Scripting;

namespace Weaver.Workflows.Nodes;

/// <summary>
/// Config: { "code": "<c# script body>" }. Executes with full trust in-process (this is a
/// self-hosted automation tool, not a multi-tenant sandbox) -- the script sees Data, Db, Log,
/// and PublishEventAsync as top-level members (see WeaverScriptGlobals) and its last expression
/// becomes this node's output.
/// </summary>
public class CodeBlockNode : INodeHandler
{
    public string Type => "action.code";

    private static readonly ScriptOptions Options = ScriptOptions.Default
        .WithReferences(
            typeof(object).Assembly,
            typeof(Enumerable).Assembly,
            typeof(JsonNode).Assembly,
            typeof(WeaverScriptGlobals).Assembly,
            typeof(ScrapingProject).Assembly)
        .WithImports("System", "System.Linq", "System.Collections.Generic", "System.Text.Json.Nodes", "System.Threading.Tasks");

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var code = context.Config?["code"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(code))
        {
            return NodeExecutionResult.Fail("Code Block node is missing 'code' in config.");
        }

        var eventPublisher = context.Services.GetRequiredService<IWorkflowEventPublisher>();
        using var scope = context.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IScriptDbAccess>();

        var globals = new WeaverScriptGlobals
        {
            Data = context.Input,
            Db = db,
            Log = context.Log,
            PublishEventAsync = (name, payload) => eventPublisher.PublishAsync(name, payload, context.CancellationToken),
        };

        try
        {
            var result = await CSharpScript.EvaluateAsync<object?>(code, Options, globals, typeof(WeaverScriptGlobals), context.CancellationToken);
            var output = result switch
            {
                null => null,
                JsonNode node => node,
                _ => JsonSerializer.SerializeToNode(result)
            };
            return NodeExecutionResult.Ok(output);
        }
        catch (CompilationErrorException ex)
        {
            return NodeExecutionResult.Fail("Script compilation failed: " + string.Join("; ", ex.Diagnostics));
        }
        catch (Exception ex)
        {
            return NodeExecutionResult.Fail("Script threw: " + ex.Message);
        }
    }
}
