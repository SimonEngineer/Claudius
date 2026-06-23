using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NameTags.Core.Geometry;
using SkiaSharp;

namespace NameTags.Core.Svg;

/// <summary>
/// Parses an uploaded SVG into a Polygon2D outline, fitted/centered to the requested plate size --
/// the same contract as IShapeOutlineProvider. Supports &lt;path&gt;, &lt;rect&gt;, &lt;circle&gt;,
/// &lt;ellipse&gt;, &lt;polygon&gt;, &lt;polyline&gt; geometry, and "transform" attributes
/// (translate/scale/rotate/matrix/skewX/skewY) composed through ancestor &lt;g&gt; chains.
/// &lt;svg viewBox&gt; scaling is not applied (uncommon for hand-authored outline SVGs) -- a known
/// follow-up gap, not handled here.
/// </summary>
public static class SvgOutlineParser
{
    private static readonly Regex TransformFunctionRegex = new(@"(\w+)\s*\(([^)]*)\)", RegexOptions.Compiled);
    private static readonly string[] NonVisualElements = { "defs", "clipPath", "mask", "symbol", "title", "desc" };

    public static Polygon2D Parse(byte[] svgBytes, float targetWidthMm, float targetHeightMm)
    {
        var xml = XDocument.Parse(System.Text.Encoding.UTF8.GetString(svgBytes));
        if (xml.Root is null)
        {
            throw new InvalidOperationException("The uploaded file is not a valid SVG document.");
        }

        var rawContours = new List<List<Vector2>>();
        Walk(xml.Root, Matrix3x2.Identity, rawContours);

        if (rawContours.Count == 0)
        {
            throw new InvalidOperationException("The uploaded SVG did not contain any usable outline geometry.");
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

    private static void Walk(XElement element, Matrix3x2 parentMatrix, List<List<Vector2>> rawContours)
    {
        if (NonVisualElements.Contains(element.Name.LocalName)) return;

        var matrix = ParseTransform(element.Attribute("transform")?.Value) * parentMatrix;

        using var localPath = BuildLocalPath(element);
        if (localPath is not null)
        {
            localPath.Transform(ToSkMatrix(matrix));
            AppendContours(rawContours, localPath);
        }

        foreach (var child in element.Elements())
        {
            Walk(child, matrix, rawContours);
        }
    }

    private static SKPath? BuildLocalPath(XElement element)
    {
        switch (element.Name.LocalName)
        {
            case "path":
            {
                var d = element.Attribute("d")?.Value;
                return string.IsNullOrWhiteSpace(d) ? null : SKPath.ParseSvgPathData(d);
            }

            case "rect":
            {
                float x = ParseFloat(element.Attribute("x")?.Value);
                float y = ParseFloat(element.Attribute("y")?.Value);
                float width = ParseFloat(element.Attribute("width")?.Value);
                float height = ParseFloat(element.Attribute("height")?.Value);
                if (width <= 0f || height <= 0f) return null;

                float? rxAttr = element.Attribute("rx") is { } rxA ? ParseFloat(rxA.Value) : null;
                float? ryAttr = element.Attribute("ry") is { } ryA ? ParseFloat(ryA.Value) : null;
                float rx = rxAttr ?? ryAttr ?? 0f;
                float ry = ryAttr ?? rxAttr ?? 0f;

                var path = new SKPath();
                if (rx > 0f || ry > 0f)
                    path.AddRoundRect(new SKRoundRect(SKRect.Create(x, y, width, height), rx, ry));
                else
                    path.AddRect(SKRect.Create(x, y, width, height));
                return path;
            }

            case "circle":
            {
                float cx = ParseFloat(element.Attribute("cx")?.Value);
                float cy = ParseFloat(element.Attribute("cy")?.Value);
                float r = ParseFloat(element.Attribute("r")?.Value);
                if (r <= 0f) return null;
                var path = new SKPath();
                path.AddCircle(cx, cy, r);
                return path;
            }

            case "ellipse":
            {
                float cx = ParseFloat(element.Attribute("cx")?.Value);
                float cy = ParseFloat(element.Attribute("cy")?.Value);
                float rx = ParseFloat(element.Attribute("rx")?.Value);
                float ry = ParseFloat(element.Attribute("ry")?.Value);
                if (rx <= 0f || ry <= 0f) return null;
                var path = new SKPath();
                path.AddOval(SKRect.Create(cx - rx, cy - ry, rx * 2f, ry * 2f));
                return path;
            }

            case "polygon":
            case "polyline":
            {
                var points = ParsePoints(element.Attribute("points")?.Value);
                if (points.Length < 2) return null;
                var path = new SKPath();
                // Treated as a closed silhouette either way -- an open polyline can't become a
                // printable solid contour.
                path.AddPoly(points, close: true);
                return path;
            }

            default:
                return null;
        }
    }

    private static SKPoint[] ParsePoints(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Array.Empty<SKPoint>();
        var numbers = SplitNumbers(value);
        var points = new List<SKPoint>(numbers.Count / 2);
        for (int i = 0; i + 1 < numbers.Count; i += 2)
        {
            points.Add(new SKPoint(numbers[i], numbers[i + 1]));
        }
        return points.ToArray();
    }

    private static List<float> SplitNumbers(string value) =>
        value.Split(new[] { ' ', ',', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f)
            .ToList();

    private static float ParseFloat(string? value) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f;

    /// <summary>
    /// Parses an SVG "transform" attribute (one or more translate/scale/rotate/matrix/skewX/skewY
    /// functions) into a single composed Matrix3x2. Per the SVG spec, when multiple functions are
    /// listed the rightmost (last-listed) one is applied to the point first. Expressed via .NET's
    /// Matrix3x2 operator* ("left operand applied first" when used as v' = v * (M1 * M2)), that means
    /// folding the parsed matrices starting from the last one listed.
    /// </summary>
    private static Matrix3x2 ParseTransform(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Matrix3x2.Identity;

        var matrices = new List<Matrix3x2>();
        foreach (Match match in TransformFunctionRegex.Matches(value))
        {
            var name = match.Groups[1].Value;
            var args = SplitNumbers(match.Groups[2].Value);

            matrices.Add(name switch
            {
                "translate" => Matrix3x2.CreateTranslation(
                    args.ElementAtOrDefault(0),
                    args.Count > 1 ? args[1] : 0f),
                "scale" => Matrix3x2.CreateScale(
                    args.Count > 0 ? args[0] : 1f,
                    args.Count > 1 ? args[1] : (args.Count > 0 ? args[0] : 1f)),
                "rotate" => CreateSvgRotation(
                    args.ElementAtOrDefault(0),
                    args.Count > 2 ? args[1] : 0f,
                    args.Count > 2 ? args[2] : 0f),
                "matrix" when args.Count >= 6 => new Matrix3x2(args[0], args[1], args[2], args[3], args[4], args[5]),
                "skewX" or "skewx" => new Matrix3x2(1f, 0f, MathF.Tan(args.ElementAtOrDefault(0) * MathF.PI / 180f), 1f, 0f, 0f),
                "skewY" or "skewy" => new Matrix3x2(1f, MathF.Tan(args.ElementAtOrDefault(0) * MathF.PI / 180f), 0f, 1f, 0f, 0f),
                _ => Matrix3x2.Identity,
            });
        }

        var combined = Matrix3x2.Identity;
        for (int i = matrices.Count - 1; i >= 0; i--)
        {
            combined *= matrices[i];
        }
        return combined;
    }

    private static Matrix3x2 CreateSvgRotation(float angleDegrees, float cx, float cy)
    {
        float a = angleDegrees * MathF.PI / 180f;
        float cosA = MathF.Cos(a);
        float sinA = MathF.Sin(a);
        return new Matrix3x2(
            cosA, sinA,
            -sinA, cosA,
            cx - cx * cosA + cy * sinA, cy - cx * sinA - cy * cosA);
    }

    private static SKMatrix ToSkMatrix(Matrix3x2 m) => new()
    {
        ScaleX = m.M11, SkewX = m.M21, TransX = m.M31,
        SkewY = m.M12, ScaleY = m.M22, TransY = m.M32,
        Persp0 = 0f, Persp1 = 0f, Persp2 = 1f,
    };

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
