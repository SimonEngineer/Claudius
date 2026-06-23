using System.Numerics;
using NameTags.Core.Geometry;

namespace NameTags.Core.Outlines;

public sealed class RoundedRectangleOutlineProvider : IShapeOutlineProvider
{
    public Polygon2D GetOutline(ShapeParams shapeParams, float targetWidthMm, float targetHeightMm)
    {
        float w = targetWidthMm / 2f;
        float h = targetHeightMm / 2f;
        float r = Math.Min(shapeParams.CornerRadiusMm, Math.Min(w, h));
        int segmentsPerCorner = Math.Max(2, shapeParams.CurveSegments / 4);

        var points = new List<Vector2>();

        // Corner centers, walked CCW starting bottom-right corner's arc.
        AddCornerArc(points, new Vector2(w - r, -(h - r)), r, -90f, 0f, segmentsPerCorner);   // bottom-right
        AddCornerArc(points, new Vector2(w - r, h - r), r, 0f, 90f, segmentsPerCorner);        // top-right
        AddCornerArc(points, new Vector2(-(w - r), h - r), r, 90f, 180f, segmentsPerCorner);   // top-left
        AddCornerArc(points, new Vector2(-(w - r), -(h - r)), r, 180f, 270f, segmentsPerCorner); // bottom-left

        var polygon = new Polygon2D();
        polygon.AddContour(points);
        return polygon;
    }

    private static void AddCornerArc(List<Vector2> points, Vector2 center, float radius, float startDeg, float endDeg, int segments)
    {
        for (int i = 0; i <= segments; i++)
        {
            float t = startDeg + (endDeg - startDeg) * i / segments;
            float rad = t * MathF.PI / 180f;
            points.Add(center + new Vector2(radius * MathF.Cos(rad), radius * MathF.Sin(rad)));
        }
    }
}
