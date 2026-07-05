namespace Weaver.Api.Dtos;

public record FailedRunDto(string Kind, Guid ResourceId, string ResourceName, string? Error, DateTimeOffset At);

public record DashboardStatsDto(
    int ScrapesSucceededToday,
    int ScrapesFailedToday,
    int WorkflowRunsSucceededToday,
    int WorkflowRunsFailedToday,
    int RunningScrapes,
    int RunningWorkflows,
    List<AuditLogEntryDto> RecentActivity,
    List<FailedRunDto> RecentFailures);

public record DailyRunCountDto(DateOnly Day, int Succeeded, int Failed);

public record RunsPerDayDto(List<DailyRunCountDto> Scrapes, List<DailyRunCountDto> Workflows);

public record WorkerInfoDto(string Name, DateTimeOffset StartedAt, DateTimeOffset LastSeen);
