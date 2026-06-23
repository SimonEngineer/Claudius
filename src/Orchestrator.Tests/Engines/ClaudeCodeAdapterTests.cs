using Microsoft.Extensions.Logging.Abstractions;
using Orchestrator.Domain;
using Orchestrator.Domain.Engines;
using Orchestrator.Infrastructure.Engines;
using Xunit;

namespace Orchestrator.Tests.Engines;

public class ClaudeCodeAdapterTests
{
    [Fact]
    public async Task PlanMode_ParsesPlanAndAcceptanceCriteriaFromFinalResult()
    {
        var lines = new[]
        {
            """{"type":"assistant","message":{"content":[{"type":"text","text":"Thinking about the plan..."}]}}""",
            """{"type":"result","subtype":"success","result":"Here is the plan.\n```json\n{\"plan\": [\"step 1\", \"step 2\"], \"acceptanceCriteria\": [\"criterion 1\"]}\n```","usage":{"input_tokens":120,"output_tokens":80},"total_cost_usd":0.012}"""
        };

        var runner = new FakeProcessRunner(lines);
        var adapter = new ClaudeCodeAdapter(runner, NullLogger<ClaudeCodeAdapter>.Instance);

        var context = new RunContext(
            TaskId: Guid.NewGuid(),
            RunId: Guid.NewGuid(),
            Mode: EngineMode.Plan,
            WorkingDirectory: "/tmp",
            Model: "claude-sonnet-4-6",
            Instruction: "Add OAuth login",
            PlanJson: null,
            AcceptanceCriteriaJson: null,
            Timeout: TimeSpan.FromSeconds(5));

        var events = new List<EngineEvent>();
        var result = await adapter.RunAsync(context, e => { events.Add(e); return Task.CompletedTask; }, CancellationToken.None);

        Assert.Equal(EngineOutcome.Succeeded, result.Outcome);
        Assert.NotNull(result.PlanJson);
        Assert.Contains("step 1", result.PlanJson);
        Assert.Contains("criterion 1", result.PlanJson);
        Assert.Equal(120, result.TokensIn);
        Assert.Equal(80, result.TokensOut);
        Assert.Equal(0.012m, result.CostUsd);
        Assert.Contains(events, e => e.Type == EventType.Token);
        Assert.Equal("claude", runner.LastSpec!.FileName);
        Assert.Contains("--permission-mode", runner.LastSpec.Arguments);
    }

    [Fact]
    public async Task VerifyMode_FailVerdictProducesNeedsInputWithFollowUp()
    {
        var lines = new[]
        {
            """{"type":"result","subtype":"success","result":"```json\n{\"verdict\": \"fail\", \"notes\": \"tests fail\", \"followUpInstruction\": \"fix the failing test\"}\n```"}"""
        };

        var adapter = new ClaudeCodeAdapter(new FakeProcessRunner(lines), NullLogger<ClaudeCodeAdapter>.Instance);

        var context = new RunContext(
            Guid.NewGuid(), Guid.NewGuid(), EngineMode.Verify, "/tmp", "claude-sonnet-4-6",
            "Add OAuth login", PlanJson: "[\"step 1\"]", AcceptanceCriteriaJson: "[\"criterion 1\"]",
            Timeout: TimeSpan.FromSeconds(5));

        var result = await adapter.RunAsync(context, _ => Task.CompletedTask, CancellationToken.None);

        Assert.Equal(EngineOutcome.NeedsInput, result.Outcome);
        Assert.NotNull(result.PlanJson);
        Assert.Contains("fix the failing test", result.PlanJson);
    }

    [Fact]
    public async Task VerifyMode_PassVerdictSucceeds()
    {
        var lines = new[]
        {
            """{"type":"result","subtype":"success","result":"```json\n{\"verdict\": \"pass\", \"notes\": \"looks good\"}\n```"}"""
        };

        var adapter = new ClaudeCodeAdapter(new FakeProcessRunner(lines), NullLogger<ClaudeCodeAdapter>.Instance);

        var context = new RunContext(
            Guid.NewGuid(), Guid.NewGuid(), EngineMode.Verify, "/tmp", "claude-sonnet-4-6",
            "Add OAuth login", PlanJson: "[\"step 1\"]", AcceptanceCriteriaJson: "[\"criterion 1\"]",
            Timeout: TimeSpan.FromSeconds(5));

        var result = await adapter.RunAsync(context, _ => Task.CompletedTask, CancellationToken.None);

        Assert.Equal(EngineOutcome.Succeeded, result.Outcome);
    }
}
