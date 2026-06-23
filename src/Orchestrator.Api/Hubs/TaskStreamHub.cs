using Microsoft.AspNetCore.SignalR;

namespace Orchestrator.Api.Hubs;

/// <summary>
/// Live event feed for the dashboard. Clients join a "task-{id}" group to watch one task's
/// token stream, and/or a "project-{id}" group to watch state-change/approval events across
/// a whole project without subscribing to every task individually.
/// </summary>
public class TaskStreamHub : Hub
{
    public async Task SubscribeToTask(Guid taskId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName.ForTask(taskId));
    }

    public async Task UnsubscribeFromTask(Guid taskId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName.ForTask(taskId));
    }

    public async Task SubscribeToProject(Guid projectId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName.ForProject(projectId));
    }

    public async Task UnsubscribeFromProject(Guid projectId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName.ForProject(projectId));
    }

    /// <summary>Global group for the cross-project approval inbox -- it doesn't know task ids upfront.</summary>
    public async Task SubscribeToApprovals()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName.Approvals);
    }

    public async Task UnsubscribeFromApprovals()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName.Approvals);
    }
}

public static class GroupName
{
    public static string ForTask(Guid taskId) => $"task-{taskId}";
    public static string ForProject(Guid projectId) => $"project-{projectId}";
    public const string Approvals = "approvals";
}
