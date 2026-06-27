using Orchestrator.Infrastructure.Discord;

namespace Orchestrator.Infrastructure.Workflows.Executors;

/// <summary>Config: {"title": "...", "message": "..."} -- both support {{trigger.path}}
/// placeholders. Posts to the same Discord channel the system already notifies on approvals/
/// dead-letters; there's only one bot connection, so there's nothing per-node to configure beyond
/// the message itself.</summary>
public class SendDiscordMessageNodeExecutor(IDiscordNotifier discord) : IWorkflowNodeExecutor
{
    public string Type => "action.sendDiscordMessage";

    public async Task<string> ExecuteAsync(WorkflowNodeContext context)
    {
        var title = WorkflowTemplating.Render(WorkflowTemplating.GetConfigString(context.Config, "title") ?? "Workflow notification", context.TriggerPayload);
        var message = WorkflowTemplating.Render(WorkflowTemplating.GetConfigString(context.Config, "message") ?? "", context.TriggerPayload);

        await discord.SendNotificationAsync(title, message, context.CancellationToken);
        return "Sent Discord message.";
    }
}
