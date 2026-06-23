namespace Orchestrator.Api.Dtos;

public record CreateProjectRequest(
    string Name,
    string RepoPath,
    string? GitRemote,
    string WorkerModel,
    string SupervisorModel,
    int MaxWorkerConcurrency = 1,
    int Priority = 0);

public record CreateGoalRequest(string Description, string? Title = null);

public record ResolveApprovalRequest(string? Answer, string ResolvedBy = "user");
