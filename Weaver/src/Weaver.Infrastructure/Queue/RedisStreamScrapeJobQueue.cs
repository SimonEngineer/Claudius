using System.Text.Json;
using StackExchange.Redis;

namespace Weaver.Infrastructure.Queue;

public class RedisStreamScrapeJobQueue : IScrapeJobQueue
{
    public const string StreamKey = "weaver:scrape-jobs";
    public const string ConsumerGroup = "weaver-workers";
    private const string PayloadField = "payload";

    private readonly IConnectionMultiplexer _redis;

    public RedisStreamScrapeJobQueue(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task EnsureGroupExistsAsync(CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        try
        {
            await db.StreamCreateConsumerGroupAsync(StreamKey, ConsumerGroup, StreamPosition.NewMessages, createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.StartsWith("BUSYGROUP"))
        {
            // Group already exists -- fine, another instance created it first.
        }
    }

    public async Task<string> EnqueueAsync(ScrapeJobMessage message, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var json = JsonSerializer.Serialize(message);
        var id = await db.StreamAddAsync(StreamKey, PayloadField, json);
        return id.ToString();
    }

    public async Task<IReadOnlyList<QueuedScrapeJob>> ReadAsync(
        string consumerName,
        int maxCount,
        TimeSpan blockFor,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var entries = await db.StreamReadGroupAsync(
            StreamKey,
            ConsumerGroup,
            consumerName,
            position: StreamPosition.NewMessages,
            count: maxCount);

        // StackExchange.Redis has no client-side equivalent of Redis' native BLOCK option for
        // XREADGROUP, so an empty read waits out blockFor here before the caller polls again.
        if (entries.Length == 0 && blockFor > TimeSpan.Zero)
        {
            await Task.Delay(blockFor, cancellationToken);
        }

        return ToQueuedJobs(entries, deliveryCount: 1);
    }

    public async Task<IReadOnlyList<QueuedScrapeJob>> ReclaimStaleAsync(
        string consumerName,
        TimeSpan minIdleTime,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var result = await db.StreamAutoClaimAsync(
            StreamKey,
            ConsumerGroup,
            consumerName,
            (long)minIdleTime.TotalMilliseconds,
            StreamPosition.Beginning,
            count: maxCount);

        return ToQueuedJobs(result.ClaimedEntries, deliveryCount: 2);
    }

    public async Task AcknowledgeAsync(string streamMessageId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        await db.StreamAcknowledgeAsync(StreamKey, ConsumerGroup, streamMessageId);
    }

    private static List<QueuedScrapeJob> ToQueuedJobs(StreamEntry[] entries, int deliveryCount)
    {
        var jobs = new List<QueuedScrapeJob>(entries.Length);
        foreach (var entry in entries)
        {
            var field = entry.Values.FirstOrDefault(v => v.Name == PayloadField);
            if (field.Value.IsNullOrEmpty)
            {
                continue;
            }

            var message = JsonSerializer.Deserialize<ScrapeJobMessage>(field.Value!);
            if (message is not null)
            {
                jobs.Add(new QueuedScrapeJob(entry.Id.ToString(), message, deliveryCount));
            }
        }

        return jobs;
    }
}
