using TripPlanner.Domain.Enums;

namespace TripPlanner.Domain.Entities;

public class Trip : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public TripStatus Status { get; set; } = TripStatus.Planning;
    public decimal? Budget { get; set; }
    public string? BudgetCurrency { get; set; }
    /// <summary>Opaque slug enabling unauthenticated read-only access to this trip; null until first shared.</summary>
    public string? ShareSlug { get; set; }
    public string? EmergencyInfo { get; set; }

    public ICollection<TripStop> Stops { get; set; } = new List<TripStop>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<TimelineEntry> Timeline { get; set; } = new List<TimelineEntry>();
    public ICollection<PackingItem> PackingItems { get; set; } = new List<PackingItem>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    public ICollection<TripCompanion> Companions { get; set; } = new List<TripCompanion>();
    public ICollection<TravelDocument> Documents { get; set; } = new List<TravelDocument>();
}

public class TripCompanion : EntityBase
{
    public Guid TripId { get; set; }
    public Trip Trip { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
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

public class Expense : EntityBase
{
    public Guid TripId { get; set; }
    public Trip Trip { get; set; } = null!;
    public ExpenseCategory Category { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateOnly Date { get; set; }
    public string? Note { get; set; }
    public Guid? BookingId { get; set; }
    /// <summary>Companion who fronted the cost, if any (null means trip owner paid).</summary>
    public Guid? PaidByCompanionId { get; set; }
    /// <summary>Comma-separated TripCompanion ids this expense is split across; empty/null means not split.</summary>
    public string? SplitCompanionIds { get; set; }
}

public class TravelDocument : EntityBase
{
    public Guid TripId { get; set; }
    public Trip Trip { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public DocumentType DocType { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? Url { get; set; }
    public string? Notes { get; set; }
}
