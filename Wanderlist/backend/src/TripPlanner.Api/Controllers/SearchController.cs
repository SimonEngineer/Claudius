using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripPlanner.Api.Dtos;
using TripPlanner.Infrastructure.Persistence;

namespace TripPlanner.Api.Controllers;

/// <summary>Global search across trips, wishlist locations and goals for the header search bar.</summary>
[ApiController]
[Route("api/search")]
public class SearchController : ControllerBase
{
    private readonly TripPlannerDbContext _db;
    public SearchController(TripPlannerDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<SearchResultDto>>> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return new List<SearchResultDto>();

        var trips = await _db.Trips.AsNoTracking().Where(t => t.Name.Contains(q))
            .Select(t => new SearchResultDto("Trip", t.Id, t.Name, t.Description)).Take(10).ToListAsync();
        var locations = await _db.WishlistLocations.AsNoTracking().Where(l => l.Name.Contains(q))
            .Select(l => new SearchResultDto("Wishlist", l.Id, l.Name, l.Country)).Take(10).ToListAsync();
        var goals = await _db.Goals.AsNoTracking().Where(g => g.Name.Contains(q))
            .Select(g => new SearchResultDto("Goal", g.Id, g.Name, null)).Take(10).ToListAsync();

        return trips.Concat(locations).Concat(goals).ToList();
    }
}
