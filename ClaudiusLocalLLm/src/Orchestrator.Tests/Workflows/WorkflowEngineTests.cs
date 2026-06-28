using Hangfire;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Orchestrator.Domain;
using Orchestrator.Infrastructure.Workflows;
using Orchestrator.Infrastructure.Workflows.Executors;
using Orchestrator.Tests.Scheduling;
using Xunit;

namespace Orchestrator.Tests.Workflows;

public class WorkflowEngineTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly IBackgroundJobClient _backgroundJobClient = Substitute.For<IBackgroundJobClient>();

    public void Dispose() => _db.Dispose();

    private const string DefinitionJson =
        """
        {
          "nodes": [
            { "id": "n1", "type": "trigger.event", "config": { "eventName": "task.done" }, "x": 0, "y": 0 },
            { "id": "n2", "type": "action.fileLogger", "config": { "path": "PLACEHOLDER", "format": "json" }, "x": 100, "y": 0 }
          ],
          "edges": [
            { "from": "n1", "to": "n2" }
          ]
        }
        """;

    private WorkflowEngine MakeEngine(IEnumerable<IWorkflowNodeExecutor>? executors = null) =>
        new(_db.Context, executors ?? [new FileLoggerNodeExecutor()], _backgroundJobClient, NullLogger<WorkflowEngine>.Instance);

    [Fact]
    public async Task ExecuteWorkflowAsync_RunsDownstreamActionNode_AndRecordsSucceededStepLog()
    {
        var path = Path.Combine(Path.GetTempPath(), "workflow-engine-tests-" + Guid.NewGuid() + ".json");
        try
        {
            var workflow = new Workflow { Name = "wf", DefinitionJson = DefinitionJson.Replace("PLACEHOLDER", path.Replace("\\", "\\\\")) };
            _db.Context.Workflows.Add(workflow);
            await _db.Context.SaveChangesAsync();

            var engine = MakeEngine();
            var runId = await engine.ExecuteWorkflowAsync(workflow.Id, "{}", CancellationToken.None);

            var run = await _db.Context.WorkflowRuns.FindAsync(runId);
            Assert.Equal(WorkflowRunStatus.Succeeded, run!.Status);
            Assert.Contains("n2", run.StepLogJson);
            Assert.True(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_RecordsFailedRun_WhenNoExecutorRegisteredForNodeType()
    {
        var workflow = new Workflow { Name = "wf", DefinitionJson = DefinitionJson.Replace("PLACEHOLDER", "/tmp/unused.json") };
        _db.Context.Workflows.Add(workflow);
        await _db.Context.SaveChangesAsync();

        var engine = MakeEngine(executors: []);
        var runId = await engine.ExecuteWorkflowAsync(workflow.Id, "{}", CancellationToken.None);

        var run = await _db.Context.WorkflowRuns.FindAsync(runId);
        Assert.Equal(WorkflowRunStatus.Failed, run!.Status);
        Assert.NotNull(run.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Throws_WhenWorkflowNotFound()
    {
        var engine = MakeEngine();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.ExecuteWorkflowAsync(Guid.NewGuid(), "{}", CancellationToken.None));
    }

    [Fact]
    public async Task TriggerEventAsync_EnqueuesMatchingEnabledWorkflow_ButNotDisabledOrNonMatchingOnes()
    {
        var matching = new Workflow { Name = "matches", IsEnabled = true, DefinitionJson = DefinitionJson.Replace("PLACEHOLDER", "/tmp/unused.json") };
        var disabled = new Workflow { Name = "disabled", IsEnabled = false, DefinitionJson = DefinitionJson.Replace("PLACEHOLDER", "/tmp/unused.json") };
        var nonMatching = new Workflow
        {
            Name = "other-event",
            IsEnabled = true,
            DefinitionJson = DefinitionJson.Replace("PLACEHOLDER", "/tmp/unused.json").Replace("task.done", "task.other")
        };
        _db.Context.Workflows.AddRange(matching, disabled, nonMatching);
        await _db.Context.SaveChangesAsync();

        var engine = MakeEngine();
        await engine.TriggerEventAsync("task.done", new { taskId = Guid.NewGuid() }, CancellationToken.None);

        _backgroundJobClient.ReceivedWithAnyArgs(1).Create(default!, default!);
    }
}
