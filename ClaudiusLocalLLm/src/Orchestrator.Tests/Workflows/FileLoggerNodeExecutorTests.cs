using System.Text.Json;
using Orchestrator.Infrastructure.Workflows;
using Orchestrator.Infrastructure.Workflows.Executors;
using Xunit;

namespace Orchestrator.Tests.Workflows;

public class FileLoggerNodeExecutorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "workflow-tests-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Theory]
    [InlineData("json")]
    [InlineData("csv")]
    [InlineData("txt")]
    public async Task ExecuteAsync_AppendsOneLine_InRequestedFormat(string format)
    {
        var executor = new FileLoggerNodeExecutor();
        var path = Path.Combine(_dir, $"log.{format}");

        using var config = JsonDocument.Parse($$"""{"path":"{{path.Replace("\\", "\\\\")}}","format":"{{format}}"}""");
        using var trigger = JsonDocument.Parse("""{"title":"build the thing"}""");

        await executor.ExecuteAsync(new WorkflowNodeContext(config.RootElement, trigger.RootElement, null!, CancellationToken.None));

        Assert.True(File.Exists(path));
        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("build the thing", content);
    }

    [Fact]
    public async Task ExecuteAsync_AppendsAcrossMultipleRuns()
    {
        var executor = new FileLoggerNodeExecutor();
        var path = Path.Combine(_dir, "log.json");

        using var config = JsonDocument.Parse($$"""{"path":"{{path.Replace("\\", "\\\\")}}","format":"json"}""");
        using var trigger = JsonDocument.Parse("{}");

        await executor.ExecuteAsync(new WorkflowNodeContext(config.RootElement, trigger.RootElement, null!, CancellationToken.None));
        await executor.ExecuteAsync(new WorkflowNodeContext(config.RootElement, trigger.RootElement, null!, CancellationToken.None));

        var lines = await File.ReadAllLinesAsync(path);
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public async Task ExecuteAsync_Throws_WhenPathIsMissing()
    {
        var executor = new FileLoggerNodeExecutor();
        using var config = JsonDocument.Parse("""{"format":"json"}""");
        using var trigger = JsonDocument.Parse("{}");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(new WorkflowNodeContext(config.RootElement, trigger.RootElement, null!, CancellationToken.None)));
    }
}
