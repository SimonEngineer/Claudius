using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Orchestrator.Infrastructure.Persistence;

namespace Orchestrator.Infrastructure.Workflows.Executors;

/// <summary>
/// Globals exposed to a code block's script body as top-level `Trigger`/`Db`. Workflow
/// definitions are authored by whoever administers this orchestrator (not arbitrary internet
/// input reaching this endpoint), so the script runs with the same trust level as the rest of the
/// backend -- including direct EF Core access, since "run a query against the db data" is exactly
/// what was asked for.
/// </summary>
public class CodeBlockGlobals(System.Text.Json.JsonElement trigger, OrchestratorDbContext db)
{
    public System.Text.Json.JsonElement Trigger { get; } = trigger;
    public OrchestratorDbContext Db { get; } = db;
}

/// <summary>Config: {"code": "C# script body"}. Compiled fresh each run via Roslyn scripting --
/// no caching, since workflows are edited far more often than they're executed at any volume that
/// would make compile time matter.</summary>
public class CodeBlockNodeExecutor : IWorkflowNodeExecutor
{
    public string Type => "action.codeBlock";

    private static readonly ScriptOptions Options = ScriptOptions.Default
        .WithReferences(typeof(OrchestratorDbContext).Assembly, typeof(System.Text.Json.JsonElement).Assembly)
        .WithImports("System", "System.Linq", "System.Collections.Generic", "System.Threading.Tasks", "Orchestrator.Domain");

    public async Task<string> ExecuteAsync(WorkflowNodeContext context)
    {
        var code = WorkflowTemplating.GetConfigString(context.Config, "code")
            ?? throw new InvalidOperationException("codeBlock node is missing 'code'.");

        var globals = new CodeBlockGlobals(context.TriggerPayload, context.Db);
        var result = await CSharpScript.EvaluateAsync<object?>(code, Options, globals, cancellationToken: context.CancellationToken);
        return result?.ToString() ?? "(code block returned no value)";
    }
}
