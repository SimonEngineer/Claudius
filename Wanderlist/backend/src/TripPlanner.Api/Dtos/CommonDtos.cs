using TripPlanner.Domain.Enums;

namespace TripPlanner.Api.Dtos;

public record TagDto(Guid Id, string Name, string Color);
public record TagCreateDto(string Name, string Color);
public record TagUpdateDto(string Name, string Color);
public record TagUsageDto(Guid Id, string Name, string Color, int UsageCount);

public record AssignTagDto(EntityType EntityType, Guid EntityId);

public record MediaItemDto(Guid Id, EntityType EntityType, Guid EntityId, MediaKind Kind, string Url, string? Caption, DateTime? CapturedAt, double? Lat, double? Lng, DateTime CreatedAt);
public record MediaItemCreateDto(EntityType EntityType, Guid EntityId, MediaKind Kind, string Url, string? Caption, DateTime? CapturedAt, double? Lat, double? Lng);
