using System.Collections.Concurrent;
using System.Numerics;
using NameTags.Core.Geometry;
using SkiaSharp;

namespace NameTags.Core.Outlines;

/// <summary>
/// Extracts vector glyph outlines via SkiaSharp. fontFamilyOrPath may be an absolute path to a
/// .ttf/.otf file (used directly) or a family name resolved through the system font manager.
/// No kerning/shaping is applied (acceptable for v1 -- a simple per-glyph cursor advance).
/// </summary>
public sealed class SkiaGlyphOutlineProvider : IGlyphOutlineProvider
{
    private static readonly ConcurrentDictionary<string, SKTypeface> TypefaceCache = new();

    public Polygon2D GetTextOutline(string text, string fontFamilyOrPath, float fontSizeMm)
    {
        var typeface = ResolveTypeface(fontFamilyOrPath);
        using var font = new SKFont(typeface, fontSizeMm);

        var glyphIds = font.GetGlyphs(text);
        var widths = font.GetGlyphWidths(glyphIds, paint: null);

        var polygon = new Polygon2D();
        float cursorX = 0f;

        for (int i = 0; i < glyphIds.Length; i++)
        {
            using var path = font.GetGlyphPath(glyphIds[i]);
            if (path is not null)
            {
                AppendGlyphContours(polygon, path, cursorX);
            }
            cursorX += widths[i];
        }

        return polygon;
    }

    private static void AppendGlyphContours(Polygon2D polygon, SKPath path, float offsetX)
    {
        var glyphContours = new List<List<Vector2>>();

        using var measure = new SKPathMeasure(path, forceClosed: true);
        do
        {
            float length = measure.Length;
            if (length <= 0f) continue;

            int steps = Math.Max(8, (int)(length / 0.4f));
            var points = new List<Vector2>(steps);
            for (int s = 0; s < steps; s++)
            {
                float dist = length * s / steps;
                if (measure.GetPosition(dist, out var pos))
                {
                    // Skia text coordinates are y-down; flip to our y-up mesh convention.
                    points.Add(new Vector2(pos.X + offsetX, -pos.Y));
                }
            }
            if (points.Count >= 3)
            {
                glyphContours.Add(points);
            }
        } while (measure.NextContour());

        if (glyphContours.Count == 0) return;

        // TrueType winds outer/hole contours oppositely and consistently; a uniform y-flip
        // preserves that relationship. Normalize so the largest contour (the outer boundary) is CCW.
        int largestIndex = 0;
        float largestAbsArea = 0f;
        for (int i = 0; i < glyphContours.Count; i++)
        {
            float area = SignedArea2(glyphContours[i]);
            if (Math.Abs(area) > largestAbsArea)
            {
                largestAbsArea = Math.Abs(area);
                largestIndex = i;
            }
        }

        bool needsFlip = SignedArea2(glyphContours[largestIndex]) < 0;
        foreach (var contour in glyphContours)
        {
            if (needsFlip) contour.Reverse();
            polygon.AddContour(contour);
        }
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

    private static SKTypeface ResolveTypeface(string fontFamilyOrPath)
    {
        return TypefaceCache.GetOrAdd(fontFamilyOrPath, key =>
            File.Exists(key)
                ? SKTypeface.FromFile(key)
                : SKTypeface.FromFamilyName(key) ?? SKTypeface.Default);
    }
}
