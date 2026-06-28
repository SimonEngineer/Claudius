namespace NameTags.Core.Outlines;

/// <summary>
/// Shape-specific parameters, kept as one flat record (rather than a type per shape) since
/// most presets need at most one or two extra knobs. Unused fields are ignored by a given provider.
/// </summary>
public sealed record ShapeParams
{
    /// <summary>Corner radius in mm, used by RoundedRectangle. Also doubles as the scalloped-end radius for Plaque.</summary>
    public float CornerRadiusMm { get; init; } = 4f;

    /// <summary>Number of points, used by Star.</summary>
    public int StarPoints { get; init; } = 5;

    /// <summary>Inner-to-outer radius ratio (0-1), used by Star.</summary>
    public float StarInnerRadiusRatio { get; init; } = 0.45f;

    /// <summary>Number of polyline segments used to approximate curved boundaries (circle, oval, heart, rounded corners).</summary>
    public int CurveSegments { get; init; } = 48;
}
