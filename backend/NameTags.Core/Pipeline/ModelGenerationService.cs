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

        var availableWidth = Math.Max(1f, request.PlateWidthMm - 2 * request.TextMarginMm);
        var availableHeight = Math.Max(1f, request.PlateHeightMm - 2 * request.TextMarginMm);

        var rawTextOutline = _glyphOutlineProvider.GetTextOutline(request.Text, request.FontFamilyOrPath, NominalTextSizeMm);
        var fittedTextOutline = rawTextOutline.FitToSize(availableWidth, availableHeight);

        float textBottom = request.PlateThicknessMm - request.OverlapEpsilonMm;
        float textTop = textBottom + request.TextDepthMm;
        var textMesh = MeshExtruder.Extrude(fittedTextOutline, textBottom, textTop);

        return Mesh3D.Combine(plateMesh, textMesh);
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
