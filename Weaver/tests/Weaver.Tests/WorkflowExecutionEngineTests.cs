using System.Text.Json.Nodes;
using Weaver.Domain;
using Weaver.Tests.Support;
using Xunit;

namespace Weaver.Tests;

public class WorkflowExecutionEngineTests
{
    private static WorkflowNode Node(Guid id, Guid workflowId, string type, string name, string configJson = "{}",
        int maxRetries = 0, int retryDelayMs = 1, bool isDisabled = false) => new()
    {
        Id = id,
        WorkflowId = workflowId,
        Type = type,
        Name = name,
        ConfigJson = configJson,
        MaxRetries = maxRetries,
        RetryDelayMs = retryDelayMs,
        IsDisabled = isDisabled,
    };

    private static WorkflowEdge Edge(Guid workflowId, Guid source, Guid target, string? sourceHandle = null) => new()
    {
        Id = Guid.NewGuid(),
        WorkflowId = workflowId,
        SourceNodeId = source,
        SourceHandle = sourceHandle,
        TargetNodeId = target,
    };

    [Fact]
    public async Task MergeNode_WaitsForBothPredecessors_AndKeysInputByNodeName()
    {
        var harness = new WorkflowEngineHarness(new EchoNodeHandler());
        var workflowId = Guid.NewGuid();
        var trigger = Guid.NewGuid();
        var nodeA = Guid.NewGuid();
        var nodeB = Guid.NewGuid();
        var merge = Guid.NewGuid();

        harness.Seed(db =>
        {
            var workflow = new Workflow { Id = workflowId, Name = "wf", IsEnabled = true };
            workflow.Nodes.Add(Node(trigger, workflowId, "trigger.manual", "Start"));
            workflow.Nodes.Add(Node(nodeA, workflowId, "test.echo", "A", """{"value":"fromA"}"""));
            workflow.Nodes.Add(Node(nodeB, workflowId, "test.echo", "B", """{"value":"fromB"}"""));
            workflow.Nodes.Add(Node(merge, workflowId, "test.echo", "Merge"));
            workflow.Edges.Add(Edge(workflowId, trigger, nodeA));
            workflow.Edges.Add(Edge(workflowId, trigger, nodeB));
            workflow.Edges.Add(Edge(workflowId, nodeA, merge));
            workflow.Edges.Add(Edge(workflowId, nodeB, merge));
            db.Workflows.Add(workflow);
        });

        var runId = await harness.Engine.StartRunFromNodeAsync(workflowId, trigger, TriggerKind.Manual, null);

        var run = harness.Query(db => db.WorkflowRuns.Single(r => r.Id == runId));
        Assert.Equal(RunStatus.Succeeded, run.Status);

        var mergeRun = harness.Query(db => db.NodeRuns.Single(n => n.WorkflowNodeId == merge));
        Assert.Equal(RunStatus.Succeeded, mergeRun.Status);
        var mergedInput = JsonNode.Parse(mergeRun.InputJson!)!;
        Assert.Equal("fromA", mergedInput["A"]!.GetValue<string>());
        Assert.Equal("fromB", mergedInput["B"]!.GetValue<string>());
    }

    [Fact]
    public async Task Branch_OnlyTakenEdgeExecutes_OtherBranchNeverRuns()
    {
        var harness = new WorkflowEngineHarness(new EchoNodeHandler(), new BranchNodeHandler());
        var workflowId = Guid.NewGuid();
        var trigger = Guid.NewGuid();
        var cond = Guid.NewGuid();
        var whenTrue = Guid.NewGuid();
        var whenFalse = Guid.NewGuid();

        harness.Seed(db =>
        {
            var workflow = new Workflow { Id = workflowId, Name = "wf", IsEnabled = true };
            workflow.Nodes.Add(Node(trigger, workflowId, "trigger.manual", "Start"));
            workflow.Nodes.Add(Node(cond, workflowId, "test.branch", "Cond", """{"takeBranch":"true"}"""));
            workflow.Nodes.Add(Node(whenTrue, workflowId, "test.echo", "WhenTrue"));
            workflow.Nodes.Add(Node(whenFalse, workflowId, "test.echo", "WhenFalse"));
            workflow.Edges.Add(Edge(workflowId, trigger, cond));
            workflow.Edges.Add(Edge(workflowId, cond, whenTrue, "true"));
            workflow.Edges.Add(Edge(workflowId, cond, whenFalse, "false"));
            db.Workflows.Add(workflow);
        });

        var runId = await harness.Engine.StartRunFromNodeAsync(workflowId, trigger, TriggerKind.Manual, null);

        Assert.Equal(RunStatus.Succeeded, harness.Query(db => db.WorkflowRuns.Single(r => r.Id == runId).Status));
        Assert.True(harness.Query(db => db.NodeRuns.Any(n => n.WorkflowNodeId == whenTrue)));
        Assert.False(harness.Query(db => db.NodeRuns.Any(n => n.WorkflowNodeId == whenFalse)));
    }

