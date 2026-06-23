using NameTags.Core.Geometry;

namespace NameTags.Core.Outlines;

public sealed class CircleOutlineProvider : IShapeOutlineProvider
{
    private readonly OvalOutlineProvider _oval = new();

    public Polygon2D GetOutline(ShapeParams shapeParams, float targetWidthMm, float targetHeightMm)
    {
        float diameter = Math.Min(targetWidthMm, targetHeightMm);
        return _oval.GetOutline(shapeParams, diameter, diameter);
    }
}
