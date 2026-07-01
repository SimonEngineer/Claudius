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
/// Config: { "code": "<c# script body>", "timeoutSeconds": 30 }. Executes with full trust
/// in-process (this is a self-hosted automation tool, not a multi-tenant sandbox) -- the script
/// sees Data, Nodes, Db, Log, and PublishEventAsync as top-level members (see WeaverScriptGlobals)
/// and its last expression becomes this node's output.
/// </summary>
public class CodeBlockNode : INodeHandler
{
    private const int DefaultTimeoutSeconds = 30;

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

        var timeoutSeconds = context.Config?["timeoutSeconds"]?.GetValue<int?>() ?? DefaultTimeoutSeconds;

        var eventPublisher = context.Services.GetRequiredService<IWorkflowEventPublisher>();
        using var scope = context.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IScriptDbAccess>();

        var globals = new WeaverScriptGlobals
        {
            Data = context.Input,
            Nodes = context.AllNodeOutputs,
            Db = db,
            Log = context.Log,
            PublishEventAsync = (name, payload) => eventPublisher.PublishAsync(name, payload, context.CancellationToken),
        };

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);

        try
        {
            var scriptTask = CSharpScript.EvaluateAsync<object?>(code, Options, globals, typeof(WeaverScriptGlobals), timeoutCts.Token);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), context.CancellationToken);

            var finished = await Task.WhenAny(scriptTask, timeoutTask);
            if (finished == timeoutTask)
            {
                // Roslyn scripting's cancellation is cooperative: this signals the script to stop
                // at its next checkpoint, but can't forcibly kill a runaway synchronous loop. The
                // node fails immediately either way so the workflow run doesn't hang on it.
                timeoutCts.Cancel();
                return NodeExecutionResult.Fail($"Code Block timed out after {timeoutSeconds}s.");
            }

            var result = await scriptTask;
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
