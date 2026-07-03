namespace Weaver.Infrastructure.Realtime;

/// <summary>
/// Fires the owner's configured failure notifications (email / webhook) when a run fails.
/// Implementations must never throw -- a broken notification channel must not take down or
/// re-fail the run that triggered it.
/// </summary>
public interface IFailureNotifier
{
    Task NotifyScrapeFailureAsync(Guid ownerUserId, string projectName, Guid runId, string? error, CancellationToken cancellationToken = default);
    Task NotifyWorkflowFailureAsync(Guid ownerUserId, string workflowName, Guid runId, string? error, CancellationToken cancellationToken = default);
}
