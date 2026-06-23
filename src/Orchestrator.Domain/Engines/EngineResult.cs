namespace Orchestrator.Domain.Engines;

public enum EngineOutcome
{
    Succeeded,
    NeedsInput,
    Failed,
    Cancelled
}

public record EngineResult(
    EngineOutcome Outcome,
    string Summary,
    string? PlanJson = null,
    string? AcceptanceCriteriaJson = null,
    string? ApprovalQuestion = null,
    string[]? ApprovalOptions = null,
    int? TokensIn = null,
    int? TokensOut = null,
    decimal? CostUsd = null,
    string? SkillName = null,
    string? SkillContent = null
);
