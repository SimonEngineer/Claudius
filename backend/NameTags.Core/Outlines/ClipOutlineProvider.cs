using System.Numerics;
using NameTags.Core.Geometry;

namespace NameTags.Core.Outlines;

/// <summary>
/// Builds a 2D "alligator clip" outline: a single bent spring strip shaped like a
/// horseshoe/U -- a semicircular spring bend (anchored into the plate) with two parallel
/// arms extending away from the plate, separated by a gap that grips fabric. Printed as
/// one flat piece (same extrusion direction as the plate), it relies on the printed
/// material's own flex at the bend to act as the spring; this is a v1 design and may need
/// per-printer/filament tuning of ArmThicknessMm to flex reliably without snapping.
/// </summary>
public static class ClipOutlineProvider
{
    /// <param name="armLengthMm">Length of each arm, measured from the bend.</param>
    /// <param name="gapMm">Open gap between the two arms' facing inner surfaces -- the fabric bite.</param>
    /// <param name="armThicknessMm">Thickness of each arm / the spring band.</param>
    public static Polygon2D BuildAlligatorClipOutline(float armLengthMm, float gapMm, float armThicknessMm, int bendSegments = 24)
    {
        float outerHalf = gapMm / 2f + armThicknessMm;
        float innerHalf = gapMm / 2f;

        var points = new List<Vector2>();

        // Outer edge: back end of the upper arm -> forward to the bend.
        points.Add(new Vector2(0f, outerHalf));
        points.Add(new Vector2(armLengthMm, outerHalf));

        // Outer semicircle around the bend (front, u = armLengthMm): sweeps from top (+90 deg)
        // through the front-most point (0 deg) to the bottom (-90 deg), centered there.
        float outerRadius = outerHalf;
        for (int i = 0; i <= bendSegments; i++)
        {
            float angle = MathF.PI / 2f - MathF.PI * i / bendSegments;
            points.Add(new Vector2(armLengthMm + outerRadius * MathF.Cos(angle), outerRadius * MathF.Sin(angle)));
        }

        // Outer edge: lower arm, bend back to the open back end.
        points.Add(new Vector2(0f, -outerHalf));

        // Inner edge: back end of the lower arm -> forward to the bend.
        points.Add(new Vector2(0f, -innerHalf));
        points.Add(new Vector2(armLengthMm, -innerHalf));

        // Inner semicircle around the bend, sweeping back from bottom (-90 deg) to top (+90 deg).
        float innerRadius = innerHalf;
        for (int i = 0; i <= bendSegments; i++)
        {
            float angle = -MathF.PI / 2f + MathF.PI * i / bendSegments;
            points.Add(new Vector2(armLengthMm + innerRadius * MathF.Cos(angle), innerRadius * MathF.Sin(angle)));
        }

        // Inner edge: upper arm, bend back to the open back end (closes the loop).
        points.Add(new Vector2(0f, innerHalf));

        // The trace above runs clockwise; reverse to satisfy the CCW-outer-boundary convention
        // the triangulator/extruder rely on.
        points.Reverse();

        var polygon = new Polygon2D();
        polygon.AddContour(points);
        return polygon;
    }
}
