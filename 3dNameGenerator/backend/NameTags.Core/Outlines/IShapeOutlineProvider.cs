using NameTags.Core.Geometry;

namespace NameTags.Core.Outlines;

/// <summary>
/// Produces a single-contour (no holes) 2D outline sized to fit within targetWidth x targetHeight (mm),
/// centered at the origin, wound counter-clockwise (solid-to-the-left convention used by MeshExtruder).
/// </summary>
public interface IShapeOutlineProvider
{
    Polygon2D GetOutline(ShapeParams shapeParams, float targetWidthMm, float targetHeightMm);
}
