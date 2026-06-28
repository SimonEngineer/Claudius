using System.Numerics;
using NameTags.Core.Extrusion;
using NameTags.Core.Geometry;
using NameTags.Core.Outlines;

namespace NameTags.Core.Pipeline;

/// <summary>
/// Builds a small charm tag with an open hook fused to its top edge, sized to hang from a
/// wine glass stem. The hook is mounted dead-center on the plate's top edge, so the plate
/// hangs with its width axis horizontal under gravity -- which is exactly the axis the text
/// already reads along, satisfying "the name shows horizontally" with no extra rotation logic.
/// </summary>
public static class WineGlassCharmBuilder
{
    public const float PlateWidthMm = 28f;
    public const float PlateHeightMm = 13f;
    public const float PlateThicknessMm = 2f;
    public const float TextDepthMm = 1f;

    public const float HookOuterRadiusMm = 6f;
    public const float HookBandThicknessMm = 1.8f;
    private const float HookOverlapMm = 2f;

    public static Mesh3D Build(
        ModelGenerationService modelService,
        string text,
        string fontFamilyOrPath,
        float plateWidthMm = PlateWidthMm,
        float plateHeightMm = PlateHeightMm,
        float hookOuterRadiusMm = HookOuterRadiusMm,
        float hookBandThicknessMm = HookBandThicknessMm)
    {
        var plateRequest = new TagGenerationRequest
        {
            Text = text,
            FontFamilyOrPath = fontFamilyOrPath,
            ShapeType = NameTags.Core.Outlines.ShapeType.RoundedRectangle,
            ShapeParams = new ShapeParams { CornerRadiusMm = 3f },
            PlateWidthMm = plateWidthMm,
            PlateHeightMm = plateHeightMm,
            PlateThicknessMm = PlateThicknessMm,
            TextDepthMm = TextDepthMm,
            TextMarginLeftMm = 2.5f,
            TextMarginRightMm = 2.5f,
            TextMarginTopMm = 4f,
            TextMarginBottomMm = 2.5f,
        };
        var plateMesh = modelService.GenerateTagMesh(plateRequest);

        var hookOutline = HookOutlineProvider.BuildOpenHookOutline(hookOuterRadiusMm, hookBandThicknessMm);
        var hookMesh = MeshExtruder.Extrude(hookOutline, zBottom: 0f, zTop: PlateThicknessMm);

        // Anchor the hook's open end into the plate's top edge, overlapping by a couple mm so
        // slicers see one fused solid rather than two merely-touching ones.
        float anchorY = plateHeightMm / 2f - HookOverlapMm;
        var positionedHookMesh = hookMesh.Translate(new Vector3(0f, anchorY, 0f));

        return Mesh3D.Combine(plateMesh, positionedHookMesh);
    }
}
