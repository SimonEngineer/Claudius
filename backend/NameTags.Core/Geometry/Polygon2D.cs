using System.Numerics;

namespace NameTags.Core.Geometry;

/// <summary>A single closed loop of points in 2D, e.g. a glyph outer boundary or an inner hole.</summary>
public sealed class Contour
{
    public List<Vector2> Points { get; }

    public Contour(IEnumerable<Vector2> points)
    {
        Points = points.ToList();
    }

    /// <summary>Twice the signed area (shoelace formula). Positive = counter-clockwise winding.</summary>
    public float SignedArea2()
    {
        float sum = 0f;
        for (int i = 0; i < Points.Count; i++)
        {
            var p0 = Points[i];
            var p1 = Points[(i + 1) % Points.Count];
            sum += (p0.X * p1.Y) - (p1.X * p0.Y);
        }
        return sum;
    }

    public bool IsCounterClockwise => SignedArea2() > 0;
}

/// <summary>A 2D shape made of one or more contours (outer boundaries plus optional holes).</summary>
public sealed class Polygon2D
{
    public List<Contour> Contours { get; } = new();

    public void AddContour(IEnumerable<Vector2> points) => Contours.Add(new Contour(points));

    public (Vector2 Min, Vector2 Max) GetBounds()
    {
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        foreach (var contour in Contours)
        {
            foreach (var p in contour.Points)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
        }
        return (min, max);
    }

    /// <summary>Returns a new polygon translated and uniformly scaled so its bounds match the given size, centered at the origin.</summary>
    public Polygon2D FitToSize(float targetWidth, float targetHeight)
    {
        var (min, max) = GetBounds();
        var size = max - min;
        float scale = Math.Min(
            size.X > 0 ? targetWidth / size.X : 1f,
            size.Y > 0 ? targetHeight / size.Y : 1f);
        var center = (min + max) / 2f;

        var result = new Polygon2D();
        foreach (var contour in Contours)
        {
            result.AddContour(contour.Points.Select(p => (p - center) * scale));
        }
        return result;
    }

    public Polygon2D Translate(Vector2 offset)
    {
        var result = new Polygon2D();
        foreach (var contour in Contours)
        {
            result.AddContour(contour.Points.Select(p => p + offset));
        }
        return result;
    }

    /// <summary>Returns a new polygon uniformly scaled about the given anchor point (not necessarily its own center).</summary>
    public Polygon2D Scale(float scale, Vector2 anchor)
    {
        var result = new Polygon2D();
        foreach (var contour in Contours)
        {
            result.AddContour(contour.Points.Select(p => anchor + (p - anchor) * scale));
        }
        return result;
    }
}
