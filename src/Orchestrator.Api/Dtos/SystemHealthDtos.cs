namespace Orchestrator.Api.Dtos;

public record LaneHealthDto(
    int QueueDepth,
    int InFlight,
    int Capacity);

/// <summary>
/// Operational snapshot of the scheduler itself, distinct from CostSummaryDto (spend) and
/// OverviewDto (task-state counts) -- this answers "is the pipeline actually keeping up" by
/// surfacing per-lane backlog/utilization and leases that are overdue for the crash-recovery
/// sweep (TaskClaimingService.RequeueStaleLeasesAsync) to pick up.
/// </summary>
public record SystemHealthDto(
    bool DatabaseHealthy,
    LaneHealthDto Supervisor,
    LaneHealthDto Worker,
    int StaleLeaseCount,
    int DeadLetterCount,
    int PausedProjectCount);
