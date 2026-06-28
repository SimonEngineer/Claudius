using TripPlanner.Domain.Enums;

namespace TripPlanner.Domain.Entities;

public class WishlistLocation : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Country { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public WishlistStatus Status { get; set; } = WishlistStatus.Idea;

    public ICollection<WishlistNote> Notes { get; set; } = new List<WishlistNote>();
    public ICollection<WishlistLink> Links { get; set; } = new List<WishlistLink>();
    public WishlistPlan? Plan { get; set; }
    public Whiteboard? Whiteboard { get; set; }
}

public class WishlistNote : EntityBase
{
    public Guid WishlistLocationId { get; set; }
    public WishlistLocation WishlistLocation { get; set; } = null!;
    public string Text { get; set; } = string.Empty;
}

public class WishlistLink : EntityBase
{
    public Guid WishlistLocationId { get; set; }
    public WishlistLocation WishlistLocation { get; set; } = null!;
    public string Url { get; set; } = string.Empty;
    public string? Label { get; set; }
}

public class WishlistPlan : EntityBase
{
    public Guid WishlistLocationId { get; set; }
    public WishlistLocation WishlistLocation { get; set; } = null!;
    public string? TransportDetails { get; set; }
    public string? AccommodationDetails { get; set; }
    public DateOnly? DateRangeStart { get; set; }
    public DateOnly? DateRangeEnd { get; set; }
    public string? FreeformText { get; set; }
}

public class Whiteboard : EntityBase
{
    public Guid WishlistLocationId { get; set; }
    public WishlistLocation WishlistLocation { get; set; } = null!;
    /// <summary>Serialized canvas elements (shapes, sticky notes, images, lines) as JSON.</summary>
    public string ContentJson { get; set; } = "{\"elements\":[]}";
}
