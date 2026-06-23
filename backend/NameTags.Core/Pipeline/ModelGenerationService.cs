using System.Numerics;
using NameTags.Core.Extrusion;
using NameTags.Core.Geometry;
using NameTags.Core.Outlines;
using NameTags.Core.Svg;

namespace NameTags.Core.Pipeline;

/// <summary>
/// Single entry point for turning a TagGenerationRequest into a printable Mesh3D.
/// Always regenerates from scratch -- there is no cached intermediate to invalidate,
/// which is what lets a saved project be edited and re-previewed/re-downloaded freely.
/// </summary>
public sealed class ModelGenerationService
{
    private readonly IGlyphOutlineProvider _glyphOutlineProvider;
    private const float NominalTextSizeMm = 10f;

    public ModelGenerationService(IGlyphOutlineProvider? glyphOutlineProvider = null)
    {
        _glyphOutlineProvider = glyphOutlineProvider ?? new SkiaGlyphOutlineProvider();
    }

    public Mesh3D GenerateTagMesh(TagGenerationRequest request)
    {
        var plateOutline = GetPlateOutline(request);
        var plateMesh = MeshExtruder.Extrude(plateOutline, zBottom: 0f, zTop: request.PlateThicknessMm);

        var availableWidth = Math.Max(1f, request.PlateWidthMm - request.TextMarginLeftMm - request.TextMarginRightMm);
        var availableHeight = Math.Max(1f, request.PlateHeightMm - request.TextMarginTopMm - request.TextMarginBottomMm);

        var rawTextOutline = _glyphOutlineProvider.GetTextOutline(request.Text, request.FontFamilyOrPath, NominalTextSizeMm);
        var fittedTextOutline = rawTextOutline.FitToSize(availableWidth, availableHeight);
        var alignedTextOutline = AlignWithinPlate(fittedTextOutline, request, availableWidth, availableHeight);
        alignedTextOutline = EnsureWithinPlate(fittedTextOutline, alignedTextOutline, plateOutline, request, availableWidth, availableHeight);

        float textBottom = request.PlateThicknessMm - request.OverlapEpsilonMm;
        float textTop = textBottom + request.TextDepthMm;
        var textMesh = MeshExtruder.Extrude(alignedTextOutline, textBottom, textTop);

        return Mesh3D.Combine(plateMesh, textMesh);
    }

    /// <summary>
    /// Moves a text outline (already fit to size and centered at the origin) into its
    /// final position: first re-centers it on the available box (which is off-center
    /// from the plate whenever the per-side margins are asymmetric), then nudges it to
    /// the requested edge per the horizontal/vertical alignment, leaving it centered
    /// within the available box along any axis set to Center.
    /// </summary>
    private static Polygon2D AlignWithinPlate(Polygon2D textOutline, TagGenerationRequest request, float availableWidth, float availableHeight)
    {
        var marginCenterOffset = new Vector2(
            (request.TextMarginLeftMm - request.TextMarginRightMm) / 2f,
            (request.TextMarginBottomMm - request.TextMarginTopMm) / 2f);

        var (min, max) = textOutline.GetBounds();
        float halfTextWidth = (max.X - min.X) / 2f;
        float halfTextHeight = (max.Y - min.Y) / 2f;

        float alignX = request.TextHorizontalAlign switch
        {
            TextHorizontalAlign.Left => -(availableWidth / 2f - halfTextWidth),
            TextHorizontalAlign.Right => availableWidth / 2f - halfTextWidth,
            _ => 0f,
        };
        float alignY = request.TextVerticalAlign switch
        {
            TextVerticalAlign.Top => availableHeight / 2f - halfTextHeight,
            TextVerticalAlign.Bottom => -(availableHeight / 2f - halfTextHeight),
            _ => 0f,
        };

        return textOutline.Translate(marginCenterOffset + new Vector2(alignX, alignY));
    }

    /// <summary>
    /// Guarantees every vertex of the text outline falls within the plate's actual
    /// silhouette (not just its bounding box) -- this app's purpose is 3D printing, so
    /// text poking outside a curved/pointed plate edge (Heart, Star, Oval, Plaque,
    /// CustomSvg) is a correctness bug, not cosmetic. If the already-aligned text fails
    /// containment, binary-searches a uniform shrink of the pre-alignment (origin-centered)
    /// outline, re-running alignment at each candidate scale so the result keeps hugging its
    /// chosen edge instead of leaving an inconsistent gap.
    /// </summary>
    private static Polygon2D EnsureWithinPlate(
        Polygon2D fittedTextOutline, Polygon2D alignedTextOutline, Polygon2D plateOutline,
        TagGenerationRequest request, float availableWidth, float availableHeight)
    {
        if (IsFullyInside(alignedTextOutline, plateOutline)) return alignedTextOutline;

        float lo = 0f, hi = 1f;
        for (int i = 0; i < 25; i++)
        {
            float mid = (lo + hi) / 2f;
            var candidate = AlignWithinPlate(fittedTextOutline.Scale(mid, Vector2.Zero), request, availableWidth, availableHeight);
            if (IsFullyInside(candidate, plateOutline)) lo = mid; else hi = mid;
        }

        return AlignWithinPlate(fittedTextOutline.Scale(lo, Vector2.Zero), request, availableWidth, availableHeight);
    }

    private static bool IsFullyInside(Polygon2D textOutline, Polygon2D plateOutline)
    {
        foreach (var contour in textOutline.Contours)
        {
            foreach (var point in contour.Points)
            {
                if (!PointInPolygon.IsInside(plateOutline, point)) return false;
            }
        }
        return true;
    }

    private static Polygon2D GetPlateOutline(TagGenerationRequest request)
    {
        if (request.ShapeType == ShapeType.CustomSvg)
        {
            if (request.CustomSvgBytes is null)
            {
                throw new InvalidOperationException("CustomSvgBytes is required when ShapeType is CustomSvg.");
            }
            return SvgOutlineParser.Parse(request.CustomSvgBytes, request.PlateWidthMm, request.PlateHeightMm);
        }

        var provider = ShapeOutlineProviderFactory.Resolve(request.ShapeType);
        return provider.GetOutline(request.ShapeParams, request.PlateWidthMm, request.PlateHeightMm);
    }
}
