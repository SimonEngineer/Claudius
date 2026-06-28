namespace Orchestrator.Api.Dtos;

public record ProjectCostDto(
    Guid ProjectId,
    string ProjectName,
    decimal CostUsd,
    int TokensIn,
    int TokensOut,
    int RunCount);

public record DailyCostDto(DateOnly Date, decimal CostUsd);

/// <summary>
/// Aggregate spend across every project, backing the dashboard's cost page -- the per-run
/// CostUsd/TokensIn/TokensOut that Run already records was never rolled up anywhere.
/// </summary>
public record CostSummaryDto(
    decimal TotalCostUsd,
    int TotalTokensIn,
    int TotalTokensOut,
    List<ProjectCostDto> ByProject,
    List<DailyCostDto> Last30Days);
