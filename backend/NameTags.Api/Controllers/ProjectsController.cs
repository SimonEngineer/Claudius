using System.IO.Compression;
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

    /// <summary>
    /// Streams every name's STL into a single zip without buffering the archive or any
    /// individual mesh in memory beyond one entry at a time -- important at the ~100-name
    /// scale this is meant for. Pass nameIds to export a subset; omit it to export all names.
    /// </summary>
    [HttpGet("{id:int}/export.zip")]
    public async Task<IActionResult> ExportZip(int id, [FromQuery] string? nameIds)
    {
        var project = await db.TagProjects.Include(p => p.Names).Include(p => p.MountingHoles).FirstOrDefaultAsync(p => p.Id == id);
        if (project is null) return NotFound();

        var names = project.Names.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(nameIds))
        {
            var requestedIds = nameIds.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToHashSet();
            names = names.Where(n => requestedIds.Contains(n.Id));
        }
        var nameList = names.ToList();
        if (nameList.Count == 0) return NotFound();

        Response.ContentType = "application/zip";
        Response.Headers.ContentDisposition = $"attachment; filename=\"{SanitizeFileName(project.Name)}.zip\"";

        using var archive = new ZipArchive(Response.BodyWriter.AsStream(), ZipArchiveMode.Create);
        var usedFileNames = new HashSet<string>();
        foreach (var name in nameList)
        {
            var fileName = $"{SanitizeFileName(name.Text)}.stl";
            while (!usedFileNames.Add(fileName))
            {
                fileName = $"{SanitizeFileName(name.Text)}-{name.Id}.stl";
            }

            var entry = archive.CreateEntry(fileName, CompressionLevel.Fastest);
            await using var entryStream = entry.Open();
            var mesh = _generationService.GenerateTagMesh(BuildRequest(project, name));
            StlWriter.WriteBinary(entryStream, mesh);
        }

        return new EmptyResult();
    }

    /// <summary>
    /// One pre-arranged STL per name: the project's standing tag plus a wine-glass charm and
    /// a clothes clip, laid out side by side flat on the bed -- drag this straight into a
    /// slicer with nothing left to rearrange.
    /// </summary>
    [HttpGet("{id:int}/names/{nameId:int}/pack.stl")]
    public async Task<IActionResult> DownloadPack(int id, int nameId)
    {
        var project = await db.TagProjects.Include(p => p.Names).Include(p => p.MountingHoles).FirstOrDefaultAsync(p => p.Id == id);
        if (project is null) return NotFound();

        var name = project.Names.FirstOrDefault(n => n.Id == nameId);
        if (name is null) return NotFound();

        var mesh = PackGenerationService.BuildPackMesh(_generationService, BuildRequest(project, name), BuildPackSettings(project));
        var stream = new MemoryStream();
        StlWriter.WriteBinary(stream, mesh);
        stream.Position = 0;

        return File(stream, "model/stl", $"{SanitizeFileName(name.Text)}-pack.stl");
    }

    /// <summary>Same streaming-zip pattern as ExportZip, but each entry is a full 3-variant pack.</summary>
    [HttpGet("{id:int}/packs.zip")]
    public async Task<IActionResult> ExportPacksZip(int id)
    {
        var project = await db.TagProjects.Include(p => p.Names).Include(p => p.MountingHoles).FirstOrDefaultAsync(p => p.Id == id);
        if (project is null) return NotFound();
        if (project.Names.Count == 0) return NotFound();

        Response.ContentType = "application/zip";
        Response.Headers.ContentDisposition = $"attachment; filename=\"{SanitizeFileName(project.Name)}-packs.zip\"";

        var packSettings = BuildPackSettings(project);
        using var archive = new ZipArchive(Response.BodyWriter.AsStream(), ZipArchiveMode.Create);
        var usedFileNames = new HashSet<string>();
        foreach (var name in project.Names)
        {
            var fileName = $"{SanitizeFileName(name.Text)}-pack.stl";
            while (!usedFileNames.Add(fileName))
            {
                fileName = $"{SanitizeFileName(name.Text)}-pack-{name.Id}.stl";
            }

            var entry = archive.CreateEntry(fileName, CompressionLevel.Fastest);
            await using var entryStream = entry.Open();
            var mesh = PackGenerationService.BuildPackMesh(_generationService, BuildRequest(project, name), packSettings);
            StlWriter.WriteBinary(entryStream, mesh);
        }

        return new EmptyResult();
    }

    private async Task<IActionResult> GenerateStl(int id, int nameId, bool asAttachment)
    {
        var project = await db.TagProjects.Include(p => p.Names).Include(p => p.MountingHoles).FirstOrDefaultAsync(p => p.Id == id);
        if (project is null) return NotFound();

        var name = project.Names.FirstOrDefault(n => n.Id == nameId);
        if (name is null) return NotFound();

        var mesh = _generationService.GenerateTagMesh(BuildRequest(project, name));
        var stream = new MemoryStream();
        StlWriter.WriteBinary(stream, mesh);
        stream.Position = 0;

        return asAttachment ? File(stream, "model/stl", $"{name.Text}.stl") : File(stream, "model/stl");
    }

    private static TagGenerationRequest BuildRequest(TagProject project, TagName name) => new()
    {
        Text = name.Text,
        ShapeType = project.ShapeType,
        FontFamilyOrPath = ResolveFontPath(project.FontFamilyOrPath),
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
        BevelMm = project.BevelMm,
        CustomSvgBytes = project.CustomSvgBytes,
        ShapeParams = new ShapeParamsDto(
            project.CornerRadiusMm, project.StarPoints, project.StarInnerRadiusRatio, project.CurveSegments
        ).ToShapeParams(),
        MountingHoles = project.MountingHoles
            .Select(h => new NameTags.Core.Geometry.MountingHole(h.OffsetXMm, h.OffsetYMm, h.DiameterMm))
            .ToList(),
    };

    private static PackSettings BuildPackSettings(TagProject project) => new(
        project.CharmPlateWidthMm,
        project.CharmPlateHeightMm,
        project.CharmHookOuterRadiusMm,
        project.CharmHookBandThicknessMm,
        project.ClipPlateWidthMm,
        project.ClipPlateHeightMm,
        project.ClipArmLengthMm,
        project.ClipGapMm,
        project.ClipArmThicknessMm);

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
        project.BevelMm = dto.BevelMm;
        project.CharmPlateWidthMm = dto.CharmPlateWidthMm;
        project.CharmPlateHeightMm = dto.CharmPlateHeightMm;
        project.CharmHookOuterRadiusMm = dto.CharmHookOuterRadiusMm;
        project.CharmHookBandThicknessMm = dto.CharmHookBandThicknessMm;
        project.ClipPlateWidthMm = dto.ClipPlateWidthMm;
        project.ClipPlateHeightMm = dto.ClipPlateHeightMm;
        project.ClipArmLengthMm = dto.ClipArmLengthMm;
        project.ClipGapMm = dto.ClipGapMm;
        project.ClipArmThicknessMm = dto.ClipArmThicknessMm;
        project.MountingHoles = (dto.MountingHoles ?? new List<SaveMountingHoleDto>())
            .Select(h => new TagMountingHole { OffsetXMm = h.OffsetXMm, OffsetYMm = h.OffsetYMm, DiameterMm = h.DiameterMm })
            .ToList();
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
