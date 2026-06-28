using System.Numerics;
using NameTags.Core.Geometry;

namespace NameTags.Core.Outlines;

/// <summary>A banner/ribbon plaque: a rectangular body with pointed (chevron) flares on the left and right ends.</summary>
public sealed class PlaqueOutlineProvider : IShapeOutlineProvider
{
    public Polygon2D GetOutline(ShapeParams shapeParams, float targetWidthMm, float targetHeightMm)
    {
        float h = targetHeightMm / 2f;
        float flare = Math.Min(shapeParams.CornerRadiusMm * 2f, targetWidthMm / 4f);
        float bodyHalfWidth = targetWidthMm / 2f - flare;

        var points = new List<Vector2>
        {
            new(-bodyHalfWidth, -h),
            new(bodyHalfWidth, -h),
            new(bodyHalfWidth + flare, 0),
            new(bodyHalfWidth, h),
            new(-bodyHalfWidth, h),
            new(-bodyHalfWidth - flare, 0),
        };

        var polygon = new Polygon2D();
        polygon.AddContour(WindingUtil.EnsureCounterClockwise(points));
        return polygon;
    }
}
