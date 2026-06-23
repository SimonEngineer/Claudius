using NameTags.Core.Outlines;

namespace NameTags.Data.Entities;

/// <summary>
/// Persisted generation parameters for a tag design. The mesh itself is never stored --
/// every preview/download regenerates fresh from this row via ModelGenerationService,
/// so editing a project's params always reflects the current state on next request.
/// </summary>
public sealed class TagProject
{
    public int Id { get; set; }
    public string Name { get; set; } = "Untitled project";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ShapeType ShapeType { get; set; } = ShapeType.RoundedRectangle;
    public float CornerRadiusMm { get; set; } = 4f;
    public int StarPoints { get; set; } = 5;
    public float StarInnerRadiusRatio { get; set; } = 0.45f;
    public int CurveSegments { get; set; } = 48;
    public byte[]? CustomSvgBytes { get; set; }

    public string FontFamilyOrPath { get; set; } = "DejaVuSans-Bold.ttf";
    public float PlateWidthMm { get; set; } = 70f;
    public float PlateHeightMm { get; set; } = 30f;
    public float PlateThicknessMm { get; set; } = 3f;
    public float TextDepthMm { get; set; } = 2f;

    public List<TagName> Names { get; set; } = new();
}
