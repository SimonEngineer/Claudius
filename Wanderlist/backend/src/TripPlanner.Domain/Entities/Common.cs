namespace TripPlanner.Domain.Entities;

public abstract class EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Tag : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#64748b";

    public ICollection<TaggedItem> TaggedItems { get; set; } = new List<TaggedItem>();
}

/// <summary>Polymorphic join so Tag/Media can attach to any entity without per-entity join tables.</summary>
public class TaggedItem
{
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;
    public Enums.EntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
}

public class MediaItem : EntityBase
{
    public Enums.EntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public Enums.MediaKind Kind { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public DateTime? CapturedAt { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
}
