using NSubstitute;
using Orchestrator.Domain;
using Orchestrator.Domain.Streaming;
using Orchestrator.Infrastructure.Scheduling;
using Xunit;

namespace Orchestrator.Tests.Scheduling;

public class TaskLifecycleServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly IEventBroadcaster _broadcaster = Substitute.For<IEventBroadcaster>();
    private readonly IRunCancellationRegistry _cancellationRegistry = new RunCancellationRegistry();

    public void Dispose() => _db.Dispose();

    private static Project NewProject() => new()
    {
        Name = "proj",
        RepoPath = "/repo",
        WorkerModel = "worker-model",
        SupervisorModel = "supervisor-model",
    };

    private TaskLifecycleService MakeService() => new(_db.Context, _cancellationRegistry, _broadcaster);

    [Fact]
    public async Task CancelAsync_ReturnsNull_WhenTaskNotFound()
    {
        var result = await MakeService().CancelAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Null(result);
    }

    [Theory]
    [InlineData(TaskState.Done)]
    [InlineData(TaskState.Failed)]
    [InlineData(TaskState.DeadLetter)]
    public async Task CancelAsync_ReturnsFalse_WhenAlreadyTerminal(TaskState terminalState)
    {
        var project = NewProject();
        var task = new AgentTask { ProjectId = project.Id, Title = "t", State = terminalState };
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.Add(task);
        await _db.Context.SaveChangesAsync();

        var result = await MakeService().CancelAsync(task.Id, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task CancelAsync_DeadLettersTheTask()
    {
        var project = NewProject();
        var task = new AgentTask { ProjectId = project.Id, Title = "t", State = TaskState.InProgress, LockedBy = "w" };
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.Add(task);
        await _db.Context.SaveChangesAsync();

        var result = await MakeService().CancelAsync(task.Id, CancellationToken.None);

        Assert.True(result);
        var reloaded = await _db.Context.Tasks.FindAsync(task.Id);
        Assert.Equal(TaskState.DeadLetter, reloaded!.State);
        Assert.Null(reloaded.LockedBy);
    }

    [Fact]
    public async Task CancelAsync_CancellingDecomposedParent_CascadesToStillActiveChildren()
    {
        var project = NewProject();
        var parent = new AgentTask { ProjectId = project.Id, Title = "parent", State = TaskState.Decomposed };
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.Add(parent);
        await _db.Context.SaveChangesAsync();

        var activeChild = new AgentTask { ProjectId = project.Id, ParentTaskId = parent.Id, Title = "child-active", State = TaskState.InProgress };
        var doneChild = new AgentTask { ProjectId = project.Id, ParentTaskId = parent.Id, Title = "child-done", State = TaskState.Done };
        _db.Context.Tasks.AddRange(activeChild, doneChild);
        await _db.Context.SaveChangesAsync();

        await MakeService().CancelAsync(parent.Id, CancellationToken.None);

        var reloadedParent = await _db.Context.Tasks.FindAsync(parent.Id);
        var reloadedActiveChild = await _db.Context.Tasks.FindAsync(activeChild.Id);
        var reloadedDoneChild = await _db.Context.Tasks.FindAsync(doneChild.Id);

        Assert.Equal(TaskState.DeadLetter, reloadedParent!.State);
        Assert.Equal(TaskState.DeadLetter, reloadedActiveChild!.State);
        // Already-terminal sibling must be left alone -- cancellation shouldn't rewrite finished work.
        Assert.Equal(TaskState.Done, reloadedDoneChild!.State);
    }

    [Fact]
    public async Task CancelAsync_CancellingAChild_PropagatesDeadLetterToDecomposedParent()
    {
        var project = NewProject();
        var parent = new AgentTask { ProjectId = project.Id, Title = "parent", State = TaskState.Decomposed };
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.Add(parent);
        await _db.Context.SaveChangesAsync();

        var child = new AgentTask { ProjectId = project.Id, ParentTaskId = parent.Id, Title = "child", State = TaskState.InProgress };
        _db.Context.Tasks.Add(child);
        await _db.Context.SaveChangesAsync();

        await MakeService().CancelAsync(child.Id, CancellationToken.None);

        var reloadedParent = await _db.Context.Tasks.FindAsync(parent.Id);
        Assert.Equal(TaskState.DeadLetter, reloadedParent!.State);
    }

    [Fact]
    public async Task CancelAsync_DoesNotTouchParent_WhenParentIsNotDecomposed()
    {
        var project = NewProject();
        var parent = new AgentTask { ProjectId = project.Id, Title = "parent", State = TaskState.Verifying };
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.Add(parent);
        await _db.Context.SaveChangesAsync();

        var child = new AgentTask { ProjectId = project.Id, ParentTaskId = parent.Id, Title = "child", State = TaskState.InProgress };
        _db.Context.Tasks.Add(child);
        await _db.Context.SaveChangesAsync();

        await MakeService().CancelAsync(child.Id, CancellationToken.None);

        var reloadedParent = await _db.Context.Tasks.FindAsync(parent.Id);
        Assert.Equal(TaskState.Verifying, reloadedParent!.State);
    }
}
