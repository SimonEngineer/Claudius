using System.Numerics;
using NameTags.Core.Geometry;

namespace NameTags.Core.Outlines;

/// <summary>
/// Builds a 2D "open hook" outline (like a shepherd's crook / open J-hook): a thick
/// circular band that sweeps most of the way around a circle, leaving a gap wide enough
/// to slip onto a wine glass stem. It's a single CCW contour, no holes, extruded the
/// same way the flat plate is, then fused into the charm plate by overlapping the band's
/// straight end into the plate's footprint.
/// </summary>
public static class HookOutlineProvider
{
    /// <param name="outerRadiusMm">Outer radius of the hook's circular band.</param>
    /// <param name="bandThicknessMm">Radial thickness of the band.</param>
    /// <param name="openingDegrees">Angular size of the gap left open (centered at the bottom, pointing away from the anchor).</param>
    public static Polygon2D BuildOpenHookOutline(float outerRadiusMm, float bandThicknessMm, float openingDegrees = 90f, int segmentsPerArc = 48)
    {
        float innerRadius = outerRadiusMm - bandThicknessMm;
        float sweepDegrees = 360f - openingDegrees;
        float startDegrees = 90f + openingDegrees / 2f;

        var points = new List<Vector2>();

        // Outer arc, sweeping CCW from the start angle around the top to the end angle.
        for (int i = 0; i <= segmentsPerArc; i++)
        {
            float angle = MathF.PI / 180f * (startDegrees + sweepDegrees * i / segmentsPerArc);
            points.Add(new Vector2(outerRadiusMm * MathF.Cos(angle), outerRadiusMm * MathF.Sin(angle)));
        }

        // Inner arc, sweeping back CW to close the band.
        for (int i = 0; i <= segmentsPerArc; i++)
        {
            float angle = MathF.PI / 180f * (startDegrees + sweepDegrees - sweepDegrees * i / segmentsPerArc);
            points.Add(new Vector2(innerRadius * MathF.Cos(angle), innerRadius * MathF.Sin(angle)));
        }

        var polygon = new Polygon2D();
        polygon.AddContour(points);
        return polygon;
    }
}
