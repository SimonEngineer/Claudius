using Microsoft.AspNetCore.Mvc;
using Orchestrator.Api.Controllers;
using Orchestrator.Domain;
using Orchestrator.Tests.Scheduling;
using Xunit;

namespace Orchestrator.Tests.Controllers;

public class AuditLogControllerTests : IDisposable
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
    public async Task List_ReturnsEntriesNewestFirst()
    {
        var project = NewProject();
        _db.Context.Projects.Add(project);
        var older = new AuditLogEntry { ProjectId = project.Id, Action = "a", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5) };
        var newer = new AuditLogEntry { ProjectId = project.Id, Action = "b", CreatedAt = DateTimeOffset.UtcNow };
        _db.Context.AuditLogEntries.AddRange(older, newer);
        await _db.Context.SaveChangesAsync();

        var controller = new AuditLogController(_db.Context);
        var result = await controller.List(null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var entries = Assert.IsAssignableFrom<IEnumerable<AuditLogEntry>>(ok.Value).ToList();
        Assert.Equal([newer.Id, older.Id], entries.Select(e => e.Id));
    }

    [Fact]
    public async Task List_FiltersByProjectId_WhenProvided()
    {
        var project = NewProject();
        var other = NewProject();
        _db.Context.Projects.AddRange(project, other);
        var mine = new AuditLogEntry { ProjectId = project.Id, Action = "a" };
        var theirs = new AuditLogEntry { ProjectId = other.Id, Action = "b" };
        _db.Context.AuditLogEntries.AddRange(mine, theirs);
        await _db.Context.SaveChangesAsync();

        var controller = new AuditLogController(_db.Context);
        var result = await controller.List(project.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var entries = Assert.IsAssignableFrom<IEnumerable<AuditLogEntry>>(ok.Value).ToList();
        Assert.Equal([mine.Id], entries.Select(e => e.Id));
    }
}
