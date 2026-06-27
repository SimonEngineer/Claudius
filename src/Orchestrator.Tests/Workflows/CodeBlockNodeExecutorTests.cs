using System.Text.Json;
using Orchestrator.Domain;
using Orchestrator.Infrastructure.Workflows;
using Orchestrator.Infrastructure.Workflows.Executors;
using Orchestrator.Tests.Scheduling;
using Xunit;

namespace Orchestrator.Tests.Workflows;

public class CodeBlockNodeExecutorTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ExecuteAsync_ReturnsScriptResult_WithAccessToTriggerPayload()
    {
        var executor = new CodeBlockNodeExecutor();
        using var config = JsonDocument.Parse("""{"code":"Trigger.GetProperty(\"title\").GetString()"}""");
        using var trigger = JsonDocument.Parse("""{"title":"build the thing"}""");

        var result = await executor.ExecuteAsync(new WorkflowNodeContext(config.RootElement, trigger.RootElement, _db.Context, CancellationToken.None));

        Assert.Equal("build the thing", result);
    }

    [Fact]
    public async Task ExecuteAsync_CanQueryDb_ViaInjectedDbContext()
    {
        _db.Context.Projects.Add(new Project { Name = "proj", RepoPath = "/repo", WorkerModel = "w", SupervisorModel = "s" });
        await _db.Context.SaveChangesAsync();

        var executor = new CodeBlockNodeExecutor();
        using var config = JsonDocument.Parse("""{"code":"Db.Projects.Count()"}""");
        using var trigger = JsonDocument.Parse("{}");

        var result = await executor.ExecuteAsync(new WorkflowNodeContext(config.RootElement, trigger.RootElement, _db.Context, CancellationToken.None));

        Assert.Equal("1", result);
    }

    [Fact]
    public async Task ExecuteAsync_Throws_WhenCodeIsMissing()
    {
        var executor = new CodeBlockNodeExecutor();
        using var config = JsonDocument.Parse("{}");
        using var trigger = JsonDocument.Parse("{}");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(new WorkflowNodeContext(config.RootElement, trigger.RootElement, _db.Context, CancellationToken.None)));
    }
}
