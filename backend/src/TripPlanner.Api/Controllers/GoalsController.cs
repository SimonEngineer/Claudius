using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripPlanner.Api.Dtos;
using TripPlanner.Api.Services;
using TripPlanner.Domain.Entities;
using TripPlanner.Domain.Enums;
using TripPlanner.Infrastructure.Persistence;

namespace TripPlanner.Api.Controllers;

[ApiController]
[Route("api/goals")]
public class GoalsController : ControllerBase
{
    private readonly TripPlannerDbContext _db;
    public GoalsController(TripPlannerDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<GoalDto>>> GetAll()
    {
        var goals = await _db.Goals.Include(g => g.FieldDefinitions).Include(g => g.Items).AsNoTracking().OrderByDescending(g => g.CreatedAt).ToListAsync();
        var tagMap = await TagHelper.GetTagsForManyAsync(_db, EntityType.Goal, goals.Select(g => g.Id));
        return goals.Select(g => ToDto(g, tagMap.GetValueOrDefault(g.Id, new()))).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GoalDto>> Get(Guid id)
    {
        var g = await _db.Goals.Include(x => x.FieldDefinitions).Include(x => x.Items).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (g == null) return NotFound();
        var tags = await TagHelper.GetTagsForAsync(_db, EntityType.Goal, id);
        return ToDto(g, tags);
    }

    [HttpPost]
    public async Task<ActionResult<GoalDto>> Create(GoalCreateDto dto)
    {
        var goal = new Goal
        {
            Name = dto.Name,
            Description = dto.Description,
            Icon = dto.Icon,
            Kind = dto.Kind,
            LinkedTripId = dto.LinkedTripId,
            FieldDefinitions = dto.FieldDefinitions.Select(f => new GoalFieldDefinition
            {
                Key = f.Key,
                Label = f.Label,
                FieldType = f.FieldType,
                SortOrder = f.SortOrder,
            }).ToList(),
        };
        _db.Goals.Add(goal);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = goal.Id }, ToDto(goal, new()));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<GoalDto>> Update(Guid id, GoalUpdateDto dto)
    {
        var goal = await _db.Goals.Include(g => g.FieldDefinitions).Include(g => g.Items).FirstOrDefaultAsync(g => g.Id == id);
        if (goal == null) return NotFound();
        goal.Name = dto.Name; goal.Description = dto.Description; goal.Icon = dto.Icon; goal.LinkedTripId = dto.LinkedTripId;
        await _db.SaveChangesAsync();
        var tags = await TagHelper.GetTagsForAsync(_db, EntityType.Goal, id);
        return ToDto(goal, tags);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var goal = await _db.Goals.FindAsync(id);
        if (goal == null) return NotFound();
        _db.Goals.Remove(goal);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/fields")]
    public async Task<ActionResult<GoalFieldDefinitionDto>> AddField(Guid id, GoalFieldDefinitionCreateDto dto)
    {
        var goal = await _db.Goals.FindAsync(id);
        if (goal == null) return NotFound();
        var field = new GoalFieldDefinition { GoalId = id, Key = dto.Key, Label = dto.Label, FieldType = dto.FieldType, SortOrder = dto.SortOrder };
        _db.GoalFieldDefinitions.Add(field);
        await _db.SaveChangesAsync();
        return new GoalFieldDefinitionDto(field.Id, field.Key, field.Label, field.FieldType, field.SortOrder);
    }

    [HttpDelete("fields/{fieldId:guid}")]
    public async Task<IActionResult> DeleteField(Guid fieldId)
    {
        var field = await _db.GoalFieldDefinitions.FindAsync(fieldId);
        if (field == null) return NotFound();
        _db.GoalFieldDefinitions.Remove(field);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Items
    [HttpGet("{id:guid}/items")]
    public async Task<ActionResult<List<GoalItemDto>>> GetItems(Guid id)
    {
        var items = await _db.GoalItems.Where(i => i.GoalId == id)
            .Include(i => i.FieldValues).Include(i => i.Notes).Include(i => i.Links)
            .AsNoTracking().OrderBy(i => i.CreatedAt).ToListAsync();
        var tagMap = await TagHelper.GetTagsForManyAsync(_db, EntityType.GoalItem, items.Select(i => i.Id));
        return items.Select(i => ToItemDto(i, tagMap.GetValueOrDefault(i.Id, new()))).ToList();
    }

    [HttpPost("{id:guid}/items")]
    public async Task<ActionResult<GoalItemDto>> AddItem(Guid id, GoalItemCreateDto dto)
    {
        var goal = await _db.Goals.FindAsync(id);
        if (goal == null) return NotFound();
        var item = new GoalItem
        {
            GoalId = id,
            Name = dto.Name,
            Lat = dto.Lat,
            Lng = dto.Lng,
            FieldValues = dto.FieldValues.Select(v => new GoalItemFieldValue { GoalFieldDefinitionId = v.GoalFieldDefinitionId, Value = v.Value }).ToList(),
        };
        _db.GoalItems.Add(item);
        await _db.SaveChangesAsync();
        return ToItemDto(item, new());
    }

    [HttpPut("items/{itemId:guid}")]
    public async Task<ActionResult<GoalItemDto>> UpdateItem(Guid itemId, GoalItemUpdateDto dto)
    {
        var item = await _db.GoalItems.Include(i => i.FieldValues).Include(i => i.Notes).Include(i => i.Links).FirstOrDefaultAsync(i => i.Id == itemId);
        if (item == null) return NotFound();
        item.Name = dto.Name; item.Lat = dto.Lat; item.Lng = dto.Lng;

        foreach (var v in dto.FieldValues)
        {
            var existing = item.FieldValues.FirstOrDefault(f => f.GoalFieldDefinitionId == v.GoalFieldDefinitionId);
            if (existing != null) existing.Value = v.Value;
            else item.FieldValues.Add(new GoalItemFieldValue { GoalItemId = itemId, GoalFieldDefinitionId = v.GoalFieldDefinitionId, Value = v.Value });
        }
        await _db.SaveChangesAsync();
        var tags = await TagHelper.GetTagsForAsync(_db, EntityType.GoalItem, itemId);
        return ToItemDto(item, tags);
    }

    [HttpPost("items/{itemId:guid}/complete")]
    public async Task<ActionResult<GoalItemDto>> Complete(Guid itemId, [FromQuery] bool completed = true)
    {
        var item = await _db.GoalItems.Include(i => i.FieldValues).Include(i => i.Notes).Include(i => i.Links).FirstOrDefaultAsync(i => i.Id == itemId);
        if (item == null) return NotFound();
        item.IsCompleted = completed;
        item.CompletedAt = completed ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync();
        var tags = await TagHelper.GetTagsForAsync(_db, EntityType.GoalItem, itemId);
        return ToItemDto(item, tags);
    }

    [HttpDelete("items/{itemId:guid}")]
    public async Task<IActionResult> DeleteItem(Guid itemId)
    {
        var item = await _db.GoalItems.FindAsync(itemId);
        if (item == null) return NotFound();
        _db.GoalItems.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("items/{itemId:guid}/notes")]
    public async Task<ActionResult<GoalItemNoteDto>> AddItemNote(Guid itemId, GoalItemNoteCreateDto dto)
    {
        var note = new GoalItemNote { GoalItemId = itemId, Text = dto.Text };
        _db.GoalItemNotes.Add(note);
        await _db.SaveChangesAsync();
        return new GoalItemNoteDto(note.Id, note.Text, note.CreatedAt);
    }

    [HttpPost("items/{itemId:guid}/links")]
    public async Task<ActionResult<GoalItemLinkDto>> AddItemLink(Guid itemId, GoalItemLinkCreateDto dto)
    {
        var link = new GoalItemLink { GoalItemId = itemId, Url = dto.Url, Label = dto.Label };
        _db.GoalItemLinks.Add(link);
        await _db.SaveChangesAsync();
        return new GoalItemLinkDto(link.Id, link.Url, link.Label);
    }

    private static GoalDto ToDto(Goal g, List<TagDto> tags) => new(
        g.Id, g.Name, g.Description, g.Icon, g.Kind, g.LinkedTripId,
        g.FieldDefinitions.OrderBy(f => f.SortOrder).Select(f => new GoalFieldDefinitionDto(f.Id, f.Key, f.Label, f.FieldType, f.SortOrder)).ToList(),
        g.Items.Count, g.Items.Count(i => i.IsCompleted), tags);

    private static GoalItemDto ToItemDto(GoalItem i, List<TagDto> tags) => new(
        i.Id, i.GoalId, i.Name, i.Lat, i.Lng, i.IsCompleted, i.CompletedAt,
        i.FieldValues.Select(v => new GoalItemFieldValueDto(v.GoalFieldDefinitionId, v.Value)).ToList(),
        i.Notes.OrderByDescending(n => n.CreatedAt).Select(n => new GoalItemNoteDto(n.Id, n.Text, n.CreatedAt)).ToList(),
        i.Links.Select(l => new GoalItemLinkDto(l.Id, l.Url, l.Label)).ToList(),
        tags);
}
