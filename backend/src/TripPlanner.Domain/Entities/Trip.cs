using TripPlanner.Domain.Enums;

namespace TripPlanner.Domain.Entities;

public class Trip : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public TripStatus Status { get; set; } = TripStatus.Planning;

    public ICollection<TripStop> Stops { get; set; } = new List<TripStop>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<TimelineEntry> Timeline { get; set; } = new List<TimelineEntry>();
    public ICollection<PackingItem> PackingItems { get; set; } = new List<PackingItem>();
}

public class TripStop : EntityBase
{
    public Guid TripId { get; set; }
    public Trip Trip { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
    public DateOnly? ArriveDate { get; set; }
    public DateOnly? DepartDate { get; set; }
    public int SortOrder { get; set; }
    public bool IsStart { get; set; }
    public bool IsEnd { get; set; }
    public string? Notes { get; set; }
    /// <summary>The wishlist location this stop was created from, if any.</summary>
    public Guid? SourceLocationId { get; set; }
}

public class Booking : EntityBase
{
    public Guid TripId { get; set; }
    public Trip Trip { get; set; } = null!;
    public BookingType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ConfirmationNumber { get; set; }
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    /// <summary>Type-specific structured details (airline/flight no, hotel address, car class, etc.) as JSON.</summary>
    public string? DetailsJson { get; set; }
    public decimal? Cost { get; set; }
}

public class PackingItem : EntityBase
{
    public Guid TripId { get; set; }
    public Trip Trip { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public bool IsPacked { get; set; }
}

public class TimelineEntry : EntityBase
{
    public Guid TripId { get; set; }
    public Trip Trip { get; set; } = null!;
    public TimelineEntryType Type { get; set; }
    public string? Content { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public Guid? NearestStopId { get; set; }
}
