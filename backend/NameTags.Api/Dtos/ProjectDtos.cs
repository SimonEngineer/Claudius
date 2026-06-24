using NameTags.Core.Outlines;
using NameTags.Core.Pipeline;
using NameTags.Data.Entities;

namespace NameTags.Api.Dtos;

public sealed record TagNameDto(int Id, string Text, int SortOrder, float? TextDepthMmOverride);

public sealed record UpdateNameOverrideDto(float? TextDepthMmOverride);

public sealed record MountingHoleDto(int Id, float OffsetXMm, float OffsetYMm, float DiameterMm)
{
    public static MountingHoleDto FromEntity(TagMountingHole h) => new(h.Id, h.OffsetXMm, h.OffsetYMm, h.DiameterMm);
}

public sealed record SaveMountingHoleDto(float OffsetXMm, float OffsetYMm, float DiameterMm = 4f);

public sealed record ProjectSummaryDto(int Id, string Name, ShapeType ShapeType, int NameCount, DateTime CreatedAt);

public sealed record ProjectDetailDto(
    int Id,
    string Name,
    ShapeType ShapeType,
    ShapeParamsDto ShapeParams,
    string? CustomSvgBase64,
    string FontFamilyOrPath,
    float PlateWidthMm,
    float PlateHeightMm,
    float PlateThicknessMm,
    float TextDepthMm,
    float TextMarginLeftMm,
    float TextMarginRightMm,
    float TextMarginTopMm,
    float TextMarginBottomMm,
    TextHorizontalAlign TextHorizontalAlign,
    TextVerticalAlign TextVerticalAlign,
    float BevelMm,
    DateTime CreatedAt,
    List<TagNameDto> Names,
    List<MountingHoleDto> MountingHoles)
{
    public static ProjectDetailDto FromEntity(TagProject p) => new(
        p.Id,
        p.Name,
        p.ShapeType,
        new ShapeParamsDto(p.CornerRadiusMm, p.StarPoints, p.StarInnerRadiusRatio, p.CurveSegments),
        p.CustomSvgBytes is null ? null : Convert.ToBase64String(p.CustomSvgBytes),
        p.FontFamilyOrPath,
        p.PlateWidthMm,
        p.PlateHeightMm,
        p.PlateThicknessMm,
        p.TextDepthMm,
        p.TextMarginLeftMm,
        p.TextMarginRightMm,
        p.TextMarginTopMm,
        p.TextMarginBottomMm,
        p.TextHorizontalAlign,
        p.TextVerticalAlign,
        p.BevelMm,
        p.CreatedAt,
        p.Names.OrderBy(n => n.SortOrder)
            .Select(n => new TagNameDto(n.Id, n.Text, n.SortOrder, n.TextDepthMmOverride))
            .ToList(),
        p.MountingHoles.Select(MountingHoleDto.FromEntity).ToList());
}

public sealed record SaveProjectDto(
    string Name,
    ShapeType ShapeType = ShapeType.RoundedRectangle,
    ShapeParamsDto? ShapeParams = null,
    string? CustomSvgBase64 = null,
    string FontFamilyOrPath = "DejaVuSans-Bold.ttf",
    float PlateWidthMm = 70f,
    float PlateHeightMm = 30f,
    float PlateThicknessMm = 3f,
    float TextDepthMm = 2f,
    float TextMarginLeftMm = 5f,
    float TextMarginRightMm = 5f,
    float TextMarginTopMm = 5f,
    float TextMarginBottomMm = 5f,
    TextHorizontalAlign TextHorizontalAlign = TextHorizontalAlign.Center,
    TextVerticalAlign TextVerticalAlign = TextVerticalAlign.Center,
    float BevelMm = 0f,
    List<SaveMountingHoleDto>? MountingHoles = null);
