using TripPlanner.Domain.Enums;

namespace TripPlanner.Api.Dtos;

public record GoalFieldDefinitionDto(Guid Id, string Key, string Label, GoalFieldType FieldType, int SortOrder);
public record GoalFieldDefinitionCreateDto(string Key, string Label, GoalFieldType FieldType, int SortOrder);

public record GoalDto(
    Guid Id, string Name, string? Description, string? Icon, GoalKind Kind, Guid? LinkedTripId,
    List<GoalFieldDefinitionDto> FieldDefinitions, int ItemCount, int CompletedCount, List<TagDto> Tags);

public record GoalCreateDto(string Name, string? Description, string? Icon, GoalKind Kind, Guid? LinkedTripId, List<GoalFieldDefinitionCreateDto> FieldDefinitions);
public record GoalUpdateDto(string Name, string? Description, string? Icon, Guid? LinkedTripId);

public record GoalItemFieldValueDto(Guid GoalFieldDefinitionId, string? Value);
public record GoalItemDto(
    Guid Id, Guid GoalId, string Name, double? Lat, double? Lng, bool IsCompleted, DateTime? CompletedAt,
    List<GoalItemFieldValueDto> FieldValues, List<GoalItemNoteDto> Notes, List<GoalItemLinkDto> Links, List<TagDto> Tags);

public record GoalItemCreateDto(string Name, double? Lat, double? Lng, List<GoalItemFieldValueDto> FieldValues);
public record GoalItemUpdateDto(string Name, double? Lat, double? Lng, List<GoalItemFieldValueDto> FieldValues);

public record GoalItemNoteDto(Guid Id, string Text, DateTime CreatedAt);
public record GoalItemNoteCreateDto(string Text);
public record GoalItemLinkDto(Guid Id, string Url, string? Label);
public record GoalItemLinkCreateDto(string Url, string? Label);
