using Orchestrator.Domain;

namespace Orchestrator.Api.Dtos;

public record TaskSummaryDto(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    string Title,
    TaskState State,
    Lane Lane,
    int RetryCount,
    DateTimeOffset UpdatedAt);

public record ApprovalSummaryDto(
    Guid Id,
    Guid TaskId,
    Guid ProjectId,
    string ProjectName,
    string TaskTitle,
    string Question,
    DateTimeOffset CreatedAt);

/// <summary>
/// Single-call aggregate for the dashboard's overview page -- avoids the frontend having to fan
/// out a tasks-list request per project just to answer "what's running right now."
/// </summary>
public record OverviewDto(
    int ProjectCount,
    Dictionary<string, int> TaskCountsByState,
    List<TaskSummaryDto> InProgress,
    List<TaskSummaryDto> NeedsAttention,
    List<ApprovalSummaryDto> PendingApprovals);
