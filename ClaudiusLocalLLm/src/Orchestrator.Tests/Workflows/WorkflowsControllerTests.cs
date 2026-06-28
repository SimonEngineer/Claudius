using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Hangfire;
using NSubstitute;
using Orchestrator.Api.Controllers;
using Orchestrator.Api.Dtos;
using Orchestrator.Domain;
using Orchestrator.Infrastructure.Workflows;
using Orchestrator.Tests.Scheduling;
using Xunit;

namespace Orchestrator.Tests.Workflows;

public class WorkflowsControllerTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly IWorkflowEngine _engine = Substitute.For<IWorkflowEngine>();
    public void Dispose() => _db.Dispose();

    private WorkflowsController MakeController() =>
        new(_db.Context, _engine, new WorkflowCronSync(_db.Context, Substitute.For<IRecurringJobManager>()));

    private const string DefinitionWithHttpTrigger =
        """{"nodes":[{"id":"n1","type":"trigger.http","config":{},"x":0,"y":0}],"edges":[]}""";

    private const string DefinitionWithoutHttpTrigger =
        """{"nodes":[{"id":"n1","type":"trigger.event","config":{"eventName":"task.done"},"x":0,"y":0}],"edges":[]}""";

    [Fact]
    public void NodeTypes_ReturnsTheStaticCatalog()
    {
        var result = MakeController().NodeTypes();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(NodeTypeCatalog.All.Count, Assert.IsAssignableFrom<IEnumerable<NodeTypeDescriptor>>(ok.Value).Count());
    }

    [Fact]
    public async Task Create_PersistsWorkflow_AndReturnsCreatedAtAction()
    {
        var controller = MakeController();
        var request = new SaveWorkflowRequest("My workflow", "desc", DefinitionWithoutHttpTrigger, true);

        var result = await controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var workflow = Assert.IsType<Workflow>(created.Value);
        Assert.Equal("My workflow", workflow.Name);
        Assert.Single(_db.Context.Workflows);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenWorkflowDoesNotExist()
    {
        var result = await MakeController().Get(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Update_ModifiesExistingWorkflow()
    {
        var workflow = new Workflow { Name = "old", DefinitionJson = DefinitionWithoutHttpTrigger };
        _db.Context.Workflows.Add(workflow);
        await _db.Context.SaveChangesAsync();

        var controller = MakeController();
        var request = new SaveWorkflowRequest("new", null, DefinitionWithoutHttpTrigger, false);
        var result = await controller.Update(workflow.Id, request, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var reloaded = await _db.Context.Workflows.FindAsync(workflow.Id);
        Assert.Equal("new", reloaded!.Name);
        Assert.False(reloaded.IsEnabled);
    }

    [Fact]
    public async Task Delete_RemovesWorkflow()
    {
        var workflow = new Workflow { Name = "wf", DefinitionJson = DefinitionWithoutHttpTrigger };
        _db.Context.Workflows.Add(workflow);
        await _db.Context.SaveChangesAsync();

        var result = await MakeController().Delete(workflow.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(_db.Context.Workflows);
    }

    [Fact]
    public async Task Run_ReturnsNotFound_WhenWorkflowDoesNotExist()
    {
        var result = await MakeController().Run(Guid.NewGuid(), new RunWorkflowRequest(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Run_ExecutesWorkflowViaEngine_AndReturnsTheRun()
    {
        var workflow = new Workflow { Name = "wf", DefinitionJson = DefinitionWithoutHttpTrigger };
        _db.Context.Workflows.Add(workflow);
        await _db.Context.SaveChangesAsync();

        var run = new WorkflowRun { WorkflowId = workflow.Id };
        _db.Context.WorkflowRuns.Add(run);
        await _db.Context.SaveChangesAsync();
        _engine.ExecuteWorkflowAsync(workflow.Id, "{}", Arg.Any<CancellationToken>()).Returns(run.Id);

        var result = await MakeController().Run(workflow.Id, new RunWorkflowRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(run.Id, Assert.IsType<WorkflowRun>(ok.Value).Id);
    }

    [Fact]
    public async Task Webhook_ReturnsNotFound_WhenWorkflowDisabled()
    {
        var workflow = new Workflow { Name = "wf", IsEnabled = false, DefinitionJson = DefinitionWithHttpTrigger };
        _db.Context.Workflows.Add(workflow);
        await _db.Context.SaveChangesAsync();

        var controller = MakeController();
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await controller.Webhook(workflow.Id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Webhook_ReturnsConflict_WhenWorkflowHasNoHttpTriggerNode()
    {
        var workflow = new Workflow { Name = "wf", DefinitionJson = DefinitionWithoutHttpTrigger };
        _db.Context.Workflows.Add(workflow);
        await _db.Context.SaveChangesAsync();

        var controller = MakeController();
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await controller.Webhook(workflow.Id, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Webhook_ExecutesWorkflow_AndReturnsAccepted_WhenHttpTriggerNodeExists()
    {
        var workflow = new Workflow { Name = "wf", DefinitionJson = DefinitionWithHttpTrigger };
        _db.Context.Workflows.Add(workflow);
        await _db.Context.SaveChangesAsync();

        var controller = MakeController();
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await controller.Webhook(workflow.Id, CancellationToken.None);

        Assert.IsType<AcceptedResult>(result);
        await _engine.Received(1).ExecuteWorkflowAsync(workflow.Id, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
