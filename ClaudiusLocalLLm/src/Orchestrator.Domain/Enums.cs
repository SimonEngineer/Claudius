namespace Orchestrator.Domain;

public enum Lane
{
    Supervisor,
    Worker
}

public enum EngineType
{
    ClaudeCode,
    Aider
}

public enum TaskState
{
    Queued,
    Planning,
    Blocked,
    ReadyForWork,
    InProgress,
    Verifying,
    NeedsFix,
    AwaitingInput,
    Decomposed,
    Done,
    Failed,
    DeadLetter
}

public enum RunStatus
{
    Running,
    Succeeded,
    Failed,
    Cancelled
}

public enum EventType
{
    Token,
    ToolCall,
    FileDiff,
    Log,
    StatusChange
}

public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected
}

public enum ApprovalKind
{
    WorkerInput,
    PlanReview
}

public enum GoalStatus
{
    Active,
    Completed,
    Cancelled
}
