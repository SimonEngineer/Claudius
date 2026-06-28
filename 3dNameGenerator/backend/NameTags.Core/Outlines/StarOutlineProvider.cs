using System.Numerics;
using NameTags.Core.Geometry;

namespace NameTags.Core.Outlines;

public sealed class StarOutlineProvider : IShapeOutlineProvider
{
    public Polygon2D GetOutline(ShapeParams shapeParams, float targetWidthMm, float targetHeightMm)
    {
        int points = Math.Max(3, shapeParams.StarPoints);
        float outerRx = targetWidthMm / 2f;
        float outerRy = targetHeightMm / 2f;
        float innerRatio = Math.Clamp(shapeParams.StarInnerRadiusRatio, 0.05f, 0.95f);

        var vertices = new List<Vector2>(points * 2);
        int totalVertices = points * 2;
        for (int i = 0; i < totalVertices; i++)
        {
            float t = i * MathF.PI / points - MathF.PI / 2f; // start pointing up
            float r = (i % 2 == 0) ? 1f : innerRatio;
            vertices.Add(new Vector2(outerRx * r * MathF.Cos(t), outerRy * r * MathF.Sin(t)));
        }

        var polygon = new Polygon2D();
        polygon.AddContour(vertices);
        return polygon;
    }
}