    [Fact]
    public async Task Retry_FailsTwiceThenSucceeds_RecordsFinalSuccess()
    {
        var flaky = new FlakyNodeHandler { FailuresBeforeSuccess = 2 };
        var harness = new WorkflowEngineHarness(flaky);
        var workflowId = Guid.NewGuid();
        var trigger = Guid.NewGuid();
        var flakyNode = Guid.NewGuid();

        harness.Seed(db =>
        {
            var workflow = new Workflow { Id = workflowId, Name = "wf", IsEnabled = true };
            workflow.Nodes.Add(Node(trigger, workflowId, "trigger.manual", "Start"));
            workflow.Nodes.Add(Node(flakyNode, workflowId, "test.flaky", "Flaky", maxRetries: 2, retryDelayMs: 1));
            workflow.Edges.Add(Edge(workflowId, trigger, flakyNode));
            db.Workflows.Add(workflow);
        });

        var runId = await harness.Engine.StartRunFromNodeAsync(workflowId, trigger, TriggerKind.Manual, null);

        Assert.Equal(RunStatus.Succeeded, harness.Query(db => db.WorkflowRuns.Single(r => r.Id == runId).Status));
        Assert.Equal(3, flaky.CallCount); // 2 failures + 1 success = maxRetries(2) + 1 attempt
        var nodeRun = harness.Query(db => db.NodeRuns.Single(n => n.WorkflowNodeId == flakyNode));
        Assert.Equal(RunStatus.Succeeded, nodeRun.Status);
        Assert.Contains("attempt 1/3 failed", nodeRun.LogText);
        Assert.Contains("attempt 2/3 failed", nodeRun.LogText);
    }

    [Fact]
    public async Task Retry_ExhaustsAllAttempts_RecordsFailure()
    {
        var flaky = new FlakyNodeHandler { FailuresBeforeSuccess = 10 };
        var harness = new WorkflowEngineHarness(flaky);
        var workflowId = Guid.NewGuid();
        var trigger = Guid.NewGuid();
        var flakyNode = Guid.NewGuid();

        harness.Seed(db =>
        {
            var workflow = new Workflow { Id = workflowId, Name = "wf", IsEnabled = true };
            workflow.Nodes.Add(Node(trigger, workflowId, "trigger.manual", "Start"));
            workflow.Nodes.Add(Node(flakyNode, workflowId, "test.flaky", "Flaky", maxRetries: 1, retryDelayMs: 1));
            workflow.Edges.Add(Edge(workflowId, trigger, flakyNode));
            db.Workflows.Add(workflow);
        });

        var runId = await harness.Engine.StartRunFromNodeAsync(workflowId, trigger, TriggerKind.Manual, null);

        Assert.Equal(2, flaky.CallCount); // maxRetries(1) + 1 initial attempt, then give up
        Assert.Equal(RunStatus.Failed, harness.Query(db => db.WorkflowRuns.Single(r => r.Id == runId).Status));
    }

