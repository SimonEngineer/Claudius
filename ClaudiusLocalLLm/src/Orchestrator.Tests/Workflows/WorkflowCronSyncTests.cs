using Hangfire;
using NSubstitute;
using Orchestrator.Domain;
using Orchestrator.Infrastructure.Workflows;
using Orchestrator.Tests.Scheduling;
using Xunit;

namespace Orchestrator.Tests.Workflows;

public class WorkflowCronSyncTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly IRecurringJobManager _recurringJobs = Substitute.For<IRecurringJobManager>();

    public void Dispose() => _db.Dispose();

    private static string JobId(Guid workflowId) => $"workflow-cron-{workflowId:N}";

    [Fact]
    public async Task SyncAsync_RegistersRecurringJob_ForEnabledWorkflowWithCronTrigger()
    {
        var workflow = new Workflow
        {
            Name = "wf",
            IsEnabled = true,
            DefinitionJson = """{"nodes":[{"id":"n1","type":"trigger.cron","config":{"cronExpression":"*/5 * * * *"},"x":0,"y":0}],"edges":[]}"""
        };
        _db.Context.Workflows.Add(workflow);
        await _db.Context.SaveChangesAsync();

        await new WorkflowCronSync(_db.Context, _recurringJobs).SyncAsync(CancellationToken.None);

        _recurringJobs.Received(1).AddOrUpdate(
            JobId(workflow.Id), Arg.Any<Hangfire.Common.Job>(), "*/5 * * * *", Arg.Any<RecurringJobOptions>());
    }

    [Fact]
    public async Task SyncAsync_RemovesRecurringJob_WhenWorkflowIsDisabled()
    {
        var workflow = new Workflow
        {
            Name = "wf",
            IsEnabled = false,
            DefinitionJson = """{"nodes":[{"id":"n1","type":"trigger.cron","config":{"cronExpression":"*/5 * * * *"},"x":0,"y":0}],"edges":[]}"""
        };
        _db.Context.Workflows.Add(workflow);
        await _db.Context.SaveChangesAsync();

        await new WorkflowCronSync(_db.Context, _recurringJobs).SyncAsync(CancellationToken.None);

        _recurringJobs.Received(1).RemoveIfExists(JobId(workflow.Id));
    }

    [Fact]
    public async Task SyncAsync_RemovesRecurringJob_WhenWorkflowHasNoCronTriggerNode()
    {
        var workflow = new Workflow
        {
            Name = "wf",
            IsEnabled = true,
            DefinitionJson = """{"nodes":[{"id":"n1","type":"trigger.event","config":{"eventName":"task.done"},"x":0,"y":0}],"edges":[]}"""
        };
        _db.Context.Workflows.Add(workflow);
        await _db.Context.SaveChangesAsync();

        await new WorkflowCronSync(_db.Context, _recurringJobs).SyncAsync(CancellationToken.None);

        _recurringJobs.Received(1).RemoveIfExists(JobId(workflow.Id));
    }
}
