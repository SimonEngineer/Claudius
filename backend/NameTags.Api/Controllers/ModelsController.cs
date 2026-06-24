using Microsoft.AspNetCore.Mvc;
using NameTags.Api.Dtos;
using NameTags.Api.Validation;
using NameTags.Core.Export;
using NameTags.Core.Geometry;
using NameTags.Core.Outlines;
using NameTags.Core.Pipeline;

namespace NameTags.Api.Controllers;

/// <summary>
/// Generates a full tag (text embossed on a plate) directly from request parameters.
/// Both preview and download return identical bytes, regenerated fresh on every call --
/// there is no persisted mesh, so editing params and re-requesting always reflects the
/// current state.
/// </summary>
[ApiController]
[Route("api/models")]
public class ModelsController : ControllerBase
{
    private readonly ModelGenerationService _generationService = new();

    public sealed record GenerateTagRequestDto(
        string Text,
        ShapeType ShapeType = ShapeType.RoundedRectangle,
        float PlateWidthMm = 70f,
        float PlateHeightMm = 30f,
        float PlateThicknessMm = 3f,
        float TextDepthMm = 2f,
        string FontFamilyOrPath = "DejaVuSans-Bold.ttf",
        byte[]? CustomSvgBytes = null,
        ShapeParamsDto? ShapeParams = null,
        float TextMarginLeftMm = 5f,
        float TextMarginRightMm = 5f,
        float TextMarginTopMm = 5f,
        float TextMarginBottomMm = 5f,
        TextHorizontalAlign TextHorizontalAlign = TextHorizontalAlign.Center,
        TextVerticalAlign TextVerticalAlign = TextVerticalAlign.Center,
        List<MountingHole>? MountingHoles = null);

    [HttpPost("preview.stl")]
    public IActionResult Preview([FromBody] GenerateTagRequestDto dto) => GenerateStl(dto, asAttachment: false);

    [HttpPost("download.stl")]
    public IActionResult Download([FromBody] GenerateTagRequestDto dto) => GenerateStl(dto, asAttachment: true);

    private IActionResult GenerateStl(GenerateTagRequestDto dto, bool asAttachment)
    {
        var textError = TagTextValidator.Validate(dto.Text);
        if (textError is not null) return BadRequest(new { error = textError });

        var fontPath = ResolveFontPath(dto.FontFamilyOrPath);
        var request = new TagGenerationRequest
        {
            Text = dto.Text,
            ShapeType = dto.ShapeType,
            FontFamilyOrPath = fontPath,
            PlateWidthMm = dto.PlateWidthMm,
            PlateHeightMm = dto.PlateHeightMm,
            PlateThicknessMm = dto.PlateThicknessMm,
            TextDepthMm = dto.TextDepthMm,
            TextMarginLeftMm = dto.TextMarginLeftMm,
            TextMarginRightMm = dto.TextMarginRightMm,
            TextMarginTopMm = dto.TextMarginTopMm,
            TextMarginBottomMm = dto.TextMarginBottomMm,
            TextHorizontalAlign = dto.TextHorizontalAlign,
            TextVerticalAlign = dto.TextVerticalAlign,
            CustomSvgBytes = dto.CustomSvgBytes,
            ShapeParams = (dto.ShapeParams ?? new ShapeParamsDto()).ToShapeParams(),
            MountingHoles = dto.MountingHoles ?? new List<MountingHole>(),
        };

        var mesh = _generationService.GenerateTagMesh(request);
        var stream = new MemoryStream();
        StlWriter.WriteBinary(stream, mesh);
        stream.Position = 0;

        if (!asAttachment)
        {
            return File(stream, "model/stl");
        }

        var fileName = $"{SanitizeFileName(dto.Text)}.stl";
        return File(stream, "model/stl", fileName);
    }

    private static string ResolveFontPath(string fontFamilyOrPath) =>
        System.IO.File.Exists(fontFamilyOrPath)
            ? fontFamilyOrPath
            : Path.Combine(AppContext.BaseDirectory, "Fonts", fontFamilyOrPath);

    private static string SanitizeFileName(string text)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(text.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "tag" : cleaned;
    }
}
