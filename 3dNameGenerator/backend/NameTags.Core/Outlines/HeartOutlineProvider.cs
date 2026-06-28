using System.Numerics;
using NameTags.Core.Geometry;

namespace NameTags.Core.Outlines;

public sealed class HeartOutlineProvider : IShapeOutlineProvider
{
    public Polygon2D GetOutline(ShapeParams shapeParams, float targetWidthMm, float targetHeightMm)
    {
        int segments = Math.Max(16, shapeParams.CurveSegments);
        var points = new List<Vector2>(segments);

        // Classic parametric heart curve. t in [0, 2*PI), increasing t traces CCW.
        for (int i = 0; i < segments; i++)
        {
            float t = i * 2f * MathF.PI / segments;
            float x = 16f * MathF.Pow(MathF.Sin(t), 3);
            float y = 13f * MathF.Cos(t) - 5f * MathF.Cos(2 * t) - 2f * MathF.Cos(3 * t) - MathF.Cos(4 * t);
            points.Add(new Vector2(x, y));
        }

        var raw = new Polygon2D();
        raw.AddContour(WindingUtil.EnsureCounterClockwise(points));
        return raw.FitToSize(targetWidthMm, targetHeightMm);
    }
}
