using StackExchange.Redis;

namespace Weaver.Infrastructure.RateLimiting;

public class RedisTokenBucketRateLimiter : IDistributedRateLimiter
{
    // Runs entirely inside Redis as one atomic operation, so concurrent callers from any
    // number of processes never race on read-then-write of the bucket state. Uses Redis's
    // own clock (TIME) rather than the caller's, so instances with skewed wall clocks still
    // agree on how many tokens have refilled.
    private const string TokenBucketScript = """
        local key = KEYS[1]
        local permit_limit = tonumber(ARGV[1])
        local window_seconds = tonumber(ARGV[2])
        local burst_capacity = tonumber(ARGV[3])
        local requested = tonumber(ARGV[4])

        local bucket = redis.call('HMGET', key, 'tokens', 'ts')
        local tokens = tonumber(bucket[1])
        local last_ts = tonumber(bucket[2])

        local time_result = redis.call('TIME')
        local now_ms = tonumber(time_result[1]) * 1000 + math.floor(tonumber(time_result[2]) / 1000)

        if tokens == nil then
            tokens = burst_capacity
            last_ts = now_ms
        end

        local elapsed_ms = math.max(0, now_ms - last_ts)
        local refill_rate_per_ms = permit_limit / (window_seconds * 1000)
        tokens = math.min(burst_capacity, tokens + (elapsed_ms * refill_rate_per_ms))

        local allowed = 0
        local retry_after_ms = 0
        if tokens >= requested then
            tokens = tokens - requested
            allowed = 1
        else
            local deficit = requested - tokens
            retry_after_ms = math.ceil(deficit / refill_rate_per_ms)
        end

        redis.call('HMSET', key, 'tokens', tostring(tokens), 'ts', tostring(now_ms))
        redis.call('PEXPIRE', key, math.ceil(window_seconds * 2000))

        return {allowed, retry_after_ms}
        """;

    private readonly IConnectionMultiplexer _redis;

    public RedisTokenBucketRateLimiter(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<RateLimitDecision> TryAcquireAsync(
        string key,
        int permitLimit,
        int windowSeconds,
        int burstCapacity,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var redisKey = $"weaver:ratelimit:{key}";

        var result = (RedisValue[])await db.ScriptEvaluateAsync(
            TokenBucketScript,
            new RedisKey[] { redisKey },
            new RedisValue[] { permitLimit, windowSeconds, burstCapacity, 1 });

        var allowed = (long)result[0] == 1;
        var retryAfterMs = (long)result[1];
        return new RateLimitDecision(allowed, TimeSpan.FromMilliseconds(retryAfterMs));
    }
}
