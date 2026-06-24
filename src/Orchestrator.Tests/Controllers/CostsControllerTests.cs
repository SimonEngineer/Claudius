using Microsoft.AspNetCore.Mvc;
using Orchestrator.Api.Controllers;
using Orchestrator.Api.Dtos;
using Orchestrator.Domain;
using Orchestrator.Tests.Scheduling;
using Xunit;

namespace Orchestrator.Tests.Controllers;

public class CostsControllerTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    private static Project NewProject(string name) => new()
    {
        Name = name,
        RepoPath = "/repo",
        WorkerModel = "worker-model",
        SupervisorModel = "supervisor-model",
    };

    [Fact]
    public async Task Get_SumsCostAndTokensPerProject_AcrossMultipleRuns()
    {
        var projectA = NewProject("a");
        var projectB = NewProject("b");
        _db.Context.Projects.AddRange(projectA, projectB);

        var taskA = new AgentTask { ProjectId = projectA.Id, Title = "ta", State = TaskState.Done };
        var taskB = new AgentTask { ProjectId = projectB.Id, Title = "tb", State = TaskState.Done };
        _db.Context.Tasks.AddRange(taskA, taskB);
        await _db.Context.SaveChangesAsync();

        _db.Context.Runs.AddRange(
            new Run { TaskId = taskA.Id, Engine = EngineType.ClaudeCode, Model = "m", Status = RunStatus.Succeeded, CostUsd = 1.50m, TokensIn = 100, TokensOut = 50 },
            new Run { TaskId = taskA.Id, Engine = EngineType.ClaudeCode, Model = "m", Status = RunStatus.Succeeded, CostUsd = 0.50m, TokensIn = 10, TokensOut = 5 },
            new Run { TaskId = taskB.Id, Engine = EngineType.Aider, Model = "m", Status = RunStatus.Succeeded, CostUsd = null, TokensIn = null, TokensOut = null });
        await _db.Context.SaveChangesAsync();

        var result = await new CostsController(_db.Context).Get(CancellationToken.None);
        var summary = Assert.IsType<CostSummaryDto>(Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.Equal(2.00m, summary.TotalCostUsd);
        Assert.Equal(110, summary.TotalTokensIn);
        Assert.Equal(55, summary.TotalTokensOut);

        var projectACost = summary.ByProject.Single(p => p.ProjectId == projectA.Id);
        Assert.Equal(2.00m, projectACost.CostUsd);
        Assert.Equal(2, projectACost.RunCount);

        var projectBCost = summary.ByProject.Single(p => p.ProjectId == projectB.Id);
        Assert.Equal(0m, projectBCost.CostUsd);
        Assert.Equal(1, projectBCost.RunCount);
    }

    [Fact]
    public async Task Get_IncludesTodayInLast30Days_WhenARunHappenedToday()
    {
        var project = NewProject("a");
        _db.Context.Projects.Add(project);
        var task = new AgentTask { ProjectId = project.Id, Title = "t", State = TaskState.Done };
        _db.Context.Tasks.Add(task);
        await _db.Context.SaveChangesAsync();

        _db.Context.Runs.Add(new Run
        {
            TaskId = task.Id,
            Engine = EngineType.ClaudeCode,
            Model = "m",
            Status = RunStatus.Succeeded,
            CostUsd = 3.00m,
            StartedAt = DateTimeOffset.UtcNow,
        });
        await _db.Context.SaveChangesAsync();

        var result = await new CostsController(_db.Context).Get(CancellationToken.None);
        var summary = Assert.IsType<CostSummaryDto>(Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.Contains(summary.Last30Days, d => d.Date == DateOnly.FromDateTime(DateTime.UtcNow) && d.CostUsd == 3.00m);
    }
}
