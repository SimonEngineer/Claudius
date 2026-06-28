using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchestrator.Domain;
using Orchestrator.Infrastructure.Scheduling;
using Xunit;

namespace Orchestrator.Tests.Scheduling;

public class TaskClaimingServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    private static Project NewProject(int maxWorkerConcurrency = 1) => new()
    {
        Name = "proj",
        RepoPath = "/repo",
        WorkerModel = "worker-model",
        SupervisorModel = "supervisor-model",
        MaxWorkerConcurrency = maxWorkerConcurrency,
    };

    private static AgentTask NewTask(Project project, TaskState state, Lane lane = Lane.Worker, int priority = 0) => new()
    {
        ProjectId = project.Id,
        Title = "do thing",
        Lane = lane,
        State = state,
        Priority = priority,
    };

    private TaskClaimingService MakeService(SchedulerOptions? options = null) =>
        new(_db.Context, Options.Create(options ?? new SchedulerOptions()), NullLogger<TaskClaimingService>.Instance);

    [Fact]
    public async Task ClaimNextBatchAsync_ClaimsUpToFreeCapacity_AndSetsLeaseAndState()
    {
        var project = NewProject(maxWorkerConcurrency: 10);
        var tasks = Enumerable.Range(0, 3).Select(_ => NewTask(project, TaskState.ReadyForWork)).ToList();
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.AddRange(tasks);
        await _db.Context.SaveChangesAsync();

        var service = MakeService(new SchedulerOptions { WorkerConcurrency = 2 });
        var claimed = await service.ClaimNextBatchAsync(Lane.Worker, CancellationToken.None);

        Assert.Equal(2, claimed.Count);
        Assert.All(claimed, t => Assert.Equal(TaskState.InProgress, t.State));
        Assert.All(claimed, t => Assert.NotNull(t.LockedBy));
        Assert.All(claimed, t => Assert.NotNull(t.LeaseExpiresAt));
    }

    [Fact]
    public async Task ClaimNextBatchAsync_SkipsAlreadyLockedTasks()
    {
        var project = NewProject(maxWorkerConcurrency: 10);
        var locked = NewTask(project, TaskState.ReadyForWork);
        locked.LockedBy = "someone-else";
        locked.LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10);
        var free = NewTask(project, TaskState.ReadyForWork);
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.AddRange(locked, free);
        await _db.Context.SaveChangesAsync();

        var service = MakeService(new SchedulerOptions { WorkerConcurrency = 5 });
        var claimed = await service.ClaimNextBatchAsync(Lane.Worker, CancellationToken.None);

        Assert.Single(claimed);
        Assert.Equal(free.Id, claimed[0].Id);
    }

    [Fact]
    public async Task ClaimNextBatchAsync_RespectsPerProjectConcurrencyCap_EvenWithFreeLaneSlots()
    {
        var busyProject = NewProject(maxWorkerConcurrency: 1);
        var quietProject = NewProject(maxWorkerConcurrency: 1);
        var alreadyRunning = NewTask(busyProject, TaskState.InProgress);
        alreadyRunning.LockedBy = "worker-1";
        alreadyRunning.LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10);
        var busyProjectCandidate = NewTask(busyProject, TaskState.ReadyForWork);
        var quietProjectCandidate = NewTask(quietProject, TaskState.ReadyForWork);

        _db.Context.Projects.AddRange(busyProject, quietProject);
        _db.Context.Tasks.AddRange(alreadyRunning, busyProjectCandidate, quietProjectCandidate);
        await _db.Context.SaveChangesAsync();

        var service = MakeService(new SchedulerOptions { WorkerConcurrency = 5 });
        var claimed = await service.ClaimNextBatchAsync(Lane.Worker, CancellationToken.None);

        Assert.Single(claimed);
        Assert.Equal(quietProjectCandidate.Id, claimed[0].Id);
    }

    [Fact]
    public async Task ClaimNextBatchAsync_UsesPerLaneLeaseDuration()
    {
        var project = NewProject();
        var supervisorTask = NewTask(project, TaskState.Queued, Lane.Supervisor);
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.Add(supervisorTask);
        await _db.Context.SaveChangesAsync();

        var opts = new SchedulerOptions { SupervisorRunTimeout = TimeSpan.FromMinutes(5), LeaseBuffer = TimeSpan.FromMinutes(2) };
        var service = MakeService(opts);
        var before = DateTimeOffset.UtcNow;
        var claimed = await service.ClaimNextBatchAsync(Lane.Supervisor, CancellationToken.None);

        var lease = claimed[0].LeaseExpiresAt!.Value - before;
        Assert.True(lease >= TimeSpan.FromMinutes(6.9) && lease <= TimeSpan.FromMinutes(7.1),
            $"expected lease ~= SupervisorRunTimeout + LeaseBuffer (7m), got {lease}");
    }

    [Fact]
    public async Task RequeueStaleLeasesAsync_RevertsStateAndIncrementsRetryCount_WhenBelowMaxRetries()
    {
        var project = NewProject();
        var task = NewTask(project, TaskState.InProgress);
        task.LockedBy = "crashed-worker";
        task.LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        task.RetryCount = 0;
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.Add(task);
        await _db.Context.SaveChangesAsync();

        var service = MakeService();
        var requeued = await service.RequeueStaleLeasesAsync(CancellationToken.None);

        Assert.Equal(1, requeued);
        var reloaded = await _db.Context.Tasks.FindAsync(task.Id);
        Assert.Equal(TaskState.ReadyForWork, reloaded!.State);
        Assert.Equal(1, reloaded.RetryCount);
        Assert.Null(reloaded.LockedBy);
        Assert.Null(reloaded.LeaseExpiresAt);
    }

    [Fact]
    public async Task RequeueStaleLeasesAsync_SetsBackoff_SoTaskIsNotImmediatelyClaimable()
    {
        var project = NewProject();
        var task = NewTask(project, TaskState.InProgress);
        task.LockedBy = "crashed-worker";
        task.LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        task.RetryCount = 0;
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.Add(task);
        await _db.Context.SaveChangesAsync();

        var service = MakeService();
        await service.RequeueStaleLeasesAsync(CancellationToken.None);

        var reloaded = await _db.Context.Tasks.FindAsync(task.Id);
        Assert.NotNull(reloaded!.NextAttemptAt);
        Assert.True(reloaded.NextAttemptAt > DateTimeOffset.UtcNow);

        var claimed = await service.ClaimNextBatchAsync(Lane.Worker, CancellationToken.None);
        Assert.Empty(claimed);
    }

    [Fact]
    public async Task ClaimNextBatchAsync_ClaimsTask_OnceBackoffWindowHasPassed()
    {
        var project = NewProject();
        var task = NewTask(project, TaskState.ReadyForWork);
        task.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.Add(task);
        await _db.Context.SaveChangesAsync();

        var service = MakeService();
        var claimed = await service.ClaimNextBatchAsync(Lane.Worker, CancellationToken.None);

        Assert.Single(claimed);
        Assert.Null(claimed[0].NextAttemptAt);
    }

    [Fact]
    public async Task ClaimNextBatchAsync_ExcludesTasks_WhenProjectIsPaused()
    {
        var pausedProject = NewProject();
        pausedProject.IsPaused = true;
        var activeProject = NewProject();
        var pausedTask = NewTask(pausedProject, TaskState.ReadyForWork);
        var activeTask = NewTask(activeProject, TaskState.ReadyForWork);

        _db.Context.Projects.AddRange(pausedProject, activeProject);
        _db.Context.Tasks.AddRange(pausedTask, activeTask);
        await _db.Context.SaveChangesAsync();

        var service = MakeService(new SchedulerOptions { WorkerConcurrency = 5 });
        var claimed = await service.ClaimNextBatchAsync(Lane.Worker, CancellationToken.None);

        Assert.Single(claimed);
        Assert.Equal(activeTask.Id, claimed[0].Id);
    }

    [Fact]
    public async Task RequeueStaleLeasesAsync_DeadLetters_WhenRetryCountExceedsMax()
    {
        var project = NewProject();
        var task = NewTask(project, TaskState.InProgress);
        task.LockedBy = "crashed-worker";
        task.LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        task.RetryCount = AgentTask.MaxRetries;
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.Add(task);
        await _db.Context.SaveChangesAsync();

        var service = MakeService();
        await service.RequeueStaleLeasesAsync(CancellationToken.None);

        var reloaded = await _db.Context.Tasks.FindAsync(task.Id);
        Assert.Equal(TaskState.DeadLetter, reloaded!.State);
    }
}
