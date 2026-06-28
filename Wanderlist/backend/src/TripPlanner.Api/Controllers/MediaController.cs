using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripPlanner.Api.Dtos;
using TripPlanner.Domain.Entities;
using TripPlanner.Domain.Enums;
using TripPlanner.Infrastructure.Persistence;

namespace TripPlanner.Api.Controllers;

[ApiController]
[Route("api/media")]
public class MediaController : ControllerBase
{
    private readonly TripPlannerDbContext _db;
    private readonly IWebHostEnvironment _env;
    public MediaController(TripPlannerDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    private static MediaItemDto ToDto(MediaItem m) =>
        new(m.Id, m.EntityType, m.EntityId, m.Kind, m.Url, m.Caption, m.CapturedAt, m.Lat, m.Lng, m.CreatedAt);

    [HttpGet]
    public async Task<ActionResult<List<MediaItemDto>>> GetForEntity([FromQuery] EntityType entityType, [FromQuery] Guid entityId)
        => await _db.MediaItems.Where(m => m.EntityType == entityType && m.EntityId == entityId)
            .OrderBy(m => m.CapturedAt ?? m.CreatedAt).Select(m => ToDto(m)).ToListAsync();

    [HttpPost]
    public async Task<ActionResult<MediaItemDto>> Create(MediaItemCreateDto dto)
    {
        var media = new MediaItem
        {
            EntityType = dto.EntityType,
            EntityId = dto.EntityId,
            Kind = dto.Kind,
            Url = dto.Url,
            Caption = dto.Caption,
            CapturedAt = dto.CapturedAt,
            Lat = dto.Lat,
            Lng = dto.Lng,
        };
        _db.MediaItems.Add(media);
        await _db.SaveChangesAsync();
        return ToDto(media);
    }

    /// <summary>Simple disk-based upload for dev; swap for blob storage in production.</summary>
    [HttpPost("upload")]
    [RequestSizeLimit(100_000_000)]
    public async Task<ActionResult<object>> Upload(IFormFile file)
    {
        if (file.Length == 0) return BadRequest("Empty file");
        var uploadsDir = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads");
        Directory.CreateDirectory(uploadsDir);
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var path = Path.Combine(uploadsDir, fileName);
        await using var stream = new FileStream(path, FileMode.Create);
        await file.CopyToAsync(stream);
        return new { url = $"/uploads/{fileName}" };
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var media = await _db.MediaItems.FindAsync(id);
        if (media == null) return NotFound();

        if (media.Url.StartsWith("/uploads/"))
        {
            var path = Path.Combine(_env.ContentRootPath, "wwwroot", media.Url.TrimStart('/'));
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }

        _db.MediaItems.Remove(media);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
