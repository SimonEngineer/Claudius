using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripPlanner.Api.Dtos;
using TripPlanner.Infrastructure.Persistence;

namespace TripPlanner.Api.Controllers;

/// <summary>Unauthenticated read-only access to a trip via its share slug; no costs/expenses are exposed.</summary>
[ApiController]
[Route("api/public")]
public class PublicController : ControllerBase
{
    private readonly TripPlannerDbContext _db;
    public PublicController(TripPlannerDbContext db) => _db = db;

    [HttpGet("trips/{slug}")]
    public async Task<ActionResult<PublicTripDto>> GetSharedTrip(string slug)
    {
        var trip = await _db.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.ShareSlug == slug);
        if (trip == null) return NotFound();

        var stops = await _db.TripStops.Where(s => s.TripId == trip.Id).OrderBy(s => s.SortOrder)
            .Select(s => new TripStopDto(s.Id, s.Name, s.Lat, s.Lng, s.ArriveDate, s.DepartDate, s.SortOrder, s.IsStart, s.IsEnd, s.Notes, s.SourceLocationId, s.Country)).ToListAsync();
        var bookings = await _db.Bookings.Where(b => b.TripId == trip.Id).OrderBy(b => b.StartAt)
            .Select(b => new BookingDto(b.Id, b.Type, b.Title, b.ConfirmationNumber, b.StartAt, b.EndAt, b.Lat, b.Lng, b.DetailsJson, null)).ToListAsync();
        var timeline = await _db.TimelineEntries.Where(e => e.TripId == trip.Id).OrderBy(e => e.CapturedAt)
            .Select(e => new TimelineEntryDto(e.Id, e.Type, e.Content, e.Lat, e.Lng, e.CapturedAt, e.NearestStopId)).ToListAsync();

        return new PublicTripDto(trip.Name, trip.Description, trip.StartDate, trip.EndDate, trip.Status, stops, bookings, timeline);
    }
}
