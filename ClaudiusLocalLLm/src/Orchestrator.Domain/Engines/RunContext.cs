namespace Orchestrator.Domain.Engines;

public enum EngineMode
{
    /// <summary>Supervisor: turn a Goal/Task description into a concrete plan + acceptance criteria.</summary>
    Plan,
    /// <summary>Worker: implement the plan's next instruction against the repo.</summary>
    Implement,
    /// <summary>Supervisor: review a diff/test output against acceptance criteria.</summary>
    Verify
}

/// <summary>Everything an engine adapter needs to execute one Run.</summary>
public record RunContext(
    Guid TaskId,
    Guid RunId,
    EngineMode Mode,
    string WorkingDirectory,
    string Model,
    string Instruction,
    string? PlanJson,
    string? AcceptanceCriteriaJson,
    TimeSpan Timeout,
    string? ExistingSkillsJson = null
);
