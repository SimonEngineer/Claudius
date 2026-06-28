namespace Orchestrator.Infrastructure.Discord;

/// <summary>Narrow interface over DiscordBotService's notification post, so the workflow
/// "send Discord message" node can be unit tested without a real gateway connection.</summary>
public interface IDiscordNotifier
{
    Task SendNotificationAsync(string title, string description, CancellationToken ct = default);
}
