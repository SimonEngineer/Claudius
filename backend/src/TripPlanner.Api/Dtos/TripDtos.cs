using TripPlanner.Domain.Enums;

namespace TripPlanner.Api.Dtos;

public record TripDto(Guid Id, string Name, string? Description, DateOnly? StartDate, DateOnly? EndDate, TripStatus Status, List<TagDto> Tags);
public record TripCreateDto(string Name, string? Description, DateOnly? StartDate, DateOnly? EndDate);
public record TripUpdateDto(string Name, string? Description, DateOnly? StartDate, DateOnly? EndDate, TripStatus Status);

public record TripStopDto(Guid Id, string Name, double Lat, double Lng, DateOnly? ArriveDate, DateOnly? DepartDate, int SortOrder, bool IsStart, bool IsEnd, string? Notes);
public record TripStopCreateDto(string Name, double Lat, double Lng, DateOnly? ArriveDate, DateOnly? DepartDate, int SortOrder, bool IsStart, bool IsEnd, string? Notes);

public record BookingDto(Guid Id, BookingType Type, string Title, string? ConfirmationNumber, DateTime? StartAt, DateTime? EndAt, double? Lat, double? Lng, string? DetailsJson);
public record BookingCreateDto(BookingType Type, string Title, string? ConfirmationNumber, DateTime? StartAt, DateTime? EndAt, double? Lat, double? Lng, string? DetailsJson);

public record TimelineEntryDto(Guid Id, TimelineEntryType Type, string? Content, double? Lat, double? Lng, DateTime CapturedAt, Guid? NearestStopId);
public record TimelineEntryCreateDto(TimelineEntryType Type, string? Content, double? Lat, double? Lng, DateTime? CapturedAt);
