using System.Numerics;

namespace NameTags.Core.Geometry;

/// <summary>
/// Winding-number point-in-polygon test (Sunday's algorithm), summed across every contour
/// of a Polygon2D. This naturally matches the NonZero fill rule the rest of the pipeline
/// already uses for holes: a point inside an outer (CCW) contour but also inside a nested
/// hole (CW) contour gets a net winding of zero and is correctly reported as outside.
/// </summary>
public static class PointInPolygon
{
    public static bool IsInside(Polygon2D polygon, Vector2 point)
    {
        int winding = 0;
        foreach (var contour in polygon.Contours)
        {
            winding += WindingContribution(contour.Points, point);
        }
        return winding != 0;
    }

    private static int WindingContribution(List<Vector2> points, Vector2 point)
    {
        int winding = 0;
        int n = points.Count;
        for (int i = 0; i < n; i++)
        {
            var p0 = points[i];
            var p1 = points[(i + 1) % n];
            if (p0.Y <= point.Y)
            {
                if (p1.Y > point.Y && IsLeft(p0, p1, point) > 0) winding++;
            }
            else
            {
                if (p1.Y <= point.Y && IsLeft(p0, p1, point) < 0) winding--;
            }
        }
        return winding;
    }

    private static float IsLeft(Vector2 a, Vector2 b, Vector2 p) =>
        (b.X - a.X) * (p.Y - a.Y) - (p.X - a.X) * (b.Y - a.Y);
}
