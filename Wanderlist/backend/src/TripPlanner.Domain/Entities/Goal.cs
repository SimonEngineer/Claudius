using TripPlanner.Domain.Enums;

namespace TripPlanner.Domain.Entities;

/// <summary>
/// A generic, user-defined ambition (e.g. "Visit all continents", "MJ statues",
/// "USA roadtrip"). Items' shape is driven entirely by FieldDefinitions so new
/// goal "types" never require backend changes.
/// </summary>
public class Goal : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public GoalKind Kind { get; set; } = GoalKind.Checklist;

    /// <summary>Only set when Kind == TripLink.</summary>
    public Guid? LinkedTripId { get; set; }
    public Trip? LinkedTrip { get; set; }

    public ICollection<GoalFieldDefinition> FieldDefinitions { get; set; } = new List<GoalFieldDefinition>();
    public ICollection<GoalItem> Items { get; set; } = new List<GoalItem>();
}

public class GoalFieldDefinition : EntityBase
{
    public Guid GoalId { get; set; }
    public Goal Goal { get; set; } = null!;
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public GoalFieldType FieldType { get; set; } = GoalFieldType.Text;
    public int SortOrder { get; set; }
}

public class GoalItem : EntityBase
{
    public Guid GoalId { get; set; }
    public Goal Goal { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ICollection<GoalItemFieldValue> FieldValues { get; set; } = new List<GoalItemFieldValue>();
    public ICollection<GoalItemNote> Notes { get; set; } = new List<GoalItemNote>();
    public ICollection<GoalItemLink> Links { get; set; } = new List<GoalItemLink>();
}

public class GoalItemFieldValue : EntityBase
{
    public Guid GoalItemId { get; set; }
    public GoalItem GoalItem { get; set; } = null!;
    public Guid GoalFieldDefinitionId { get; set; }
    public GoalFieldDefinition GoalFieldDefinition { get; set; } = null!;
    public string? Value { get; set; }
}

public class GoalItemNote : EntityBase
{
    public Guid GoalItemId { get; set; }
    public GoalItem GoalItem { get; set; } = null!;
    public string Text { get; set; } = string.Empty;
}

public class GoalItemLink : EntityBase
{
    public Guid GoalItemId { get; set; }
    public GoalItem GoalItem { get; set; } = null!;
    public string Url { get; set; } = string.Empty;
    public string? Label { get; set; }
}
