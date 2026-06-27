using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Orchestrator.Api.Controllers;
using Orchestrator.Api.Dtos;
using Orchestrator.Domain;
using Orchestrator.Domain.Streaming;
using Orchestrator.Tests.Scheduling;
using Xunit;

namespace Orchestrator.Tests.Controllers;

public class ApprovalsControllerTests : IDisposable
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

    private ApprovalsController MakeController() => new(_db.Context, Substitute.For<IEventBroadcaster>());

    [Fact]
    public async Task Approve_PlanReview_DecomposesPlanAndClearsAwaitingInput()
    {
        var project = NewProject();
        var task = new AgentTask
        {
            ProjectId = project.Id,
            Title = "t",
            State = TaskState.AwaitingInput,
            PlanJson = """{"plan": ["step 1", "step 2"], "acceptanceCriteria": ["criterion 1"]}""",
        };
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.Add(task);
        await _db.Context.SaveChangesAsync();

        var approval = new Approval { TaskId = task.Id, Kind = ApprovalKind.PlanReview, Question = "Review the plan" };
        _db.Context.Approvals.Add(approval);
        await _db.Context.SaveChangesAsync();

        var result = await MakeController().Approve(approval.Id, new ResolveApprovalRequest(null), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var reloaded = await _db.Context.Tasks.FindAsync(task.Id);
        Assert.Equal(TaskState.Decomposed, reloaded!.State);
        var children = await _db.Context.Tasks.Where(t => t.ParentTaskId == task.Id).ToListAsync();
        Assert.Equal(2, children.Count);
    }

    [Fact]
    public async Task Reject_PlanReview_SendsTaskBackToQueued_WithoutDeadLettering()
    {
        var project = NewProject();
        var task = new AgentTask
        {
            ProjectId = project.Id,
            Title = "t",
            State = TaskState.AwaitingInput,
            PlanJson = """{"plan": ["step 1"], "acceptanceCriteria": ["criterion 1"]}""",
        };
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.Add(task);
        await _db.Context.SaveChangesAsync();

        var approval = new Approval { TaskId = task.Id, Kind = ApprovalKind.PlanReview, Question = "Review the plan" };
        _db.Context.Approvals.Add(approval);
        await _db.Context.SaveChangesAsync();

        var result = await MakeController().Reject(approval.Id, new ResolveApprovalRequest("needs more detail"), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var reloaded = await _db.Context.Tasks.FindAsync(task.Id);
        Assert.Equal(TaskState.Queued, reloaded!.State);
        Assert.Null(reloaded.PlanJson);
        Assert.Contains("needs more detail", reloaded.Description);
    }

    [Fact]
    public async Task Approve_WorkerInput_StillFlipsToReadyForWork()
    {
        var project = NewProject();
        var task = new AgentTask { ProjectId = project.Id, Title = "t", State = TaskState.AwaitingInput };
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.Add(task);
        await _db.Context.SaveChangesAsync();

        var approval = new Approval { TaskId = task.Id, Question = "Need input" };
        _db.Context.Approvals.Add(approval);
        await _db.Context.SaveChangesAsync();

        await MakeController().Approve(approval.Id, new ResolveApprovalRequest("here you go"), CancellationToken.None);

        var reloaded = await _db.Context.Tasks.FindAsync(task.Id);
        Assert.Equal(TaskState.ReadyForWork, reloaded!.State);
    }

    [Fact]
    public async Task Approve_RecordsAuditLogEntry_WithResolvedByAsActor()
    {
        var project = NewProject();
        var task = new AgentTask { ProjectId = project.Id, Title = "t", State = TaskState.AwaitingInput };
        _db.Context.Projects.Add(project);
        _db.Context.Tasks.Add(task);
        await _db.Context.SaveChangesAsync();

        var approval = new Approval { TaskId = task.Id, Question = "Need input" };
        _db.Context.Approvals.Add(approval);
        await _db.Context.SaveChangesAsync();

        await MakeController().Approve(approval.Id, new ResolveApprovalRequest("here you go", "alice"), CancellationToken.None);

        var entry = Assert.Single(_db.Context.AuditLogEntries);
        Assert.Equal("WorkerInput.Approved", entry.Action);
        Assert.Equal("alice", entry.Actor);
        Assert.Equal(task.Id, entry.TaskId);
    }
}
