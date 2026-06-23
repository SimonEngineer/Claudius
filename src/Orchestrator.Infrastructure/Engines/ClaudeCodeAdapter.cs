using System.Text.Json;
using Microsoft.Extensions.Logging;
using Orchestrator.Domain;
using Orchestrator.Domain.Engines;

namespace Orchestrator.Infrastructure.Engines;

/// <summary>
/// Supervisor-lane adapter. Drives Claude Code headlessly:
///   claude -p "&lt;prompt&gt;" --output-format stream-json --permission-mode plan
/// Used for Plan (goal/task -> implementation plan + acceptance criteria) and Verify
/// (diff/test output -> pass/fail + follow-up instruction) runs. Always backed by a
/// real Anthropic model (configured via the LLM gateway / ANTHROPIC_BASE_URL), never
/// the local model -- this lane is "the management layer".
/// </summary>
public class ClaudeCodeAdapter(IProcessRunner processRunner, ILogger<ClaudeCodeAdapter> logger) : IEngineAdapter
{
    public EngineType EngineType => EngineType.ClaudeCode;

    public async Task<EngineResult> RunAsync(
        RunContext context,
        Func<EngineEvent, Task> onEvent,
        CancellationToken cancellationToken)
    {
        var prompt = BuildPrompt(context);
        var permissionMode = context.Mode == EngineMode.Plan ? "plan" : "default";

        var spec = new ProcessSpec(
            FileName: "claude",
            Arguments: new[]
            {
                "-p", prompt,
                "--output-format", "stream-json",
                "--permission-mode", permissionMode,
                "--model", context.Model
            },
            WorkingDirectory: context.WorkingDirectory);

        var finalText = string.Empty;
        var outcome = EngineOutcome.Failed;
        int? tokensIn = null, tokensOut = null;
        decimal? costUsd = null;

        await onEvent(EngineEvent.StatusChange("started", context.Mode.ToString()));

        try
        {
            var result = await processRunner.RunAsync(
                spec,
                onStdoutLine: async line =>
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        return;
                    }

                    JsonDocument doc;
                    try
                    {
                        doc = JsonDocument.Parse(line);
                    }
                    catch (JsonException)
                    {
                        // Claude Code occasionally emits non-JSON diagnostic lines; surface as a log event.
                        await onEvent(EngineEvent.Log(line));
                        return;
                    }

                    using (doc)
                    {
                        var root = doc.RootElement;
                        var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;

                        switch (type)
                        {
                            case "assistant":
                                if (root.TryGetProperty("message", out var msg) &&
                                    msg.TryGetProperty("content", out var content) &&
                                    content.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var block in content.EnumerateArray())
                                    {
                                        var blockType = block.TryGetProperty("type", out var bt) ? bt.GetString() : null;
                                        if (blockType == "text" && block.TryGetProperty("text", out var textEl))
                                        {
                                            var text = textEl.GetString() ?? "";
                                            finalText += text;
                                            await onEvent(EngineEvent.Token(text));
                                        }
                                        else if (blockType == "tool_use")
                                        {
                                            var toolName = block.TryGetProperty("name", out var n) ? n.GetString() ?? "unknown" : "unknown";
                                            await onEvent(EngineEvent.ToolCall(toolName, block.ToString()));
                                        }
                                    }
                                }
                                break;

                            case "result":
                                if (root.TryGetProperty("result", out var resultText))
                                {
                                    finalText = resultText.GetString() ?? finalText;
                                }
                                if (root.TryGetProperty("usage", out var usage))
                                {
                                    if (usage.TryGetProperty("input_tokens", out var inTok)) tokensIn = inTok.GetInt32();
                                    if (usage.TryGetProperty("output_tokens", out var outTok)) tokensOut = outTok.GetInt32();
                                }
                                if (root.TryGetProperty("total_cost_usd", out var cost))
                                {
                                    costUsd = cost.GetDecimal();
                                }
                                var subtype = root.TryGetProperty("subtype", out var st) ? st.GetString() : null;
                                outcome = subtype == "success" ? EngineOutcome.Succeeded : EngineOutcome.Failed;
                                break;

                            default:
                                await onEvent(EngineEvent.Log(line));
                                break;
                        }
                    }
                },
                onStderrLine: async line => await onEvent(EngineEvent.Log($"[stderr] {line}")),
                timeout: context.Timeout,
                cancellationToken: cancellationToken);

            if (outcome == EngineOutcome.Failed && result.ExitCode == 0 && !string.IsNullOrWhiteSpace(finalText))
            {
                // Some stream-json sessions never emit an explicit "result" line; treat a clean
                // exit with output as success.
                outcome = EngineOutcome.Succeeded;
            }
        }
        catch (TimeoutException ex)
        {
            logger.LogWarning(ex, "Claude Code run {RunId} timed out", context.RunId);
            await onEvent(EngineEvent.StatusChange("timeout", ex.Message));
            return new EngineResult(EngineOutcome.Failed, $"Timed out after {context.Timeout}.");
        }

        var jsonBlock = StructuredOutputParser.ExtractJsonBlock(finalText);

        await onEvent(EngineEvent.StatusChange(outcome.ToString(), finalText));

        return context.Mode switch
        {
            EngineMode.Plan => new EngineResult(
                outcome,
                finalText,
                PlanJson: jsonBlock,
                TokensIn: tokensIn, TokensOut: tokensOut, CostUsd: costUsd),

            EngineMode.Verify => BuildVerifyResult(outcome, finalText, jsonBlock, tokensIn, tokensOut, costUsd),

            _ => new EngineResult(outcome, finalText, TokensIn: tokensIn, TokensOut: tokensOut, CostUsd: costUsd)
        };
    }

    private static EngineResult BuildVerifyResult(
        EngineOutcome outcome, string finalText, string? jsonBlock, int? tokensIn, int? tokensOut, decimal? costUsd)
    {
        if (jsonBlock is null)
        {
            return new EngineResult(outcome, finalText, TokensIn: tokensIn, TokensOut: tokensOut, CostUsd: costUsd);
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonBlock);
            var verdict = doc.RootElement.TryGetProperty("verdict", out var v) ? v.GetString() : null;
            var followUp = doc.RootElement.TryGetProperty("followUpInstruction", out var f) ? f.GetString() : null;

            if (verdict == "pass")
            {
                string? skillName = null, skillContent = null;
                if (doc.RootElement.TryGetProperty("skill", out var skill) && skill.ValueKind == JsonValueKind.Object)
                {
                    skillName = skill.TryGetProperty("name", out var sn) ? sn.GetString() : null;
                    skillContent = skill.TryGetProperty("content", out var sc) ? sc.GetString() : null;
                }

                return new EngineResult(
                    EngineOutcome.Succeeded, finalText,
                    TokensIn: tokensIn, TokensOut: tokensOut, CostUsd: costUsd,
                    SkillName: skillName, SkillContent: skillContent);
            }

            // verdict == "fail" (or unrecognized): surface as needing a follow-up fix, carried
            // in PlanJson so the scheduler can attach it to the requeued task as new instruction.
            return new EngineResult(
                EngineOutcome.NeedsInput,
                finalText,
                PlanJson: followUp is not null ? JsonSerializer.Serialize(new { instruction = followUp }) : null,
                TokensIn: tokensIn, TokensOut: tokensOut, CostUsd: costUsd);
        }
        catch (JsonException)
        {
            return new EngineResult(outcome, finalText, TokensIn: tokensIn, TokensOut: tokensOut, CostUsd: costUsd);
        }
    }

    private static string BuildPrompt(RunContext context) => context.Mode switch
    {
        EngineMode.Plan =>
            $$"""
            You are the supervisor for an autonomous coding task. Break the following task into a
            concrete implementation plan for a cheaper local model to execute, and define acceptance
            criteria that a later verification pass can check objectively. If the task is genuinely
            simple, a single-step plan is fine; only split into multiple steps when they are
            independently executable and verifiable.

            Task: {{context.Instruction}}
            {{ExistingSkillsSection(context)}}
            Respond with your reasoning, then end your message with a fenced json block of the form:
            ```json
            {"plan": ["step 1", "step 2"], "acceptanceCriteria": ["criterion 1", "criterion 2"]}
            ```
            """,

        EngineMode.Verify =>
            $$$"""
            You are the supervisor reviewing work done by a local model against acceptance criteria.

            Original task: {{{context.Instruction}}}
            Plan that was given to the worker: {{{context.PlanJson}}}
            Acceptance criteria: {{{context.AcceptanceCriteriaJson}}}

            Inspect the current state of the repository at {{{context.WorkingDirectory}}} (read files,
            run tests/build as appropriate) and decide whether the work satisfies the acceptance
            criteria.
            {{{ExistingSkillsSection(context)}}}
            End your message with a fenced json block of the form:
            ```json
            {"verdict": "pass", "notes": "...", "skill": {"name": "short-skill-name", "content": "reusable guidance for future similar tasks"}}
            ```
            (the "skill" field is optional -- only include it when you learned something reusable)
            or, if it fails:
            ```json
            {"verdict": "fail", "notes": "...", "followUpInstruction": "concrete instruction for the worker to fix this"}
            ```
            """,

        _ => context.Instruction
    };

    private static string ExistingSkillsSection(RunContext context) =>
        context.ExistingSkillsJson is null
            ? ""
            : $"\nReusable skills from past tasks on this project (apply them if relevant): {context.ExistingSkillsJson}\n";
}
