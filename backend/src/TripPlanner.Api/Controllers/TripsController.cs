using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripPlanner.Api.Dtos;
using TripPlanner.Api.Services;
using TripPlanner.Domain.Entities;
using TripPlanner.Domain.Enums;
using TripPlanner.Infrastructure.Persistence;

namespace TripPlanner.Api.Controllers;

[ApiController]
[Route("api/trips")]
public class TripsController : ControllerBase
{
    private readonly TripPlannerDbContext _db;
    public TripsController(TripPlannerDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<TripDto>>> GetAll()
    {
        var trips = await _db.Trips.AsNoTracking().OrderByDescending(t => t.StartDate).ToListAsync();
        var tagMap = await TagHelper.GetTagsForManyAsync(_db, EntityType.Trip, trips.Select(t => t.Id));
        return trips.Select(t => ToDto(t, tagMap.GetValueOrDefault(t.Id, new()))).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TripDto>> Get(Guid id)
    {
        var t = await _db.Trips.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();
        var tags = await TagHelper.GetTagsForAsync(_db, EntityType.Trip, id);
        return ToDto(t, tags);
    }

    [HttpPost]
    public async Task<ActionResult<TripDto>> Create(TripCreateDto dto)
    {
        var trip = new Trip { Name = dto.Name, Description = dto.Description, StartDate = dto.StartDate, EndDate = dto.EndDate };
        _db.Trips.Add(trip);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = trip.Id }, ToDto(trip, new()));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TripDto>> Update(Guid id, TripUpdateDto dto)
    {
        var trip = await _db.Trips.FindAsync(id);
        if (trip == null) return NotFound();
        trip.Name = dto.Name; trip.Description = dto.Description; trip.StartDate = dto.StartDate; trip.EndDate = dto.EndDate; trip.Status = dto.Status;
        await _db.SaveChangesAsync();
        var tags = await TagHelper.GetTagsForAsync(_db, EntityType.Trip, id);
        return ToDto(trip, tags);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var trip = await _db.Trips.FindAsync(id);
        if (trip == null) return NotFound();
        _db.Trips.Remove(trip);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Stops
    [HttpGet("{id:guid}/stops")]
    public async Task<ActionResult<List<TripStopDto>>> GetStops(Guid id)
        => await _db.TripStops.Where(s => s.TripId == id).OrderBy(s => s.SortOrder)
            .Select(s => new TripStopDto(s.Id, s.Name, s.Lat, s.Lng, s.ArriveDate, s.DepartDate, s.SortOrder, s.IsStart, s.IsEnd, s.Notes)).ToListAsync();

    [HttpPost("{id:guid}/stops")]
    public async Task<ActionResult<TripStopDto>> AddStop(Guid id, TripStopCreateDto dto)
    {
        var trip = await _db.Trips.FindAsync(id);
        if (trip == null) return NotFound();
        var stop = new TripStop
        {
            TripId = id, Name = dto.Name, Lat = dto.Lat, Lng = dto.Lng,
            ArriveDate = dto.ArriveDate, DepartDate = dto.DepartDate, SortOrder = dto.SortOrder,
            IsStart = dto.IsStart, IsEnd = dto.IsEnd, Notes = dto.Notes,
        };
        _db.TripStops.Add(stop);
        await _db.SaveChangesAsync();
        return new TripStopDto(stop.Id, stop.Name, stop.Lat, stop.Lng, stop.ArriveDate, stop.DepartDate, stop.SortOrder, stop.IsStart, stop.IsEnd, stop.Notes);
    }

    [HttpPut("stops/{stopId:guid}")]
    public async Task<ActionResult<TripStopDto>> UpdateStop(Guid stopId, TripStopCreateDto dto)
    {
        var stop = await _db.TripStops.FindAsync(stopId);
        if (stop == null) return NotFound();
        stop.Name = dto.Name; stop.Lat = dto.Lat; stop.Lng = dto.Lng; stop.ArriveDate = dto.ArriveDate;
        stop.DepartDate = dto.DepartDate; stop.SortOrder = dto.SortOrder; stop.IsStart = dto.IsStart; stop.IsEnd = dto.IsEnd; stop.Notes = dto.Notes;
        await _db.SaveChangesAsync();
        return new TripStopDto(stop.Id, stop.Name, stop.Lat, stop.Lng, stop.ArriveDate, stop.DepartDate, stop.SortOrder, stop.IsStart, stop.IsEnd, stop.Notes);
    }

    [HttpDelete("stops/{stopId:guid}")]
    public async Task<IActionResult> DeleteStop(Guid stopId)
    {
        var stop = await _db.TripStops.FindAsync(stopId);
        if (stop == null) return NotFound();
        _db.TripStops.Remove(stop);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Bookings
    [HttpGet("{id:guid}/bookings")]
    public async Task<ActionResult<List<BookingDto>>> GetBookings(Guid id)
        => await _db.Bookings.Where(b => b.TripId == id).OrderBy(b => b.StartAt)
            .Select(b => new BookingDto(b.Id, b.Type, b.Title, b.ConfirmationNumber, b.StartAt, b.EndAt, b.Lat, b.Lng, b.DetailsJson)).ToListAsync();

    [HttpPost("{id:guid}/bookings")]
    public async Task<ActionResult<BookingDto>> AddBooking(Guid id, BookingCreateDto dto)
    {
        var trip = await _db.Trips.FindAsync(id);
        if (trip == null) return NotFound();
        var booking = new Booking
        {
            TripId = id, Type = dto.Type, Title = dto.Title, ConfirmationNumber = dto.ConfirmationNumber,
            StartAt = dto.StartAt, EndAt = dto.EndAt, Lat = dto.Lat, Lng = dto.Lng, DetailsJson = dto.DetailsJson,
        };
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();
        return new BookingDto(booking.Id, booking.Type, booking.Title, booking.ConfirmationNumber, booking.StartAt, booking.EndAt, booking.Lat, booking.Lng, booking.DetailsJson);
    }

    [HttpPut("bookings/{bookingId:guid}")]
    public async Task<ActionResult<BookingDto>> UpdateBooking(Guid bookingId, BookingCreateDto dto)
    {
        var booking = await _db.Bookings.FindAsync(bookingId);
        if (booking == null) return NotFound();
        booking.Type = dto.Type; booking.Title = dto.Title; booking.ConfirmationNumber = dto.ConfirmationNumber;
        booking.StartAt = dto.StartAt; booking.EndAt = dto.EndAt; booking.Lat = dto.Lat; booking.Lng = dto.Lng; booking.DetailsJson = dto.DetailsJson;
        await _db.SaveChangesAsync();
        return new BookingDto(booking.Id, booking.Type, booking.Title, booking.ConfirmationNumber, booking.StartAt, booking.EndAt, booking.Lat, booking.Lng, booking.DetailsJson);
    }

    [HttpDelete("bookings/{bookingId:guid}")]
    public async Task<IActionResult> DeleteBooking(Guid bookingId)
    {
        var booking = await _db.Bookings.FindAsync(bookingId);
        if (booking == null) return NotFound();
        _db.Bookings.Remove(booking);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Timeline
    [HttpGet("{id:guid}/timeline")]
    public async Task<ActionResult<List<TimelineEntryDto>>> GetTimeline(Guid id)
        => await _db.TimelineEntries.Where(e => e.TripId == id).OrderBy(e => e.CapturedAt)
            .Select(e => new TimelineEntryDto(e.Id, e.Type, e.Content, e.Lat, e.Lng, e.CapturedAt, e.NearestStopId)).ToListAsync();

    [HttpPost("{id:guid}/timeline")]
    public async Task<ActionResult<TimelineEntryDto>> AddTimelineEntry(Guid id, TimelineEntryCreateDto dto)
    {
        var trip = await _db.Trips.FindAsync(id);
        if (trip == null) return NotFound();

        var capturedAt = dto.CapturedAt ?? DateTime.UtcNow;
        Guid? nearestStopId = null;
        if (dto.Lat.HasValue && dto.Lng.HasValue)
        {
            var stops = await _db.TripStops.Where(s => s.TripId == id).ToListAsync();
            nearestStopId = stops
                .OrderBy(s => Math.Pow(s.Lat - dto.Lat.Value, 2) + Math.Pow(s.Lng - dto.Lng.Value, 2))
                .FirstOrDefault()?.Id;
        }

        var entry = new TimelineEntry
        {
            TripId = id, Type = dto.Type, Content = dto.Content, Lat = dto.Lat, Lng = dto.Lng,
            CapturedAt = capturedAt, NearestStopId = nearestStopId,
        };
        _db.TimelineEntries.Add(entry);
        await _db.SaveChangesAsync();
        return new TimelineEntryDto(entry.Id, entry.Type, entry.Content, entry.Lat, entry.Lng, entry.CapturedAt, entry.NearestStopId);
    }

    [HttpDelete("timeline/{entryId:guid}")]
    public async Task<IActionResult> DeleteTimelineEntry(Guid entryId)
    {
        var entry = await _db.TimelineEntries.FindAsync(entryId);
        if (entry == null) return NotFound();
        _db.TimelineEntries.Remove(entry);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static TripDto ToDto(Trip t, List<TagDto> tags) =>
        new(t.Id, t.Name, t.Description, t.StartDate, t.EndDate, t.Status, tags);
}
