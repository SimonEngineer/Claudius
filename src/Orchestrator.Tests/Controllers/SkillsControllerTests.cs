using Microsoft.AspNetCore.Mvc;
using Orchestrator.Api.Controllers;
using Orchestrator.Domain;
using Orchestrator.Tests.Scheduling;
using Xunit;

namespace Orchestrator.Tests.Controllers;

public class SkillsControllerTests : IDisposable
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
    public async Task List_ReturnsSkillsForProject_NewestFirst()
    {
        var project = NewProject();
        var other = NewProject();
        _db.Context.Projects.AddRange(project, other);
        var older = new Skill { ProjectId = project.Id, Name = "a", Content = "do a", CreatedAt = DateTimeOffset.UtcNow.AddDays(-1) };
        var newer = new Skill { ProjectId = project.Id, Name = "b", Content = "do b", CreatedAt = DateTimeOffset.UtcNow };
        var otherProjectSkill = new Skill { ProjectId = other.Id, Name = "c", Content = "do c" };
        _db.Context.Skills.AddRange(older, newer, otherProjectSkill);
        await _db.Context.SaveChangesAsync();

        var controller = new SkillsController(_db.Context);
        var result = await controller.List(project.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var skills = Assert.IsAssignableFrom<IEnumerable<Skill>>(ok.Value).ToList();
        Assert.Equal([newer.Id, older.Id], skills.Select(s => s.Id));
    }

    [Fact]
    public async Task Delete_RemovesSkill_WhenItBelongsToProject()
    {
        var project = NewProject();
        _db.Context.Projects.Add(project);
        var skill = new Skill { ProjectId = project.Id, Name = "a", Content = "do a" };
        _db.Context.Skills.Add(skill);
        await _db.Context.SaveChangesAsync();

        var controller = new SkillsController(_db.Context);
        var result = await controller.Delete(project.Id, skill.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await _db.Context.Skills.FindAsync(skill.Id));
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenSkillBelongsToDifferentProject()
    {
        var project = NewProject();
        var other = NewProject();
        _db.Context.Projects.AddRange(project, other);
        var skill = new Skill { ProjectId = other.Id, Name = "a", Content = "do a" };
        _db.Context.Skills.Add(skill);
        await _db.Context.SaveChangesAsync();

        var controller = new SkillsController(_db.Context);
        var result = await controller.Delete(project.Id, skill.Id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
