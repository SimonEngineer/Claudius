namespace Orchestrator.Infrastructure.Workflows.Executors;

/// <summary>Config: {"to": "...", "subject": "...", "body": "..."} -- subject/body support
/// {{trigger.path}} placeholders resolved against the run's trigger payload.</summary>
public class SendEmailNodeExecutor(IEmailSender emailSender) : IWorkflowNodeExecutor
{
    public string Type => "action.sendEmail";

    public async Task<string> ExecuteAsync(WorkflowNodeContext context)
    {
        var to = WorkflowTemplating.GetConfigString(context.Config, "to")
            ?? throw new InvalidOperationException("sendEmail node is missing a 'to' address.");
        var subject = WorkflowTemplating.Render(WorkflowTemplating.GetConfigString(context.Config, "subject") ?? "", context.TriggerPayload);
        var body = WorkflowTemplating.Render(WorkflowTemplating.GetConfigString(context.Config, "body") ?? "", context.TriggerPayload);

        await emailSender.SendAsync(to, subject, body, context.CancellationToken);
        return $"Sent email to {to}.";
    }
}
