using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Orchestrator.Api.Controllers;
using Orchestrator.Api.Dtos;
using Orchestrator.Domain;
using Orchestrator.Infrastructure.Scheduling;
using Orchestrator.Tests.Scheduling;
using Xunit;

namespace Orchestrator.Tests.Controllers;

public class SystemHealthControllerTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    private static Project NewProject(bool isPaused = false) => new()
    {
        Name = "proj",
        RepoPath = "/repo",
        WorkerModel = "worker-model",
        SupervisorModel = "supervisor-model",
        IsPaused = isPaused,
    };

    private SystemHealthController MakeController(SchedulerOptions? options = null) =>
        new(_db.Context, Options.Create(options ?? new SchedulerOptions()));

    [Fact]
    public async Task Get_ReportsQueueDepthAndInFlight_PerLane()
    {
        var project = NewProject();
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.AddRange(
            new AgentTask { ProjectId = project.Id, Title = "queued", Lane = Lane.Worker, State = TaskState.ReadyForWork },
            new AgentTask
            {
                ProjectId = project.Id, Title = "running", Lane = Lane.Worker, State = TaskState.InProgress,
                LockedBy = "w1", LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
            },
            new AgentTask { ProjectId = project.Id, Title = "queued-sup", Lane = Lane.Supervisor, State = TaskState.Queued });
        await _db.Context.SaveChangesAsync();

        var result = await MakeController(new SchedulerOptions { WorkerConcurrency = 2, SupervisorConcurrency = 3 })
            .Get(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SystemHealthDto>(ok.Value);
        Assert.True(dto.DatabaseHealthy);
        Assert.Equal(1, dto.Worker.QueueDepth);
        Assert.Equal(1, dto.Worker.InFlight);
        Assert.Equal(2, dto.Worker.Capacity);
        Assert.Equal(1, dto.Supervisor.QueueDepth);
        Assert.Equal(0, dto.Supervisor.InFlight);
    }

    [Fact]
    public async Task Get_CountsStaleLeasesDeadLettersAndPausedProjects()
    {
        var activeProject = NewProject();
        var pausedProject = NewProject(isPaused: true);
        _db.Context.Projects.AddRange(activeProject, pausedProject);
        _db.Context.Tasks.AddRange(
            new AgentTask
            {
                ProjectId = activeProject.Id, Title = "stale", Lane = Lane.Worker, State = TaskState.InProgress,
                LockedBy = "crashed", LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            },
            new AgentTask { ProjectId = activeProject.Id, Title = "dead", Lane = Lane.Worker, State = TaskState.DeadLetter });
        await _db.Context.SaveChangesAsync();

        var result = await MakeController().Get(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SystemHealthDto>(ok.Value);
        Assert.Equal(1, dto.StaleLeaseCount);
        Assert.Equal(1, dto.DeadLetterCount);
        Assert.Equal(1, dto.PausedProjectCount);
    }
}
