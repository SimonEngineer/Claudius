namespace TripPlanner.Api.Dtos;

public record WishlistLocationDto(
    Guid Id, string Name, string? Description, string? Country, double? Lat, double? Lng,
    DateTime CreatedAt, List<TagDto> Tags);

public record WishlistLocationCreateDto(string Name, string? Description, string? Country, double? Lat, double? Lng);
public record WishlistLocationUpdateDto(string Name, string? Description, string? Country, double? Lat, double? Lng);

public record WishlistNoteDto(Guid Id, string Text, DateTime CreatedAt);
public record WishlistNoteCreateDto(string Text);

public record WishlistLinkDto(Guid Id, string Url, string? Label);
public record WishlistLinkCreateDto(string Url, string? Label);

public record WishlistPlanDto(string? TransportDetails, string? AccommodationDetails, DateOnly? DateRangeStart, DateOnly? DateRangeEnd, string? FreeformText);

public record WhiteboardDto(string ContentJson);
