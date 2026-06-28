using Microsoft.AspNetCore.Mvc;
using Orchestrator.Api.Controllers;
using Orchestrator.Domain;
using Orchestrator.Tests.Scheduling;
using Xunit;

namespace Orchestrator.Tests.Controllers;

public class ProjectsControllerTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    private static Project NewProject() => new()
    {
        Name = "proj",
        RepoPath = "/repo",
        WorkerModel = "worker-model",
        SupervisorModel = "supervisor-model",
    };

    [Fact]
    public async Task Pause_SetsIsPausedTrue()
    {
        var project = NewProject();
        _db.Context.Projects.Add(project);
        await _db.Context.SaveChangesAsync();

        var controller = new ProjectsController(_db.Context);
        var result = await controller.Pause(project.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<Project>(ok.Value);
        Assert.True(returned.IsPaused);
    }

    [Fact]
    public async Task Resume_SetsIsPausedFalse()
    {
        var project = NewProject();
        project.IsPaused = true;
        _db.Context.Projects.Add(project);
        await _db.Context.SaveChangesAsync();

        var controller = new ProjectsController(_db.Context);
        var result = await controller.Resume(project.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<Project>(ok.Value);
        Assert.False(returned.IsPaused);
    }

    [Fact]
    public async Task Pause_ReturnsNotFound_WhenProjectDoesNotExist()
    {
        var controller = new ProjectsController(_db.Context);
        var result = await controller.Pause(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Pause_RecordsAuditLogEntry()
    {
        var project = NewProject();
        _db.Context.Projects.Add(project);
        await _db.Context.SaveChangesAsync();

        var controller = new ProjectsController(_db.Context);
        await controller.Pause(project.Id, CancellationToken.None);

        var entry = Assert.Single(_db.Context.AuditLogEntries);
        Assert.Equal("Project.Paused", entry.Action);
        Assert.Equal(project.Id, entry.ProjectId);
    }
}
