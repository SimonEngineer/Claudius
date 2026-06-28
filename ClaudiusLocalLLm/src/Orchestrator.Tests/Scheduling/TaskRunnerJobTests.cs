using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Orchestrator.Domain;
using Orchestrator.Domain.Streaming;
using Orchestrator.Infrastructure.Engines;
using Orchestrator.Infrastructure.Scheduling;
using Orchestrator.Infrastructure.Workflows;
using Orchestrator.Tests.Engines;
using Xunit;

namespace Orchestrator.Tests.Scheduling;

/// <summary>
/// Drives the real TaskRunnerJob/ApplyStateTransitionAsync against a Sqlite-backed DbContext, with
/// the real ClaudeCodeAdapter/GitWorktreeService wired to a FakeProcessRunner so canned engine
/// output (the same JSON shape the actual `claude` CLI prints) exercises the production state
/// machine end to end, rather than re-deriving its logic in the test.
/// </summary>
public class TaskRunnerJobTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    private const string VerifyFailResult =
        """{"type":"result","subtype":"success","result":"```json\n{\"verdict\": \"fail\", \"notes\": \"tests fail\", \"followUpInstruction\": \"fix it\"}\n```"}""";

    private const string EngineErrorResult =
        """{"type":"result","subtype":"error_during_execution"}""";

    private const string PlanResult =
        """{"type":"result","subtype":"success","result":"Here is the plan.\n```json\n{\"plan\": [\"step 1\", \"step 2\"], \"acceptanceCriteria\": [\"criterion 1\"]}\n```"}""";

    private static Project NewProject(bool requirePlanApproval = false) => new()
    {
        Name = "proj",
        RepoPath = "/repo",
        WorkerModel = "worker-model",
        SupervisorModel = "supervisor-model",
        RequirePlanApproval = requirePlanApproval,
    };

    private TaskRunnerJob MakeJob(IReadOnlyList<string> claudeStdout, IModelCircuitBreaker? circuitBreaker = null, SchedulerOptions? schedulerOptions = null)
    {
        var fakeRunner = new FakeProcessRunner(claudeStdout);
        var opts = schedulerOptions ?? new SchedulerOptions();
        return new TaskRunnerJob(
            _db.Context,
            new ClaudeCodeAdapter(fakeRunner, NullLogger<ClaudeCodeAdapter>.Instance),
            new AiderAdapter(fakeRunner, NullLogger<AiderAdapter>.Instance),
            new GitWorktreeService(fakeRunner, NullLogger<GitWorktreeService>.Instance),
            new RunCancellationRegistry(),
            Substitute.For<IEventBroadcaster>(),
            circuitBreaker ?? new ModelCircuitBreaker(Options.Create(opts)),
            Substitute.For<IWorkflowEngine>(),
            Options.Create(opts),
            NullLogger<TaskRunnerJob>.Instance);
    }

    [Fact]
    public async Task VerifyFail_BelowMaxRetries_LoopsBackToNeedsFix_WithoutDeadLettering()
    {
        var project = NewProject();
        var task = new AgentTask
        {
            ProjectId = project.Id,
            Title = "t",
            State = TaskState.Verifying,
            RetryCount = 0,
        };
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.Add(task);
        await _db.Context.SaveChangesAsync();

        await MakeJob([VerifyFailResult]).ExecuteSupervisorTaskAsync(task.Id);

        var reloaded = await _db.Context.Tasks.FindAsync(task.Id);
        Assert.Equal(TaskState.NeedsFix, reloaded!.State);
        Assert.Equal(1, reloaded.RetryCount);
    }

    [Fact]
    public async Task VerifyFail_PastMaxRetries_DeadLettersInsteadOfLoopingForever()
    {
        var project = NewProject();
        var task = new AgentTask
        {
            ProjectId = project.Id,
            Title = "t",
            State = TaskState.Verifying,
            RetryCount = AgentTask.MaxRetries,
        };
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.Add(task);
        await _db.Context.SaveChangesAsync();

        await MakeJob([VerifyFailResult]).ExecuteSupervisorTaskAsync(task.Id);

        var reloaded = await _db.Context.Tasks.FindAsync(task.Id);
        Assert.Equal(TaskState.DeadLetter, reloaded!.State);
    }

    [Fact]
    public async Task VerifyFail_PastMaxRetries_PropagatesDeadLetterToDecomposedParent()
    {
        var project = NewProject();
        var parent = new AgentTask { ProjectId = project.Id, Title = "parent", State = TaskState.Decomposed };
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.Add(parent);
        await _db.Context.SaveChangesAsync();

        var child = new AgentTask
        {
            ProjectId = project.Id,
            ParentTaskId = parent.Id,
            Title = "child",
            State = TaskState.Verifying,
            RetryCount = AgentTask.MaxRetries,
        };
        _db.Context.Tasks.Add(child);
        await _db.Context.SaveChangesAsync();

        await MakeJob([VerifyFailResult]).ExecuteSupervisorTaskAsync(child.Id);

        var reloadedParent = await _db.Context.Tasks.FindAsync(parent.Id);
        Assert.Equal(TaskState.DeadLetter, reloadedParent!.State);
    }

    [Fact]
    public async Task ImplementFail_SetsNextAttemptAt_SoTaskBacksOffBeforeRetrying()
    {
        var project = NewProject();
        var task = new AgentTask
        {
            ProjectId = project.Id,
            Title = "t",
            State = TaskState.InProgress,
            RetryCount = 0,
        };
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.Add(task);
        await _db.Context.SaveChangesAsync();

        var before = DateTimeOffset.UtcNow;
        await MakeJob([EngineErrorResult]).ExecuteWorkerTaskAsync(task.Id);

        var reloaded = await _db.Context.Tasks.FindAsync(task.Id);
        Assert.Equal(TaskState.ReadyForWork, reloaded!.State);
        Assert.NotNull(reloaded.NextAttemptAt);
        Assert.True(reloaded.NextAttemptAt > before);
    }

    [Fact]
    public async Task Execute_RequeuesWithoutRunningEngine_WhenCircuitBreakerIsOpenForModel()
    {
        var project = NewProject();
        var task = new AgentTask
        {
            ProjectId = project.Id,
            Title = "t",
            State = TaskState.InProgress,
            LockedBy = "worker-1",
            RetryCount = 0,
        };
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.Add(task);
        await _db.Context.SaveChangesAsync();

        var breaker = new ModelCircuitBreaker(Options.Create(new SchedulerOptions { CircuitBreakerFailureThreshold = 1 }));
        breaker.RecordFailure(project.WorkerModel);
        Assert.True(breaker.IsOpen(project.WorkerModel));

        await MakeJob([EngineErrorResult], circuitBreaker: breaker).ExecuteWorkerTaskAsync(task.Id);

        var reloaded = await _db.Context.Tasks.FindAsync(task.Id);
        Assert.Equal(TaskState.ReadyForWork, reloaded!.State);
        Assert.Equal(0, reloaded.RetryCount);
        Assert.Null(reloaded.LockedBy);
        Assert.NotNull(reloaded.NextAttemptAt);
        Assert.Empty(_db.Context.Runs);
    }

    [Fact]
    public async Task Execute_RecordsCircuitBreakerFailure_WhenEngineRunFails()
    {
        var project = NewProject();
        var task = new AgentTask { ProjectId = project.Id, Title = "t", State = TaskState.InProgress };
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.Add(task);
        await _db.Context.SaveChangesAsync();

        var breaker = new ModelCircuitBreaker(Options.Create(new SchedulerOptions { CircuitBreakerFailureThreshold = 1 }));

        await MakeJob([EngineErrorResult], circuitBreaker: breaker).ExecuteWorkerTaskAsync(task.Id);

        Assert.True(breaker.IsOpen(project.WorkerModel));
    }

    [Fact]
    public async Task VerifyPass_MovesTaskToDone()
    {
        const string verifyPass =
            """{"type":"result","subtype":"success","result":"```json\n{\"verdict\": \"pass\", \"notes\": \"looks good\"}\n```"}""";

        var project = NewProject();
        var task = new AgentTask { ProjectId = project.Id, Title = "t", State = TaskState.Verifying };
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.Add(task);
        await _db.Context.SaveChangesAsync();

        await MakeJob([verifyPass]).ExecuteSupervisorTaskAsync(task.Id);

        var reloaded = await _db.Context.Tasks.FindAsync(task.Id);
        Assert.Equal(TaskState.Done, reloaded!.State);
    }

    [Fact]
    public async Task PlanSucceeds_DecomposesImmediately_WhenProjectDoesNotRequirePlanApproval()
    {
        var project = NewProject();
        var task = new AgentTask { ProjectId = project.Id, Title = "t", State = TaskState.Planning };
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.Add(task);
        await _db.Context.SaveChangesAsync();

        await MakeJob([PlanResult]).ExecuteSupervisorTaskAsync(task.Id);

        var reloaded = await _db.Context.Tasks.FindAsync(task.Id);
        Assert.Equal(TaskState.Decomposed, reloaded!.State);
        var children = await _db.Context.Tasks.Where(t => t.ParentTaskId == task.Id).OrderBy(c => c.Title).ToListAsync();
        Assert.Equal(2, children.Count);
        Assert.Equal(TaskState.ReadyForWork, children[0].State);
        Assert.Null(children[0].DependsOnTaskId);
        Assert.Equal(TaskState.Blocked, children[1].State);
        Assert.Equal(children[0].Id, children[1].DependsOnTaskId);
    }

    [Fact]
    public async Task VerifyPass_OnFirstStep_UnblocksSecondStep_SoStepsRunSequentially()
    {
        var project = NewProject();
        var task = new AgentTask { ProjectId = project.Id, Title = "t", State = TaskState.Planning };
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.Add(task);
        await _db.Context.SaveChangesAsync();

        await MakeJob([PlanResult]).ExecuteSupervisorTaskAsync(task.Id);
        var children = await _db.Context.Tasks.Where(t => t.ParentTaskId == task.Id).OrderBy(c => c.Title).ToListAsync();
        var firstStep = children[0];
        var secondStep = children[1];
        Assert.Equal(TaskState.Blocked, secondStep.State);

        firstStep.State = TaskState.Verifying;
        await _db.Context.SaveChangesAsync();

        const string verifyPass =
            """{"type":"result","subtype":"success","result":"```json\n{\"verdict\": \"pass\", \"notes\": \"looks good\"}\n```"}""";
        await MakeJob([verifyPass]).ExecuteSupervisorTaskAsync(firstStep.Id);

        var reloadedSecondStep = await _db.Context.Tasks.FindAsync(secondStep.Id);
        Assert.Equal(TaskState.ReadyForWork, reloadedSecondStep!.State);
    }

    [Fact]
    public async Task PlanSucceeds_WaitsForApproval_WhenProjectRequiresPlanApproval()
    {
        var project = NewProject(requirePlanApproval: true);
        var task = new AgentTask { ProjectId = project.Id, Title = "t", State = TaskState.Planning };
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.Add(task);
        await _db.Context.SaveChangesAsync();

        await MakeJob([PlanResult]).ExecuteSupervisorTaskAsync(task.Id);

        var reloaded = await _db.Context.Tasks.FindAsync(task.Id);
        Assert.Equal(TaskState.AwaitingInput, reloaded!.State);
        Assert.NotNull(reloaded.PlanJson);
        Assert.Empty(await _db.Context.Tasks.Where(t => t.ParentTaskId == task.Id).ToListAsync());

        var approval = await _db.Context.Approvals.SingleAsync(a => a.TaskId == task.Id);
        Assert.Equal(ApprovalKind.PlanReview, approval.Kind);
        Assert.Equal(ApprovalStatus.Pending, approval.Status);
    }
}
