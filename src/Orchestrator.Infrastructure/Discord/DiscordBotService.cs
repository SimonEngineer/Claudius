using System.Text;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Domain;
using Orchestrator.Infrastructure.Persistence;

namespace Orchestrator.Infrastructure.Discord;

/// <summary>
/// Owns the single persistent Discord gateway connection: posts notifications to the configured
/// channel (via <see cref="SendNotificationAsync"/>, called from <see cref="DiscordEventBroadcaster"/>)
/// and answers a "!status" message typed in any channel the bot can see with a snapshot of the
/// current system state. A no-op if Discord:Enabled is false or no bot token is configured.
/// </summary>
public class DiscordBotService(
    IOptions<DiscordOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<DiscordBotService> logger) : BackgroundService
{
    private readonly DiscordOptions _options = options.Value;
    private readonly TaskCompletionSource _ready = new();
    private DiscordSocketClient? _client;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.BotToken))
        {
            logger.LogInformation("Discord integration disabled (set Discord:Enabled and Discord:BotToken to turn it on).");
            return;
        }

        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent
        });
        _client.Log += OnClientLog;
        _client.Ready += OnReady;
        _client.MessageReceived += OnMessageReceivedAsync;

        await _client.LoginAsync(TokenType.Bot, _options.BotToken);
        await _client.StartAsync();

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
        }
        finally
        {
            await _client.StopAsync();
            await _client.LogoutAsync();
        }
    }

    /// <summary>Posts an embed to the configured notify channel. Swallows/logs failures rather than
    /// throwing, so a Discord outage never blocks the task pipeline that triggered the notification.</summary>
    public async Task SendNotificationAsync(string title, string description, CancellationToken ct = default)
    {
        if (_client is null)
        {
            return;
        }

        try
        {
            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        }
        catch (TimeoutException)
        {
            logger.LogWarning("Discord client not ready after 10s; dropping notification {Title}.", title);
            return;
        }

        if (GetNotifyChannel() is not { } channel)
        {
            logger.LogWarning("Discord notify channel {ChannelId} not found/accessible; dropping notification {Title}.", _options.NotifyChannelId, title);
            return;
        }

        try
        {
            var embed = new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(description)
                .WithCurrentTimestamp()
                .Build();
            await channel.SendMessageAsync(embed: embed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Discord notification {Title}.", title);
        }
    }

    private Task OnReady()
    {
        _ready.TrySetResult();
        return Task.CompletedTask;
    }

    private Task OnClientLog(LogMessage message)
    {
        logger.Log(MapSeverity(message.Severity), message.Exception, "{Message}", message.Message);
        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(SocketMessage message)
    {
        if (message.Author.IsBot)
        {
            return;
        }

        if (!string.Equals(message.Content.Trim(), "!status", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var embed = await BuildStatusEmbedAsync();
            await message.Channel.SendMessageAsync(embed: embed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to handle !status command.");
        }
    }

    private async Task<Embed> BuildStatusEmbedAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>();

        var projectCount = await db.Projects.CountAsync();
        var stateCounts = await db.Tasks
            .GroupBy(t => t.State)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .ToListAsync();
        var pendingApprovals = await db.Approvals
            .Where(a => a.Status == ApprovalStatus.Pending)
            .Include(a => a.Task).ThenInclude(t => t!.Project)
            .OrderBy(a => a.CreatedAt)
            .Take(10)
            .ToListAsync();
        var inProgress = await db.Tasks
            .Where(t => t.State == TaskState.Planning || t.State == TaskState.InProgress || t.State == TaskState.Verifying)
            .Include(t => t.Project)
            .OrderByDescending(t => t.UpdatedAt)
            .Take(10)
            .ToListAsync();

        var stateSummary = stateCounts.Count == 0
            ? "none"
            : string.Join("\n", stateCounts.Select(s => $"`{s.State}`: {s.Count}"));

        var approvalsSummary = pendingApprovals.Count == 0
            ? "none"
            : FormatLines(pendingApprovals.Select(a =>
                $"[{a.Task?.Project?.Name}] {a.Task?.Title} — {Truncate(a.Question, 80)}"));

        var inProgressSummary = inProgress.Count == 0
            ? "none"
            : FormatLines(inProgress.Select(t => $"[{t.Project?.Name}] {t.Title} (`{t.State}`)"));

        return new EmbedBuilder()
            .WithTitle("📊 Orchestrator status")
            .WithColor(Color.Blue)
            .WithCurrentTimestamp()
            .AddField("Projects", projectCount.ToString(), inline: true)
            .AddField("Tasks by state", stateSummary, inline: true)
            .AddField($"Pending approvals ({pendingApprovals.Count})", approvalsSummary)
            .AddField($"In progress ({inProgress.Count})", inProgressSummary)
            .Build();
    }

    private static string FormatLines(IEnumerable<string> lines)
    {
        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            sb.Append("• ").AppendLine(line);
        }
        return sb.ToString().TrimEnd();
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "…";

    private IMessageChannel? GetNotifyChannel()
        => _options.NotifyChannelId != 0 ? _client?.GetChannel(_options.NotifyChannelId) as IMessageChannel : null;

    private static LogLevel MapSeverity(LogSeverity severity) => severity switch
    {
        LogSeverity.Critical => LogLevel.Critical,
        LogSeverity.Error => LogLevel.Error,
        LogSeverity.Warning => LogLevel.Warning,
        LogSeverity.Info => LogLevel.Information,
        LogSeverity.Verbose => LogLevel.Debug,
        LogSeverity.Debug => LogLevel.Trace,
        _ => LogLevel.Information
    };
}
