using System.Text.Json;
using NSubstitute;
using Orchestrator.Infrastructure.Discord;
using Orchestrator.Infrastructure.Workflows;
using Orchestrator.Infrastructure.Workflows.Executors;
using Xunit;

namespace Orchestrator.Tests.Workflows;

public class SendDiscordMessageNodeExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_SendsNotification_WithTemplatedTitleAndMessage()
    {
        var discord = Substitute.For<IDiscordNotifier>();
        var executor = new SendDiscordMessageNodeExecutor(discord);

        using var config = JsonDocument.Parse("""{"title":"Task {{trigger.title}}","message":"done: {{trigger.title}}"}""");
        using var trigger = JsonDocument.Parse("""{"title":"build the thing"}""");

        await executor.ExecuteAsync(new WorkflowNodeContext(config.RootElement, trigger.RootElement, null!, CancellationToken.None));

        await discord.Received(1).SendNotificationAsync("Task build the thing", "done: build the thing", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_UsesDefaults_WhenTitleAndMessageMissing()
    {
        var discord = Substitute.For<IDiscordNotifier>();
        var executor = new SendDiscordMessageNodeExecutor(discord);

        using var config = JsonDocument.Parse("{}");
        using var trigger = JsonDocument.Parse("{}");

        await executor.ExecuteAsync(new WorkflowNodeContext(config.RootElement, trigger.RootElement, null!, CancellationToken.None));

        await discord.Received(1).SendNotificationAsync("Workflow notification", "", Arg.Any<CancellationToken>());
    }
}