    [Fact]
    public async Task ErrorEdge_RoutesFailureToHandler_AndRunSucceedsOverall()
    {
        var failer = new AlwaysFailNodeHandler();
        var harness = new WorkflowEngineHarness(failer, new EchoNodeHandler());
        var workflowId = Guid.NewGuid();
        var trigger = Guid.NewGuid();
        var failing = Guid.NewGuid();
        var errorHandler = Guid.NewGuid();
        var normalNext = Guid.NewGuid();

        harness.Seed(db =>
        {
            var workflow = new Workflow { Id = workflowId, Name = "wf", IsEnabled = true };
            workflow.Nodes.Add(Node(trigger, workflowId, "trigger.manual", "Start"));
            workflow.Nodes.Add(Node(failing, workflowId, "test.alwaysFail", "Failing"));
            workflow.Nodes.Add(Node(errorHandler, workflowId, "test.echo", "ErrorHandler"));
            workflow.Nodes.Add(Node(normalNext, workflowId, "test.echo", "NormalNext"));
            workflow.Edges.Add(Edge(workflowId, trigger, failing));
            workflow.Edges.Add(Edge(workflowId, failing, errorHandler, "error"));
            workflow.Edges.Add(Edge(workflowId, failing, normalNext)); // plain success edge, should be pruned
            db.Workflows.Add(workflow);
        });

        var runId = await harness.Engine.StartRunFromNodeAsync(workflowId, trigger, TriggerKind.Manual, null);

        // A handled failure (an "error" edge exists and was taken) doesn't fail the whole run.
        Assert.Equal(RunStatus.Succeeded, harness.Query(db => db.WorkflowRuns.Single(r => r.Id == runId).Status));
        Assert.True(harness.Query(db => db.NodeRuns.Any(n => n.WorkflowNodeId == errorHandler)));
        Assert.False(harness.Query(db => db.NodeRuns.Any(n => n.WorkflowNodeId == normalNext)));
        Assert.Equal(RunStatus.Failed, harness.Query(db => db.NodeRuns.Single(n => n.WorkflowNodeId == failing).Status));
    }

    [Fact]
    public async Task UnhandledFailure_WithNoErrorEdge_FailsTheWholeRunAndPrunesDownstream()
    {
        var failer = new AlwaysFailNodeHandler();
        var harness = new WorkflowEngineHarness(failer, new EchoNodeHandler());
        var workflowId = Guid.NewGuid();
        var trigger = Guid.NewGuid();
        var failing = Guid.NewGuid();
        var downstream = Guid.NewGuid();

        harness.Seed(db =>
        {
            var workflow = new Workflow { Id = workflowId, Name = "wf", IsEnabled = true };
            workflow.Nodes.Add(Node(trigger, workflowId, "trigger.manual", "Start"));
            workflow.Nodes.Add(Node(failing, workflowId, "test.alwaysFail", "Failing"));
            workflow.Nodes.Add(Node(downstream, workflowId, "test.echo", "Downstream"));
            workflow.Edges.Add(Edge(workflowId, trigger, failing));
            workflow.Edges.Add(Edge(workflowId, failing, downstream));
            db.Workflows.Add(workflow);
        });

        var runId = await harness.Engine.StartRunFromNodeAsync(workflowId, trigger, TriggerKind.Manual, null);

        Assert.Equal(RunStatus.Failed, harness.Query(db => db.WorkflowRuns.Single(r => r.Id == runId).Status));
        Assert.False(harness.Query(db => db.NodeRuns.Any(n => n.WorkflowNodeId == downstream)));
    }

    [Fact]
    public async Task DisabledNode_PassesInputThroughWithoutInvokingHandler()
    {
        var failer = new AlwaysFailNodeHandler(); // would fail the run if it were ever invoked
        var harness = new WorkflowEngineHarness(failer, new EchoNodeHandler());
        var workflowId = Guid.NewGuid();
        var trigger = Guid.NewGuid();
        var disabled = Guid.NewGuid();
        var downstream = Guid.NewGuid();

        harness.Seed(db =>
        {
            var workflow = new Workflow { Id = workflowId, Name = "wf", IsEnabled = true };
            workflow.Nodes.Add(Node(trigger, workflowId, "trigger.manual", "Start"));
            workflow.Nodes.Add(Node(disabled, workflowId, "test.alwaysFail", "Disabled", isDisabled: true));
            workflow.Nodes.Add(Node(downstream, workflowId, "test.echo", "Downstream"));
            workflow.Edges.Add(Edge(workflowId, trigger, disabled));
            workflow.Edges.Add(Edge(workflowId, disabled, downstream));
            db.Workflows.Add(workflow);
        });

        var runId = await harness.Engine.StartRunFromNodeAsync(workflowId, trigger, TriggerKind.Manual, null);

        Assert.Equal(RunStatus.Succeeded, harness.Query(db => db.WorkflowRuns.Single(r => r.Id == runId).Status));
        Assert.Equal(0, failer.CallCount);
        Assert.Equal(RunStatus.Skipped, harness.Query(db => db.NodeRuns.Single(n => n.WorkflowNodeId == disabled).Status));
        Assert.True(harness.Query(db => db.NodeRuns.Any(n => n.WorkflowNodeId == downstream)));
    }
}
