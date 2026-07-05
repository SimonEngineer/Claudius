namespace Weaver.Workflows.Nodes;

/// <summary>
/// Trigger node handlers only run their ExecuteAsync when reached as the graph's entry point in
/// an unusual configuration; the execution engine short-circuits entry-node execution to a plain
/// payload passthrough. Real firing logic (reading the cron schedule, matching event names,
/// verifying webhook secrets) lives with whatever calls IWorkflowExecutionEngine -- the cron
/// scheduler hosted service, WorkflowEventBus, and the webhook controller, respectively.
/// </summary>
public class CronTriggerNode : INodeHandler
{
    public string Type => "trigger.cron";
    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context) =>
        Task.FromResult(NodeExecutionResult.Ok(context.Input));
}

public class HttpTriggerNode : INodeHandler
{
    public string Type => "trigger.http";
    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context) =>
        Task.FromResult(NodeExecutionResult.Ok(context.Input));
}

public class EventTriggerNode : INodeHandler
{
    public string Type => "trigger.event";
    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context) =>
        Task.FromResult(NodeExecutionResult.Ok(context.Input));
}

public class ManualTriggerNode : INodeHandler
{
    public string Type => "trigger.manual";
    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context) =>
        Task.FromResult(NodeExecutionResult.Ok(context.Input));
}

/// <summary>Fires per new feed entry; the FeedTriggerSchedulerService in the Worker does the
/// polling -- at run time this is a passthrough like every other trigger.</summary>
public class FeedTriggerNode : INodeHandler
{
    public string Type => "trigger.feed";
    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context) =>
        Task.FromResult(NodeExecutionResult.Ok(context.Input));
}
