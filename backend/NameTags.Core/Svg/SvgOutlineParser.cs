using System.Numerics;
using System.Xml.Linq;
using NameTags.Core.Geometry;
using SkiaSharp;

namespace NameTags.Core.Svg;

/// <summary>
/// Parses &lt;path&gt; elements out of an uploaded SVG and turns them into a Polygon2D outline,
/// fitted/centered to the requested plate size -- the same contract as IShapeOutlineProvider.
/// v1 scope: only &lt;path d="..."&gt; geometry is read (the common case for outline/silhouette
/// SVGs); group/element transforms are not applied.
/// </summary>
public static class SvgOutlineParser
{
    public static Polygon2D Parse(byte[] svgBytes, float targetWidthMm, float targetHeightMm)
    {
        var xml = XDocument.Parse(System.Text.Encoding.UTF8.GetString(svgBytes));
        var pathData = xml.Descendants()
            .Where(e => e.Name.LocalName == "path")
            .Select(e => e.Attribute("d")?.Value)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .ToList();

        if (pathData.Count == 0)
        {
            throw new InvalidOperationException("No <path> elements with a 'd' attribute were found in the uploaded SVG.");
        }

        var rawContours = new List<List<Vector2>>();
        foreach (var d in pathData)
        {
            using var path = SKPath.ParseSvgPathData(d);
            if (path is null) continue;
            AppendContours(rawContours, path);
        }

        if (rawContours.Count == 0)
        {
            throw new InvalidOperationException("The uploaded SVG paths did not produce any usable geometry.");
        }

        int largestIndex = 0;
        float largestAbsArea = 0f;
        for (int i = 0; i < rawContours.Count; i++)
        {
            float area = SignedArea2(rawContours[i]);
            if (Math.Abs(area) > largestAbsArea)
            {
                largestAbsArea = Math.Abs(area);
                largestIndex = i;
            }
        }

        bool needsFlip = SignedArea2(rawContours[largestIndex]) < 0;
        var polygon = new Polygon2D();
        foreach (var contour in rawContours)
        {
            if (needsFlip) contour.Reverse();
            polygon.AddContour(contour);
        }

        return polygon.FitToSize(targetWidthMm, targetHeightMm);
    }

    private static void AppendContours(List<List<Vector2>> rawContours, SKPath path)
    {
        using var measure = new SKPathMeasure(path, forceClosed: true);
        do
        {
            float length = measure.Length;
            if (length <= 0f) continue;

            int steps = Math.Max(8, (int)(length / 0.5f));
            var points = new List<Vector2>(steps);
            for (int s = 0; s < steps; s++)
            {
                float dist = length * s / steps;
                if (measure.GetPosition(dist, out var pos))
                {
                    // SVG coordinates are y-down; flip to our y-up mesh convention.
                    points.Add(new Vector2(pos.X, -pos.Y));
                }
            }
            if (points.Count >= 3)
            {
                rawContours.Add(points);
            }
        } while (measure.NextContour());
    }

    private static float SignedArea2(List<Vector2> points)
    {
        float sum = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            var p0 = points[i];
            var p1 = points[(i + 1) % points.Count];
            sum += (p0.X * p1.Y) - (p1.X * p0.Y);
        }
        return sum;
    }
}
