using Orchestrator.Domain;
using Orchestrator.Domain.Streaming;

namespace Orchestrator.Api.Streaming;

/// <summary>
/// Fans every broadcast out to all configured sinks (SignalR for the live dashboard, Discord for
/// notifications) so callers (TaskRunnerJob, the controllers) only ever depend on one
/// <see cref="IEventBroadcaster"/> without knowing how many sinks are behind it.
/// </summary>
public class CompositeEventBroadcaster(IReadOnlyList<IEventBroadcaster> sinks) : IEventBroadcaster
{
    public Task BroadcastEventAsync(Guid taskId, Guid runId, TaskEvent taskEvent, CancellationToken ct = default)
        => Task.WhenAll(sinks.Select(s => s.BroadcastEventAsync(taskId, runId, taskEvent, ct)));

    public Task BroadcastTaskStateChangedAsync(Guid taskId, Guid projectId, TaskState newState, CancellationToken ct = default)
        => Task.WhenAll(sinks.Select(s => s.BroadcastTaskStateChangedAsync(taskId, projectId, newState, ct)));

    public Task BroadcastApprovalRequestedAsync(Guid taskId, Guid projectId, Approval approval, CancellationToken ct = default)
        => Task.WhenAll(sinks.Select(s => s.BroadcastApprovalRequestedAsync(taskId, projectId, approval, ct)));

    public Task BroadcastApprovalResolvedAsync(Guid taskId, Guid projectId, Guid approvalId, ApprovalStatus status, CancellationToken ct = default)
        => Task.WhenAll(sinks.Select(s => s.BroadcastApprovalResolvedAsync(taskId, projectId, approvalId, status, ct)));
}
