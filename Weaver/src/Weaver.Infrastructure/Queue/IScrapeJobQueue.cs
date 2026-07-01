namespace Weaver.Infrastructure.Queue;

/// <summary>
/// Distributed job queue backed by a Redis stream + consumer group: every worker instance is
/// a distinct consumer in the same group, so a message delivered to instance A is never also
/// delivered to instance B, and if A dies mid-processing, ReclaimStaleAsync lets another
/// instance pick the message back up instead of losing it.
/// </summary>
public interface IScrapeJobQueue
{
    Task EnsureGroupExistsAsync(CancellationToken cancellationToken = default);

    Task<string> EnqueueAsync(ScrapeJobMessage message, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QueuedScrapeJob>> ReadAsync(
        string consumerName,
        int maxCount,
        TimeSpan blockFor,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QueuedScrapeJob>> ReclaimStaleAsync(
        string consumerName,
        TimeSpan minIdleTime,
        int maxCount,
        CancellationToken cancellationToken = default);

    Task AcknowledgeAsync(string streamMessageId, CancellationToken cancellationToken = default);
}
