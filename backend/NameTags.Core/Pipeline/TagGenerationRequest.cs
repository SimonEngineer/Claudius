using NameTags.Core.Outlines;

namespace NameTags.Core.Pipeline;

public sealed record TagGenerationRequest
{
    public required string Text { get; init; }
    public ShapeType ShapeType { get; init; } = ShapeType.RoundedRectangle;
    public ShapeParams ShapeParams { get; init; } = new();

    /// <summary>Absolute path (or system family name) of the font used to extrude the text.</summary>
    public string FontFamilyOrPath { get; init; } = "DejaVuSans-Bold.ttf";

    public float PlateWidthMm { get; init; } = 70f;
    public float PlateHeightMm { get; init; } = 30f;
    public float PlateThicknessMm { get; init; } = 3f;
    public float TextDepthMm { get; init; } = 2f;

    /// <summary>Independent per-side margins between the text's bounding box and the plate's outer edge, used for auto-fit sizing.</summary>
    public float TextMarginLeftMm { get; init; } = 5f;
    public float TextMarginRightMm { get; init; } = 5f;
    public float TextMarginTopMm { get; init; } = 5f;
    public float TextMarginBottomMm { get; init; } = 5f;

    public TextHorizontalAlign TextHorizontalAlign { get; init; } = TextHorizontalAlign.Center;
    public TextVerticalAlign TextVerticalAlign { get; init; } = TextVerticalAlign.Center;

    /// <summary>How far the text's bottom is sunk below the plate's top, so the two solids genuinely overlap (avoids z-fighting at the seam).</summary>
    public float OverlapEpsilonMm { get; init; } = 0.2f;

    /// <summary>Custom SVG outline bytes, required only when ShapeType is CustomSvg.</summary>
    public byte[]? CustomSvgBytes { get; init; }
}
