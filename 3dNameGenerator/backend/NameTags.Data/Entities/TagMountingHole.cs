namespace NameTags.Data.Entities;

public sealed class TagMountingHole
{
    public int Id { get; set; }
    public int TagProjectId { get; set; }
    public TagProject TagProject { get; set; } = null!;
    public float OffsetXMm { get; set; }
    public float OffsetYMm { get; set; }
    public float DiameterMm { get; set; } = 4f;
}
