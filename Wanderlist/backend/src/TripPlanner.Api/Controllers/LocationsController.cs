using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripPlanner.Api.Dtos;
using TripPlanner.Api.Services;
using TripPlanner.Domain.Entities;
using TripPlanner.Domain.Enums;
using TripPlanner.Infrastructure.Persistence;

namespace TripPlanner.Api.Controllers;

[ApiController]
[Route("api/locations")]
public class LocationsController : ControllerBase
{
    private readonly TripPlannerDbContext _db;
    public LocationsController(TripPlannerDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<WishlistLocationDto>>> GetAll()
    {
        var locations = await _db.WishlistLocations.AsNoTracking().OrderByDescending(l => l.CreatedAt).ToListAsync();
        var tagMap = await TagHelper.GetTagsForManyAsync(_db, EntityType.WishlistLocation, locations.Select(l => l.Id));
        return locations.Select(l => ToDto(l, tagMap.GetValueOrDefault(l.Id, new()))).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WishlistLocationDto>> Get(Guid id)
    {
        var l = await _db.WishlistLocations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (l == null) return NotFound();
        var tags = await TagHelper.GetTagsForAsync(_db, EntityType.WishlistLocation, id);
        return ToDto(l, tags);
    }

    [HttpPost]
    public async Task<ActionResult<WishlistLocationDto>> Create(WishlistLocationCreateDto dto)
    {
        var location = new WishlistLocation { Name = dto.Name, Description = dto.Description, Country = dto.Country, Lat = dto.Lat, Lng = dto.Lng, Status = dto.Status ?? WishlistStatus.Idea };
        _db.WishlistLocations.Add(location);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = location.Id }, ToDto(location, new()));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WishlistLocationDto>> Update(Guid id, WishlistLocationUpdateDto dto)
    {
        var location = await _db.WishlistLocations.FindAsync(id);
        if (location == null) return NotFound();
        location.Name = dto.Name; location.Description = dto.Description; location.Country = dto.Country; location.Lat = dto.Lat; location.Lng = dto.Lng; location.Status = dto.Status;
        await _db.SaveChangesAsync();
        var tags = await TagHelper.GetTagsForAsync(_db, EntityType.WishlistLocation, id);
        return ToDto(location, tags);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var location = await _db.WishlistLocations.FindAsync(id);
        if (location == null) return NotFound();
        _db.WishlistLocations.Remove(location);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Notes
    [HttpGet("{id:guid}/notes")]
    public async Task<ActionResult<List<WishlistNoteDto>>> GetNotes(Guid id)
        => await _db.WishlistNotes.Where(n => n.WishlistLocationId == id).OrderByDescending(n => n.CreatedAt)
            .Select(n => new WishlistNoteDto(n.Id, n.Text, n.CreatedAt)).ToListAsync();

    [HttpPost("{id:guid}/notes")]
    public async Task<ActionResult<WishlistNoteDto>> AddNote(Guid id, WishlistNoteCreateDto dto)
    {
        var note = new WishlistNote { WishlistLocationId = id, Text = dto.Text };
        _db.WishlistNotes.Add(note);
        await _db.SaveChangesAsync();
        return new WishlistNoteDto(note.Id, note.Text, note.CreatedAt);
    }

    [HttpDelete("notes/{noteId:guid}")]
    public async Task<IActionResult> DeleteNote(Guid noteId)
    {
        var note = await _db.WishlistNotes.FindAsync(noteId);
        if (note == null) return NotFound();
        _db.WishlistNotes.Remove(note);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Links
    [HttpGet("{id:guid}/links")]
    public async Task<ActionResult<List<WishlistLinkDto>>> GetLinks(Guid id)
        => await _db.WishlistLinks.Where(n => n.WishlistLocationId == id)
            .Select(n => new WishlistLinkDto(n.Id, n.Url, n.Label)).ToListAsync();

    [HttpPost("{id:guid}/links")]
    public async Task<ActionResult<WishlistLinkDto>> AddLink(Guid id, WishlistLinkCreateDto dto)
    {
        var link = new WishlistLink { WishlistLocationId = id, Url = dto.Url, Label = dto.Label };
        _db.WishlistLinks.Add(link);
        await _db.SaveChangesAsync();
        return new WishlistLinkDto(link.Id, link.Url, link.Label);
    }

    [HttpDelete("links/{linkId:guid}")]
    public async Task<IActionResult> DeleteLink(Guid linkId)
    {
        var link = await _db.WishlistLinks.FindAsync(linkId);
        if (link == null) return NotFound();
        _db.WishlistLinks.Remove(link);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Plan
    [HttpGet("{id:guid}/plan")]
    public async Task<ActionResult<WishlistPlanDto>> GetPlan(Guid id)
    {
        var plan = await _db.WishlistPlans.FirstOrDefaultAsync(p => p.WishlistLocationId == id);
        if (plan == null) return new WishlistPlanDto(null, null, null, null, null);
        return new WishlistPlanDto(plan.TransportDetails, plan.AccommodationDetails, plan.DateRangeStart, plan.DateRangeEnd, plan.FreeformText);
    }

    [HttpPut("{id:guid}/plan")]
    public async Task<ActionResult<WishlistPlanDto>> PutPlan(Guid id, WishlistPlanDto dto)
    {
        var plan = await _db.WishlistPlans.FirstOrDefaultAsync(p => p.WishlistLocationId == id);
        if (plan == null)
        {
            plan = new WishlistPlan { WishlistLocationId = id };
            _db.WishlistPlans.Add(plan);
        }
        plan.TransportDetails = dto.TransportDetails;
        plan.AccommodationDetails = dto.AccommodationDetails;
        plan.DateRangeStart = dto.DateRangeStart;
        plan.DateRangeEnd = dto.DateRangeEnd;
        plan.FreeformText = dto.FreeformText;
        await _db.SaveChangesAsync();
        return dto;
    }

    // Whiteboard
    [HttpGet("{id:guid}/whiteboard")]
    public async Task<ActionResult<WhiteboardDto>> GetWhiteboard(Guid id)
    {
        var wb = await _db.Whiteboards.FirstOrDefaultAsync(w => w.WishlistLocationId == id);
        return new WhiteboardDto(wb?.ContentJson ?? "{\"elements\":[]}");
    }

    [HttpPut("{id:guid}/whiteboard")]
    public async Task<ActionResult<WhiteboardDto>> PutWhiteboard(Guid id, WhiteboardDto dto)
    {
        var wb = await _db.Whiteboards.FirstOrDefaultAsync(w => w.WishlistLocationId == id);
        if (wb == null)
        {
            wb = new Whiteboard { WishlistLocationId = id, ContentJson = dto.ContentJson };
            _db.Whiteboards.Add(wb);
        }
        else
        {
            wb.ContentJson = dto.ContentJson;
        }
        await _db.SaveChangesAsync();
        return dto;
    }

    // Trip links
    [HttpGet("{id:guid}/trip-links")]
    public async Task<ActionResult<List<TripLinkDto>>> GetTripLinks(Guid id)
        => await _db.TripStops.Where(s => s.SourceLocationId == id)
            .Select(s => new TripLinkDto(s.TripId, s.Trip.Name, s.Id)).ToListAsync();

    private static WishlistLocationDto ToDto(WishlistLocation l, List<TagDto> tags) =>
        new(l.Id, l.Name, l.Description, l.Country, l.Lat, l.Lng, l.Status, l.CreatedAt, tags);
}
