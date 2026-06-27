namespace Orchestrator.Infrastructure.Workflows;

/// <summary>
/// Entry point for firing automations both from the GUI (manual run, webhook, cron) and from
/// anywhere else in the codebase: inject IWorkflowEngine and call TriggerEventAsync with whatever
/// event name and payload make sense for that call site (e.g. "task.done", "approval.requested").
/// Any enabled workflow with a matching trigger.event node runs in the background.
/// </summary>
public interface IWorkflowEngine
{
    Task TriggerEventAsync(string eventName, object payload, CancellationToken ct);

    /// <summary>Runs one specific workflow immediately (used by the webhook trigger, the GUI's
    /// "run now" button, and cron-triggered Hangfire jobs) and returns the resulting run record.</summary>
    Task<Guid> ExecuteWorkflowAsync(Guid workflowId, string triggerPayloadJson, CancellationToken ct);
}
