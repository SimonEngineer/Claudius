using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NameTags.Api.Dtos;
using NameTags.Api.Validation;
using NameTags.Core.Export;
using NameTags.Core.Pipeline;
using NameTags.Data;
using NameTags.Data.Entities;

namespace NameTags.Api.Controllers;

/// <summary>
/// CRUD for saved tag designs (TagProject + its TagNames). Generation params are
/// persisted here; the mesh is still never stored -- preview/download below regenerate
/// fresh via ModelGenerationService from whatever is currently saved.
/// </summary>
[ApiController]
[Route("api/projects")]
public class ProjectsController(NameTagsDbContext db) : ControllerBase
{
    private readonly ModelGenerationService _generationService = new();

    [HttpGet]
    public async Task<ActionResult<List<ProjectSummaryDto>>> List()
    {
        var summaries = await db.TagProjects
            .Select(p => new ProjectSummaryDto(p.Id, p.Name, p.ShapeType, p.Names.Count, p.CreatedAt))
            .ToListAsync();
        return summaries;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProjectDetailDto>> Get(int id)
    {
        var project = await db.TagProjects.Include(p => p.Names).Include(p => p.MountingHoles).FirstOrDefaultAsync(p => p.Id == id);
        if (project is null) return NotFound();
        return ProjectDetailDto.FromEntity(project);
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDetailDto>> Create([FromBody] SaveProjectDto dto)
    {
        var project = new TagProject();
        ApplySaveDto(project, dto);
        db.TagProjects.Add(project);
        await db.SaveChangesAsync();
        return ProjectDetailDto.FromEntity(project);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProjectDetailDto>> Update(int id, [FromBody] SaveProjectDto dto)
    {
        var project = await db.TagProjects.Include(p => p.Names).Include(p => p.MountingHoles).FirstOrDefaultAsync(p => p.Id == id);
        if (project is null) return NotFound();
        db.TagMountingHoles.RemoveRange(project.MountingHoles);
        ApplySaveDto(project, dto);
        await db.SaveChangesAsync();
        return ProjectDetailDto.FromEntity(project);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var project = await db.TagProjects.FindAsync(id);
        if (project is null) return NotFound();
        db.TagProjects.Remove(project);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Replaces the full name list for a project -- the editor UX edits names as one bulk text block, so there is no need for granular per-name endpoints yet.</summary>
    [HttpPut("{id:int}/names")]
    public async Task<ActionResult<List<TagNameDto>>> ReplaceNames(int id, [FromBody] List<string> names)
    {
        var project = await db.TagProjects.Include(p => p.Names).FirstOrDefaultAsync(p => p.Id == id);
        if (project is null) return NotFound();

        foreach (var text in names)
        {
            var error = TagTextValidator.Validate(text);
            if (error is not null) return BadRequest(new { error });
        }

        db.TagNames.RemoveRange(project.Names);
        project.Names = names
            .Select((text, index) => new TagName { TagProjectId = id, Text = text, SortOrder = index })
            .ToList();
        await db.SaveChangesAsync();

        return project.Names.Select(n => new TagNameDto(n.Id, n.Text, n.SortOrder, n.TextDepthMmOverride)).ToList();
    }

    /// <summary>Sets per-name overrides (currently just text depth) on top of the project's shared params.</summary>
    [HttpPut("{id:int}/names/{nameId:int}/override")]
    public async Task<ActionResult<TagNameDto>> SetNameOverride(int id, int nameId, [FromBody] UpdateNameOverrideDto dto)
    {
        var name = await db.TagNames.FirstOrDefaultAsync(n => n.Id == nameId && n.TagProjectId == id);
        if (name is null) return NotFound();

        name.TextDepthMmOverride = dto.TextDepthMmOverride;
        await db.SaveChangesAsync();

        return new TagNameDto(name.Id, name.Text, name.SortOrder, name.TextDepthMmOverride);
    }

    [HttpGet("{id:int}/names/{nameId:int}/preview.stl")]
    public Task<IActionResult> Preview(int id, int nameId) => GenerateStl(id, nameId, asAttachment: false);

    [HttpGet("{id:int}/names/{nameId:int}/download.stl")]
    public Task<IActionResult> Download(int id, int nameId) => GenerateStl(id, nameId, asAttachment: true);

    private async Task<IActionResult> GenerateStl(int id, int nameId, bool asAttachment)
    {
        var project = await db.TagProjects.Include(p => p.Names).FirstOrDefaultAsync(p => p.Id == id);
        if (project is null) return NotFound();

        var name = project.Names.FirstOrDefault(n => n.Id == nameId);
        if (name is null) return NotFound();

        var fontPath = ResolveFontPath(project.FontFamilyOrPath);
        var request = new TagGenerationRequest
        {
            Text = name.Text,
            ShapeType = project.ShapeType,
            FontFamilyOrPath = fontPath,
            PlateWidthMm = project.PlateWidthMm,
            PlateHeightMm = project.PlateHeightMm,
            PlateThicknessMm = project.PlateThicknessMm,
            TextDepthMm = name.TextDepthMmOverride ?? project.TextDepthMm,
            TextMarginLeftMm = project.TextMarginLeftMm,
            TextMarginRightMm = project.TextMarginRightMm,
            TextMarginTopMm = project.TextMarginTopMm,
            TextMarginBottomMm = project.TextMarginBottomMm,
            TextHorizontalAlign = project.TextHorizontalAlign,
            TextVerticalAlign = project.TextVerticalAlign,
            CustomSvgBytes = project.CustomSvgBytes,
            ShapeParams = new ShapeParamsDto(
                project.CornerRadiusMm, project.StarPoints, project.StarInnerRadiusRatio, project.CurveSegments
            ).ToShapeParams(),
            MountingHoles = project.MountingHoles
                .Select(h => new NameTags.Core.Geometry.MountingHole(h.OffsetXMm, h.OffsetYMm, h.DiameterMm))
                .ToList(),
        };

        var mesh = _generationService.GenerateTagMesh(request);
        var stream = new MemoryStream();
        StlWriter.WriteBinary(stream, mesh);
        stream.Position = 0;

        return asAttachment ? File(stream, "model/stl", $"{name.Text}.stl") : File(stream, "model/stl");
    }

    private static void ApplySaveDto(TagProject project, SaveProjectDto dto)
    {
        var shapeParams = dto.ShapeParams ?? new ShapeParamsDto();
        project.Name = dto.Name;
        project.ShapeType = dto.ShapeType;
        project.CornerRadiusMm = shapeParams.CornerRadiusMm;
        project.StarPoints = shapeParams.StarPoints;
        project.StarInnerRadiusRatio = shapeParams.StarInnerRadiusRatio;
        project.CurveSegments = shapeParams.CurveSegments;
        project.CustomSvgBytes = dto.CustomSvgBase64 is null ? null : Convert.FromBase64String(dto.CustomSvgBase64);
        project.FontFamilyOrPath = dto.FontFamilyOrPath;
        project.PlateWidthMm = dto.PlateWidthMm;
        project.PlateHeightMm = dto.PlateHeightMm;
        project.PlateThicknessMm = dto.PlateThicknessMm;
        project.TextDepthMm = dto.TextDepthMm;
        project.TextMarginLeftMm = dto.TextMarginLeftMm;
        project.TextMarginRightMm = dto.TextMarginRightMm;
        project.TextMarginTopMm = dto.TextMarginTopMm;
        project.TextMarginBottomMm = dto.TextMarginBottomMm;
        project.TextHorizontalAlign = dto.TextHorizontalAlign;
        project.TextVerticalAlign = dto.TextVerticalAlign;
        project.MountingHoles = (dto.MountingHoles ?? new List<SaveMountingHoleDto>())
            .Select(h => new TagMountingHole { OffsetXMm = h.OffsetXMm, OffsetYMm = h.OffsetYMm, DiameterMm = h.DiameterMm })
            .ToList();
    }

    private static string ResolveFontPath(string fontFamilyOrPath) =>
        System.IO.File.Exists(fontFamilyOrPath)
            ? fontFamilyOrPath
            : Path.Combine(AppContext.BaseDirectory, "Fonts", fontFamilyOrPath);
}
