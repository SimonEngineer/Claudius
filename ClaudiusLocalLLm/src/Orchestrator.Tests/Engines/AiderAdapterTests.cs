using Microsoft.Extensions.Logging.Abstractions;
using Orchestrator.Domain.Engines;
using Orchestrator.Infrastructure.Engines;
using Xunit;

namespace Orchestrator.Tests.Engines;

public class AiderAdapterTests
{
    private static RunContext MakeContext(string instruction = "Implement step 1") => new(
        Guid.NewGuid(), Guid.NewGuid(), EngineMode.Implement, "/tmp", "ollama/qwen2.5-coder:32b",
        instruction, PlanJson: null, AcceptanceCriteriaJson: null, Timeout: TimeSpan.FromSeconds(5));

    [Fact]
    public async Task SuccessfulEdit_ReportsChangedFilesAndSucceeds()
    {
        var lines = new[]
        {
            "Applied edit to src/auth.py",
            "Commit abc1234 Add OAuth login flow"
        };

        var adapter = new AiderAdapter(new FakeProcessRunner(lines), NullLogger<AiderAdapter>.Instance);
        var result = await adapter.RunAsync(MakeContext(), _ => Task.CompletedTask, CancellationToken.None);

        Assert.Equal(EngineOutcome.Succeeded, result.Outcome);
        Assert.Contains("src/auth.py", result.Summary);
    }

    [Fact]
    public async Task NeedsInputMarker_ProducesNeedsInputOutcomeWithQuestion()
    {
        var lines = new[]
        {
            "Looking at the repo...",
            "AIDER_NEEDS_INPUT:Which OAuth provider should I integrate first?"
        };

        var adapter = new AiderAdapter(new FakeProcessRunner(lines), NullLogger<AiderAdapter>.Instance);
        var result = await adapter.RunAsync(MakeContext(), _ => Task.CompletedTask, CancellationToken.None);

        Assert.Equal(EngineOutcome.NeedsInput, result.Outcome);
        Assert.Equal("Which OAuth provider should I integrate first?", result.ApprovalQuestion);
    }

    [Fact]
    public async Task NoChangesAndNoCommit_Fails()
    {
        var lines = new[] { "Nothing to do here." };

        var adapter = new AiderAdapter(new FakeProcessRunner(lines), NullLogger<AiderAdapter>.Instance);
        var result = await adapter.RunAsync(MakeContext(), _ => Task.CompletedTask, CancellationToken.None);

        Assert.Equal(EngineOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task NonZeroExitCode_Fails()
    {
        var adapter = new AiderAdapter(new FakeProcessRunner(Array.Empty<string>(), exitCode: 1), NullLogger<AiderAdapter>.Instance);
        var result = await adapter.RunAsync(MakeContext(), _ => Task.CompletedTask, CancellationToken.None);

        Assert.Equal(EngineOutcome.Failed, result.Outcome);
    }
}
