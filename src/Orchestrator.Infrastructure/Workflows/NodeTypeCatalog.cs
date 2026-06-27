namespace Orchestrator.Infrastructure.Workflows;

public record NodeFieldDescriptor(string Name, string Label, string FieldType);

public record NodeTypeDescriptor(string Type, string Category, string Label, string Description, List<NodeFieldDescriptor> Fields);

/// <summary>Static metadata the GUI builder uses to render the node palette and a generic config
/// form per node (rather than hand-writing a bespoke form component per node type). Adding a new
/// starter block means adding one executor (see Executors/) plus one entry here.</summary>
public static class NodeTypeCatalog
{
    public static readonly List<NodeTypeDescriptor> All =
    [
        new("trigger.event", "trigger", "Event", "Fires when matching code raises this named event.",
            [new("eventName", "Event name", "text")]),
        new("trigger.cron", "trigger", "Schedule", "Fires on a cron schedule.",
            [new("cronExpression", "Cron expression", "text")]),
        new("trigger.http", "trigger", "Webhook", "Fires when this workflow's webhook URL receives a POST.",
            []),

        new("action.sendEmail", "action", "Send email", "Sends an email.",
            [new("to", "To", "text"), new("subject", "Subject", "text"), new("body", "Body", "textarea")]),
        new("action.sendDiscordMessage", "action", "Send Discord message", "Posts to the configured Discord channel.",
            [new("title", "Title", "text"), new("message", "Message", "textarea")]),
        new("action.codeBlock", "action", "Code block (C#)", "Runs a C# script with access to the trigger payload and the database.",
            [new("code", "Code", "code")]),
        new("action.fileLogger", "action", "File logger", "Appends the trigger payload to a file.",
            [new("path", "File path", "text"), new("format", "Format", "select:json,csv,txt")]),
    ];
}
