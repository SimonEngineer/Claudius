using System.Text.Json.Nodes;

namespace Weaver.Workflows.Scripting;

/// <summary>The `this` context available inside a Code Block script (referenced there as top-level members, e.g. `Data["price"]`, `Log("...")`).</summary>
public class WeaverScriptGlobals
{
    /// <summary>The upstream node's output / trigger payload, as a JSON tree.</summary>
    public JsonNode? Data { get; init; }

    /// <summary>Read-only queries against Weaver's own data (scraping projects, past runs, item history).</summary>
    public required IScriptDbAccess Db { get; init; }

    /// <summary>Appends a line to this node's run log, visible in the workflow run history UI.</summary>
    public required Action<string> Log { get; init; }

    /// <summary>Raises a named event, exactly as if the code had been a Weaver automation block -- any workflow with a matching Event Trigger node fires from here.</summary>
    public required Func<string, object, Task> PublishEventAsync { get; init; }
}
