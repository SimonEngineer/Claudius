using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Orchestrator.Domain;
using Orchestrator.Domain.Engines;

namespace Orchestrator.Infrastructure.Engines;

/// <summary>
/// Worker-lane adapter. Drives Aider non-interactively against a project git worktree:
///   aider --yes --message "&lt;instruction&gt;" --model &lt;model-via-gateway&gt;
/// `--model` is routed through the LLM gateway, which is what actually points this at
/// Ollama/LocalAI. Aider commits its own changes per edit, which doubles as cheap
/// checkpointing -- a retried task simply re-runs from the latest commit.
/// </summary>
public partial class AiderAdapter(IProcessRunner processRunner, ILogger<AiderAdapter> logger) : IEngineAdapter
{
    public EngineType EngineType => EngineType.Aider;

    [GeneratedRegex(@"^Applied edit to (?<file>.+)$")]
    private static partial Regex AppliedEditRegex();

    [GeneratedRegex(@"^Commit (?<hash>[0-9a-f]+) ")]
    private static partial Regex CommitRegex();

    public async Task<EngineResult> RunAsync(
        RunContext context,
        Func<EngineEvent, Task> onEvent,
        CancellationToken cancellationToken)
    {
        var instruction =
            $"""
            {context.Instruction}

            If you are blocked and cannot proceed without more information from the user, do not
            guess: print a single line starting with exactly "AIDER_NEEDS_INPUT:" followed by your
            question, and stop.
            """;

        var spec = new ProcessSpec(
            FileName: "aider",
            Arguments: new[]
            {
                "--yes",
                "--no-pretty",
                "--message", instruction,
                "--model", context.Model
            },
            WorkingDirectory: context.WorkingDirectory);

        var filesChanged = new List<string>();
        string? needsInputQuestion = null;
        var sawCommit = false;

        await onEvent(EngineEvent.StatusChange("started", "implement"));

        ProcessOutcome result;
        try
        {
            result = await processRunner.RunAsync(
                spec,
                onStdoutLine: async line =>
                {
                    await onEvent(EngineEvent.Token(line + "\n"));

                    if (line.StartsWith("AIDER_NEEDS_INPUT:", StringComparison.Ordinal))
                    {
                        needsInputQuestion = line["AIDER_NEEDS_INPUT:".Length..].Trim();
                        return;
                    }

                    var editMatch = AppliedEditRegex().Match(line);
                    if (editMatch.Success)
                    {
                        var file = editMatch.Groups["file"].Value;
                        filesChanged.Add(file);
                        await onEvent(EngineEvent.FileDiff(file, "(see git history for full diff)"));
                        return;
                    }

                    if (CommitRegex().IsMatch(line))
                    {
                        sawCommit = true;
                    }
                },
                onStderrLine: async line => await onEvent(EngineEvent.Log($"[stderr] {line}")),
                timeout: context.Timeout,
                cancellationToken: cancellationToken);
        }
        catch (TimeoutException ex)
        {
            logger.LogWarning(ex, "Aider run {RunId} timed out", context.RunId);
            await onEvent(EngineEvent.StatusChange("timeout", ex.Message));
            return new EngineResult(EngineOutcome.Failed, $"Timed out after {context.Timeout}.");
        }

        var summary = filesChanged.Count > 0
            ? $"Changed {filesChanged.Count} file(s): {string.Join(", ", filesChanged)}"
            : "No files changed.";

        await onEvent(EngineEvent.StatusChange(
            needsInputQuestion is not null ? "needs_input" : result.ExitCode == 0 ? "succeeded" : "failed",
            summary));

        if (needsInputQuestion is not null)
        {
            return new EngineResult(EngineOutcome.NeedsInput, summary, ApprovalQuestion: needsInputQuestion);
        }

        if (result.ExitCode != 0)
        {
            return new EngineResult(EngineOutcome.Failed, $"aider exited with code {result.ExitCode}. {summary}");
        }

        if (!sawCommit && filesChanged.Count == 0)
        {
            return new EngineResult(EngineOutcome.Failed, "aider made no changes and committed nothing.");
        }

        return new EngineResult(EngineOutcome.Succeeded, summary);
    }
}
