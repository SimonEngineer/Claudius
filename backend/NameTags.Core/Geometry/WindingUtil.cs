using System.Numerics;

namespace NameTags.Core.Geometry;

public static class WindingUtil
{
    /// <summary>Returns the points reversed if they are wound clockwise, so the result is always CCW.</summary>
    public static List<Vector2> EnsureCounterClockwise(List<Vector2> points)
    {
        float signedArea2 = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            var p0 = points[i];
            var p1 = points[(i + 1) % points.Count];
            signedArea2 += (p0.X * p1.Y) - (p1.X * p0.Y);
        }

        if (signedArea2 < 0)
        {
            var reversed = new List<Vector2>(points);
            reversed.Reverse();
            return reversed;
        }
        return points;
    }
}
