using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Orchestrator.Infrastructure.Workflows;

/// <summary>Runs WorkflowCronSync once at startup so cron triggers created in a previous process
/// lifetime (or edited directly in the DB) are registered with Hangfire on every boot, not just
/// when a workflow is saved through the API.</summary>
public class WorkflowCronSyncStartupService(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var sync = scope.ServiceProvider.GetRequiredService<WorkflowCronSync>();
        await sync.SyncAsync(ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
