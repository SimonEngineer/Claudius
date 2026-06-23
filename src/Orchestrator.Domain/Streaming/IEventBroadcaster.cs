namespace Orchestrator.Domain.Streaming;

/// <summary>
/// Pushes a persisted TaskEvent out to live subscribers (the SignalR hub, in the API layer).
/// Kept as an interface here so the scheduling/engine code in Infrastructure doesn't need to
/// know about SignalR.
/// </summary>
public interface IEventBroadcaster
{
    Task BroadcastEventAsync(Guid taskId, Guid runId, TaskEvent taskEvent, CancellationToken ct = default);

    Task BroadcastTaskStateChangedAsync(Guid taskId, Guid projectId, TaskState newState, CancellationToken ct = default);

    Task BroadcastApprovalRequestedAsync(Guid taskId, Approval approval, CancellationToken ct = default);
}
