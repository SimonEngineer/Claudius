namespace NameTags.Core.Outlines;

public static class ShapeOutlineProviderFactory
{
    public static IShapeOutlineProvider Resolve(ShapeType shapeType) => shapeType switch
    {
        ShapeType.Rectangle => new RectangleOutlineProvider(),
        ShapeType.RoundedRectangle => new RoundedRectangleOutlineProvider(),
        ShapeType.Oval => new OvalOutlineProvider(),
        ShapeType.Circle => new CircleOutlineProvider(),
        ShapeType.Heart => new HeartOutlineProvider(),
        ShapeType.Star => new StarOutlineProvider(),
        ShapeType.Plaque => new PlaqueOutlineProvider(),
        ShapeType.CustomSvg => throw new InvalidOperationException(
            "CustomSvg outlines are produced via CustomSvgOutlineProvider, not this factory."),
        _ => throw new ArgumentOutOfRangeException(nameof(shapeType), shapeType, null),
    };
}
