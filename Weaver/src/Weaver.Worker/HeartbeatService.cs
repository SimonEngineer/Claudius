using System.Text.Json;
using StackExchange.Redis;

namespace Weaver.Worker;

/// <summary>
/// Worker has no HTTP listener to expose a /health endpoint from, so instead it touches a
/// heartbeat file on a fixed interval; the Docker healthcheck just checks the file's mtime is
/// recent (see Dockerfile.worker). A worker that's deadlocked or its process hung stops
/// refreshing the file and the container gets marked unhealthy within a couple of intervals.
/// The same tick also publishes a self-expiring Redis record so the Api's dashboard can list
/// which worker instances are alive.
/// </summary>
public class HeartbeatService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan RedisTtl = TimeSpan.FromSeconds(65);

    private readonly string _path;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<HeartbeatService> _logger;
    private readonly string _instanceId = $"{Environment.MachineName}-{Environment.ProcessId}";
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;

    public HeartbeatService(IConnectionMultiplexer redis, ILogger<HeartbeatService> logger)
    {
        _redis = redis;
        _logger = logger;
        _path = Environment.GetEnvironmentVariable("WEAVER_HEARTBEAT_PATH") ?? "/tmp/weaver-worker-heartbeat";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await File.WriteAllTextAsync(_path, DateTimeOffset.UtcNow.ToString("O"), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write heartbeat file at {Path}", _path);
            }

            try
            {
                var record = JsonSerializer.Serialize(new
                {
                    name = _instanceId,
                    startedAt = _startedAt,
                    lastSeen = DateTimeOffset.UtcNow,
                });
                await _redis.GetDatabase().StringSetAsync($"weaver:worker:{_instanceId}", record, RedisTtl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish heartbeat to Redis");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
