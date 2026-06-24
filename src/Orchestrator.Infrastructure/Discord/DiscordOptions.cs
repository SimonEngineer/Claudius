namespace Orchestrator.Infrastructure.Discord;

/// <summary>
/// Optional Discord integration: notifies a channel when a task needs human input (approval) or
/// attention (failed/dead-lettered), and answers a "!status" command with the current system
/// state. Disabled (no-op) unless Enabled is true and BotToken is set.
/// </summary>
public class DiscordOptions
{
    public const string SectionName = "Discord";

    public bool Enabled { get; set; }

    /// <summary>Bot token from the Discord developer portal. Needs the "Message Content Intent" enabled.</summary>
    public string? BotToken { get; set; }

    /// <summary>Channel id that approval/attention notifications are posted to.</summary>
    public ulong NotifyChannelId { get; set; }
}
