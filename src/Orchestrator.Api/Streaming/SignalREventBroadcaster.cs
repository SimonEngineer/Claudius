using Microsoft.AspNetCore.SignalR;
using Orchestrator.Api.Hubs;
using Orchestrator.Domain;
using Orchestrator.Domain.Streaming;

namespace Orchestrator.Api.Streaming;

public class SignalREventBroadcaster(IHubContext<TaskStreamHub> hub) : IEventBroadcaster
{
    public async Task BroadcastEventAsync(Guid taskId, Guid runId, TaskEvent taskEvent, CancellationToken ct = default)
    {
        await hub.Clients.Group(GroupName.ForTask(taskId)).SendAsync("EventReceived", new
        {
            taskId,
            runId,
            id = taskEvent.Id,
            type = taskEvent.Type.ToString(),
            timestamp = taskEvent.Timestamp,
            payload = taskEvent.PayloadJson
        }, ct);
    }

    public async Task BroadcastTaskStateChangedAsync(Guid taskId, Guid projectId, TaskState newState, CancellationToken ct = default)
    {
        var payload = new { taskId, projectId, state = newState.ToString() };
        await hub.Clients.Group(GroupName.ForTask(taskId)).SendAsync("TaskStateChanged", payload, ct);
        await hub.Clients.Group(GroupName.ForProject(projectId)).SendAsync("TaskStateChanged", payload, ct);
    }

    public async Task BroadcastApprovalRequestedAsync(Guid taskId, Guid projectId, Approval approval, CancellationToken ct = default)
    {
        var payload = new
        {
            taskId,
            projectId,
            approvalId = approval.Id,
            question = approval.Question,
            options = approval.OptionsJson
        };
        await hub.Clients.Group(GroupName.ForTask(taskId)).SendAsync("ApprovalRequested", payload, ct);
        await hub.Clients.Group(GroupName.Approvals).SendAsync("ApprovalRequested", payload, ct);
    }

    public async Task BroadcastApprovalResolvedAsync(Guid taskId, Guid projectId, Guid approvalId, ApprovalStatus status, CancellationToken ct = default)
    {
        var payload = new { taskId, projectId, approvalId, status = status.ToString() };
        await hub.Clients.Group(GroupName.ForTask(taskId)).SendAsync("ApprovalResolved", payload, ct);
        await hub.Clients.Group(GroupName.Approvals).SendAsync("ApprovalResolved", payload, ct);
    }
}
