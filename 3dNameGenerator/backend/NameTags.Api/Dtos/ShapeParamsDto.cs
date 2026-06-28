using NameTags.Core.Outlines;

namespace NameTags.Api.Dtos;

public sealed record ShapeParamsDto(
    float CornerRadiusMm = 4f,
    int StarPoints = 5,
    float StarInnerRadiusRatio = 0.45f,
    int CurveSegments = 48)
{
    public ShapeParams ToShapeParams() => new()
    {
        CornerRadiusMm = CornerRadiusMm,
        StarPoints = StarPoints,
        StarInnerRadiusRatio = StarInnerRadiusRatio,
        CurveSegments = CurveSegments,
    };

    public static ShapeParamsDto FromShapeParams(ShapeParams p) => new(
        p.CornerRadiusMm, p.StarPoints, p.StarInnerRadiusRatio, p.CurveSegments);
}
