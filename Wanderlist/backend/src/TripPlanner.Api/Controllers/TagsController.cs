using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripPlanner.Api.Dtos;
using TripPlanner.Domain.Entities;
using TripPlanner.Infrastructure.Persistence;

namespace TripPlanner.Api.Controllers;

[ApiController]
[Route("api/tags")]
public class TagsController : ControllerBase
{
    private readonly TripPlannerDbContext _db;
    public TagsController(TripPlannerDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<TagDto>>> GetAll()
        => await _db.Tags.OrderBy(t => t.Name).Select(t => new TagDto(t.Id, t.Name, t.Color)).ToListAsync();

    [HttpPost]
    public async Task<ActionResult<TagDto>> Create(TagCreateDto dto)
    {
        var existing = await _db.Tags.FirstOrDefaultAsync(t => t.Name == dto.Name);
        if (existing != null) return new TagDto(existing.Id, existing.Name, existing.Color);

        var tag = new Tag { Name = dto.Name, Color = string.IsNullOrWhiteSpace(dto.Color) ? "#64748b" : dto.Color };
        _db.Tags.Add(tag);
        await _db.SaveChangesAsync();
        return new TagDto(tag.Id, tag.Name, tag.Color);
    }

    [HttpGet("usage")]
    public async Task<ActionResult<List<TagUsageDto>>> GetUsage()
    {
        var tags = await _db.Tags.OrderBy(t => t.Name).ToListAsync();
        var counts = await _db.TaggedItems.GroupBy(ti => ti.TagId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        var countMap = counts.ToDictionary(c => c.Key, c => c.Count);
        return tags.Select(t => new TagUsageDto(t.Id, t.Name, t.Color, countMap.GetValueOrDefault(t.Id, 0))).ToList();
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TagDto>> Update(Guid id, TagUpdateDto dto)
    {
        var tag = await _db.Tags.FindAsync(id);
        if (tag == null) return NotFound();
        tag.Name = dto.Name; tag.Color = dto.Color;
        await _db.SaveChangesAsync();
        return new TagDto(tag.Id, tag.Name, tag.Color);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var tag = await _db.Tags.FindAsync(id);
        if (tag == null) return NotFound();
        _db.Tags.Remove(tag);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, AssignTagDto dto)
    {
        var tag = await _db.Tags.FindAsync(id);
        if (tag == null) return NotFound();
        var exists = await _db.TaggedItems.AnyAsync(ti => ti.TagId == id && ti.EntityType == dto.EntityType && ti.EntityId == dto.EntityId);
        if (!exists)
        {
            _db.TaggedItems.Add(new TaggedItem { TagId = id, EntityType = dto.EntityType, EntityId = dto.EntityId });
            await _db.SaveChangesAsync();
        }
        return NoContent();
    }

    [HttpPost("{id:guid}/unassign")]
    public async Task<IActionResult> Unassign(Guid id, AssignTagDto dto)
    {
        var item = await _db.TaggedItems.FirstOrDefaultAsync(ti => ti.TagId == id && ti.EntityType == dto.EntityType && ti.EntityId == dto.EntityId);
        if (item != null)
        {
            _db.TaggedItems.Remove(item);
            await _db.SaveChangesAsync();
        }
        return NoContent();
    }
}
