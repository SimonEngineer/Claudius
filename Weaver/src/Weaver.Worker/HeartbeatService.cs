namespace Weaver.Worker;

/// <summary>
/// Worker has no HTTP listener to expose a /health endpoint from, so instead it touches a
/// heartbeat file on a fixed interval; the Docker healthcheck just checks the file's mtime is
/// recent (see Dockerfile.worker). A worker that's deadlocked or its process hung stops
/// refreshing the file and the container gets marked unhealthy within a couple of intervals.
/// </summary>
public class HeartbeatService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(20);
    private readonly string _path;
    private readonly ILogger<HeartbeatService> _logger;

    public HeartbeatService(ILogger<HeartbeatService> logger)
    {
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

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
