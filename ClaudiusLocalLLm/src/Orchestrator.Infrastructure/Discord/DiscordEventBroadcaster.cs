using Microsoft.EntityFrameworkCore;
using Orchestrator.Domain;
using Orchestrator.Domain.Streaming;
using Orchestrator.Infrastructure.Persistence;

namespace Orchestrator.Infrastructure.Discord;

/// <summary>
/// Adapts orchestrator events into Discord notifications via the shared <see cref="DiscordBotService"/>
/// gateway connection. Only forwards events a human actually needs to act on -- approvals, and tasks
/// that landed in a terminal failure state -- token/tool-call/diff events are too high-volume for a
/// chat channel and are left to the SignalR broadcaster.
/// </summary>
public class DiscordEventBroadcaster(DiscordBotService bot, OrchestratorDbContext db) : IEventBroadcaster
{
    public Task BroadcastEventAsync(Guid taskId, Guid runId, TaskEvent taskEvent, CancellationToken ct = default)
        => Task.CompletedTask;

    public async Task BroadcastTaskStateChangedAsync(Guid taskId, Guid projectId, TaskState newState, CancellationToken ct = default)
    {
        if (newState is not (TaskState.Failed or TaskState.DeadLetter))
        {
            return;
        }

        var task = await db.Tasks.AsNoTracking().Include(t => t.Project).FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task is null)
        {
            return;
        }

        await bot.SendNotificationAsync(
            "⚠️ Task needs attention",
            $"**[{task.Project?.Name}]** {task.Title}\nState: `{newState}`",
            ct);
    }

    public async Task BroadcastApprovalRequestedAsync(Guid taskId, Guid projectId, Approval approval, CancellationToken ct = default)
    {
        var task = await db.Tasks.AsNoTracking().Include(t => t.Project).FirstOrDefaultAsync(t => t.Id == taskId, ct);

        await bot.SendNotificationAsync(
            "🔔 Approval needed",
            $"**[{task?.Project?.Name}]** {task?.Title}\n> {approval.Question}\n\nApproval id: `{approval.Id}`",
            ct);
    }

    public Task BroadcastApprovalResolvedAsync(Guid taskId, Guid projectId, Guid approvalId, ApprovalStatus status, CancellationToken ct = default)
        => Task.CompletedTask;
}
