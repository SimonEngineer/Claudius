using Weaver.Domain;

namespace Weaver.Api.Dtos;

public record AuditLogEntryDto(Guid Id, AuditAction Action, string ResourceType, Guid ResourceId, string ResourceName, DateTimeOffset CreatedAt)
{
    public static AuditLogEntryDto FromEntity(AuditLogEntry e) => new(e.Id, e.Action, e.ResourceType, e.ResourceId, e.ResourceName, e.CreatedAt);
}
