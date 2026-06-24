using TripPlanner.Domain.Enums;

namespace TripPlanner.Api.Dtos;

public record TripDto(Guid Id, string Name, string? Description, DateOnly? StartDate, DateOnly? EndDate, TripStatus Status, decimal? Budget, string? BudgetCurrency, List<TagDto> Tags);
public record TripCreateDto(string Name, string? Description, DateOnly? StartDate, DateOnly? EndDate);
public record TripUpdateDto(string Name, string? Description, DateOnly? StartDate, DateOnly? EndDate, TripStatus Status, decimal? Budget, string? BudgetCurrency);

public record TripStopDto(Guid Id, string Name, double Lat, double Lng, DateOnly? ArriveDate, DateOnly? DepartDate, int SortOrder, bool IsStart, bool IsEnd, string? Notes, Guid? SourceLocationId);
public record TripStopCreateDto(string Name, double Lat, double Lng, DateOnly? ArriveDate, DateOnly? DepartDate, int SortOrder, bool IsStart, bool IsEnd, string? Notes, Guid? SourceLocationId);

public record BookingDto(Guid Id, BookingType Type, string Title, string? ConfirmationNumber, DateTime? StartAt, DateTime? EndAt, double? Lat, double? Lng, string? DetailsJson, decimal? Cost);
public record BookingCreateDto(BookingType Type, string Title, string? ConfirmationNumber, DateTime? StartAt, DateTime? EndAt, double? Lat, double? Lng, string? DetailsJson, decimal? Cost);

public record TimelineEntryDto(Guid Id, TimelineEntryType Type, string? Content, double? Lat, double? Lng, DateTime CapturedAt, Guid? NearestStopId);
public record TimelineEntryCreateDto(TimelineEntryType Type, string? Content, double? Lat, double? Lng, DateTime? CapturedAt);

public record PackingItemDto(Guid Id, string Name, bool IsPacked);
public record PackingItemCreateDto(string Name);

public record TripLinkDto(Guid TripId, string TripName, Guid StopId);

public record ExpenseDto(Guid Id, ExpenseCategory Category, decimal Amount, string Currency, DateOnly Date, string? Note, Guid? BookingId);
public record ExpenseCreateDto(ExpenseCategory Category, decimal Amount, string Currency, DateOnly Date, string? Note, Guid? BookingId);
