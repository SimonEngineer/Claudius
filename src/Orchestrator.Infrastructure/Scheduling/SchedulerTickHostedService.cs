using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Orchestrator.Infrastructure.Scheduling;

/// <summary>
/// Drives SchedulerTickJob on a fixed interval (default 5s -- configurable, see
/// SchedulerOptions.TickInterval). Runs as a plain BackgroundService rather than a Hangfire
/// recurring job because Hangfire's cron scheduling only resolves to whole minutes, which is
/// far too coarse for "notice a freed worker slot and dispatch the next task" responsiveness.
/// </summary>
public class SchedulerTickHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<SchedulerOptions> options,
    ILogger<SchedulerTickHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.TickInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var tickJob = scope.ServiceProvider.GetRequiredService<SchedulerTickJob>();
                await tickJob.TickAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduler tick failed; will retry next interval");
            }
        }
    }
}
