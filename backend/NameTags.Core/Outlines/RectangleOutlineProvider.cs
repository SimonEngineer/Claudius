using System.Numerics;
using NameTags.Core.Geometry;

namespace NameTags.Core.Outlines;

public sealed class RectangleOutlineProvider : IShapeOutlineProvider
{
    public Polygon2D GetOutline(ShapeParams shapeParams, float targetWidthMm, float targetHeightMm)
    {
        float w = targetWidthMm / 2f;
        float h = targetHeightMm / 2f;

        var polygon = new Polygon2D();
        polygon.AddContour(new[]
        {
            new Vector2(-w, -h),
            new Vector2(w, -h),
            new Vector2(w, h),
            new Vector2(-w, h),
        });
        return polygon;
    }
}
