using System.Numerics;
using NameTags.Core.Extrusion;
using NameTags.Core.Geometry;
using NameTags.Core.Outlines;

namespace NameTags.Core.Pipeline;

/// <summary>
/// Builds a chest tag with a printed alligator-clip spring fused to its back edge. The
/// clip is flat (extruded along the same Z axis as the plate, not rotated), so the whole
/// part still lies flat on the print bed; "back" only matters once it's worn, where the
/// clip's arms stack vertically and the fabric slides into the gap from the side.
/// </summary>
public static class ClothesClipBuilder
{
    public const float PlateWidthMm = 55f;
    public const float PlateHeightMm = 22f;
    public const float PlateThicknessMm = 3f;
    public const float TextDepthMm = 1.5f;

    private const float ClipArmLengthMm = 20f;
    private const float ClipGapMm = 2.4f;
    private const float ClipArmThicknessMm = 1.2f;
    private const float ClipOverlapMm = 3f;

    public static Mesh3D Build(ModelGenerationService modelService, string text, string fontFamilyOrPath)
    {
        var plateRequest = new TagGenerationRequest
        {
            Text = text,
            FontFamilyOrPath = fontFamilyOrPath,
            ShapeType = NameTags.Core.Outlines.ShapeType.RoundedRectangle,
            ShapeParams = new ShapeParams { CornerRadiusMm = 3f },
            PlateWidthMm = PlateWidthMm,
            PlateHeightMm = PlateHeightMm,
            PlateThicknessMm = PlateThicknessMm,
            TextDepthMm = TextDepthMm,
            TextMarginLeftMm = 4f,
            TextMarginRightMm = 4f,
            TextMarginTopMm = 4f,
            TextMarginBottomMm = 4f,
        };
        var plateMesh = modelService.GenerateTagMesh(plateRequest);

        // ClipOutlineProvider builds the clip with its arm axis (u) along local X: the open
        // (anchor) end at u=0 and the bend at u=armLengthMm. Rotate -90deg so that axis runs
        // along world Y instead, with the anchor end overlapping the plate's bottom edge and
        // the bend extending further away (more negative Y).
        var clipOutline = ClipOutlineProvider.BuildAlligatorClipOutline(ClipArmLengthMm, ClipGapMm, ClipArmThicknessMm);
        var rotatedOutline = RotateMinus90(clipOutline);
        var clipMesh = MeshExtruder.Extrude(rotatedOutline, zBottom: 0f, zTop: PlateThicknessMm);

        float anchorY = -(PlateHeightMm / 2f) + ClipOverlapMm;
        var positionedClipMesh = clipMesh.Translate(new Vector3(0f, anchorY, 0f));

        return Mesh3D.Combine(plateMesh, positionedClipMesh);
    }

    /// <summary>Rotates a polygon -90 degrees about the origin: (x,y) -> (y,-x). A proper rotation, so winding/normals are unaffected.</summary>
    private static Polygon2D RotateMinus90(Polygon2D polygon)
    {
        var result = new Polygon2D();
        foreach (var contour in polygon.Contours)
        {
            result.AddContour(contour.Points.Select(p => new Vector2(p.Y, -p.X)));
        }
        return result;
    }
}
