namespace Weaver.Api.Dtos;

public record DashboardStatsDto(
    int ScrapesSucceededToday,
    int ScrapesFailedToday,
    int WorkflowRunsSucceededToday,
    int WorkflowRunsFailedToday,
    List<AuditLogEntryDto> RecentActivity);
