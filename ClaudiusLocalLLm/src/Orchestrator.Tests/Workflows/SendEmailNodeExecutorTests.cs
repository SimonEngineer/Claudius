using System.Text.Json;
using NSubstitute;
using Orchestrator.Infrastructure.Workflows;
using Orchestrator.Infrastructure.Workflows.Executors;
using Xunit;

namespace Orchestrator.Tests.Workflows;

public class SendEmailNodeExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_SendsEmail_WithTemplatedSubjectAndBody()
    {
        var sender = Substitute.For<IEmailSender>();
        var executor = new SendEmailNodeExecutor(sender);

        using var config = JsonDocument.Parse("""{"to":"me@example.com","subject":"Task {{trigger.title}} done","body":"see {{trigger.title}}"}""");
        using var trigger = JsonDocument.Parse("""{"title":"build the thing"}""");

        await executor.ExecuteAsync(new WorkflowNodeContext(config.RootElement, trigger.RootElement, null!, CancellationToken.None));

        await sender.Received(1).SendAsync("me@example.com", "Task build the thing done", "see build the thing", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Throws_WhenToIsMissing()
    {
        var executor = new SendEmailNodeExecutor(Substitute.For<IEmailSender>());
        using var config = JsonDocument.Parse("""{"subject":"x"}""");
        using var trigger = JsonDocument.Parse("{}");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(new WorkflowNodeContext(config.RootElement, trigger.RootElement, null!, CancellationToken.None)));
    }
}
