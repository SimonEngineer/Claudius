using System.Numerics;
using NameTags.Core.Geometry;

namespace NameTags.Core.Outlines;

public sealed class OvalOutlineProvider : IShapeOutlineProvider
{
    public Polygon2D GetOutline(ShapeParams shapeParams, float targetWidthMm, float targetHeightMm)
    {
        float rx = targetWidthMm / 2f;
        float ry = targetHeightMm / 2f;
        int segments = Math.Max(8, shapeParams.CurveSegments);

        var points = new List<Vector2>(segments);
        for (int i = 0; i < segments; i++)
        {
            float t = i * 2f * MathF.PI / segments;
            points.Add(new Vector2(rx * MathF.Cos(t), ry * MathF.Sin(t)));
        }

        var polygon = new Polygon2D();
        polygon.AddContour(points);
        return polygon;
    }
}
