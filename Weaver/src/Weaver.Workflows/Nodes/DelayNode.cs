namespace Weaver.Workflows.Nodes;

/// <summary>Config: { "seconds": 5 }. Pauses the run for the given duration, then passes its input
/// through unchanged -- for pacing a workflow against a downstream rate limit, or just waiting for
/// an external system to catch up before the next step reads from it.</summary>
public class DelayNode : INodeHandler
{
    private const int MaxSeconds = 3600;

    public string Type => "action.delay";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var seconds = context.Config?["seconds"]?.GetValue<double>() ?? 0;
        if (seconds < 0)
        {
            return NodeExecutionResult.Fail("Delay node's 'seconds' must not be negative.");
        }

        var clamped = Math.Min(seconds, MaxSeconds);
        await Task.Delay(TimeSpan.FromSeconds(clamped), context.CancellationToken);

        context.Log($"delay: waited {clamped:0.##}s");
        return NodeExecutionResult.Ok(context.Input?.DeepClone());
    }
}
