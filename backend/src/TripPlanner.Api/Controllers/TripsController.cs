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
        trip.Budget = dto.Budget; trip.BudgetCurrency = dto.BudgetCurrency;
        await _db.SaveChangesAsync();
        var tags = await TagHelper.GetTagsForAsync(_db, EntityType.Trip, id);
        return ToDto(trip, tags);
    }

    [HttpPost("{id:guid}/share")]
    public async Task<ActionResult<TripDto>> CreateShareLink(Guid id)
    {
        var trip = await _db.Trips.FindAsync(id);
        if (trip == null) return NotFound();
        trip.ShareSlug ??= Guid.NewGuid().ToString("N");
        await _db.SaveChangesAsync();
        var tags = await TagHelper.GetTagsForAsync(_db, EntityType.Trip, id);
        return ToDto(trip, tags);
    }

    [HttpDelete("{id:guid}/share")]
    public async Task<ActionResult<TripDto>> RevokeShareLink(Guid id)
    {
        var trip = await _db.Trips.FindAsync(id);
        if (trip == null) return NotFound();
        trip.ShareSlug = null;
        await _db.SaveChangesAsync();
        var tags = await TagHelper.GetTagsForAsync(_db, EntityType.Trip, id);
        return ToDto(trip, tags);
    }

    [HttpPut("{id:guid}/emergency-info")]
    public async Task<ActionResult<TripDto>> UpdateEmergencyInfo(Guid id, EmergencyInfoUpdateDto dto)
    {
        var trip = await _db.Trips.FindAsync(id);
        if (trip == null) return NotFound();
        trip.EmergencyInfo = dto.EmergencyInfo;
        await _db.SaveChangesAsync();
        var tags = await TagHelper.GetTagsForAsync(_db, EntityType.Trip, id);
        return ToDto(trip, tags);
    }

    [HttpGet("{id:guid}/calendar.ics")]
    public async Task<IActionResult> GetCalendar(Guid id)
    {
        var trip = await _db.Trips.FindAsync(id);
        if (trip == null) return NotFound();
        var bookings = await _db.Bookings.Where(b => b.TripId == id).ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//TripPlanner//Calendar//EN");
        foreach (var b in bookings)
        {
            var start = b.StartAt ?? DateTime.UtcNow;
            var end = b.EndAt ?? start.AddHours(1);
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"UID:{b.Id}@tripplanner");
            sb.AppendLine($"DTSTART:{start:yyyyMMddTHHmmssZ}");
            sb.AppendLine($"DTEND:{end:yyyyMMddTHHmmssZ}");
            sb.AppendLine($"SUMMARY:{IcsEscape(b.Title)}");
            if (!string.IsNullOrWhiteSpace(b.ConfirmationNumber))
                sb.AppendLine($"DESCRIPTION:{IcsEscape("Confirmation: " + b.ConfirmationNumber)}");
            sb.AppendLine("END:VEVENT");
        }
        sb.AppendLine("END:VCALENDAR");

        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/calendar", $"{trip.Name}.ics");
    }

    private static string IcsEscape(string s) => s.Replace(",", "\\,").Replace(";", "\\;");

    // Companions
    [HttpGet("{id:guid}/companions")]
    public async Task<ActionResult<List<TripCompanionDto>>> GetCompanions(Guid id)
        => await _db.TripCompanions.Where(c => c.TripId == id).OrderBy(c => c.CreatedAt)
            .Select(c => new TripCompanionDto(c.Id, c.Name)).ToListAsync();

    [HttpPost("{id:guid}/companions")]
    public async Task<ActionResult<TripCompanionDto>> AddCompanion(Guid id, TripCompanionCreateDto dto)
    {
        var trip = await _db.Trips.FindAsync(id);
        if (trip == null) return NotFound();
        var companion = new TripCompanion { TripId = id, Name = dto.Name };
        _db.TripCompanions.Add(companion);
        await _db.SaveChangesAsync();
        return new TripCompanionDto(companion.Id, companion.Name);
    }

    [HttpDelete("companions/{companionId:guid}")]
    public async Task<IActionResult> DeleteCompanion(Guid companionId)
    {
        var companion = await _db.TripCompanions.FindAsync(companionId);
        if (companion == null) return NotFound();
        _db.TripCompanions.Remove(companion);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Documents
    [HttpGet("{id:guid}/documents")]
    public async Task<ActionResult<List<TravelDocumentDto>>> GetDocuments(Guid id)
        => await _db.TravelDocuments.Where(d => d.TripId == id).OrderBy(d => d.ExpiryDate)
            .Select(d => new TravelDocumentDto(d.Id, d.Title, d.DocType, d.ExpiryDate, d.Url, d.Notes)).ToListAsync();

    [HttpPost("{id:guid}/documents")]
    public async Task<ActionResult<TravelDocumentDto>> AddDocument(Guid id, TravelDocumentCreateDto dto)
    {
        var trip = await _db.Trips.FindAsync(id);
        if (trip == null) return NotFound();
        var doc = new TravelDocument { TripId = id, Title = dto.Title, DocType = dto.DocType, ExpiryDate = dto.ExpiryDate, Url = dto.Url, Notes = dto.Notes };
        _db.TravelDocuments.Add(doc);
        await _db.SaveChangesAsync();
        return new TravelDocumentDto(doc.Id, doc.Title, doc.DocType, doc.ExpiryDate, doc.Url, doc.Notes);
    }

    [HttpPut("documents/{documentId:guid}")]
    public async Task<ActionResult<TravelDocumentDto>> UpdateDocument(Guid documentId, TravelDocumentCreateDto dto)
    {
        var doc = await _db.TravelDocuments.FindAsync(documentId);
        if (doc == null) return NotFound();
        doc.Title = dto.Title; doc.DocType = dto.DocType; doc.ExpiryDate = dto.ExpiryDate; doc.Url = dto.Url; doc.Notes = dto.Notes;
        await _db.SaveChangesAsync();
        return new TravelDocumentDto(doc.Id, doc.Title, doc.DocType, doc.ExpiryDate, doc.Url, doc.Notes);
    }

    [HttpDelete("documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid documentId)
    {
        var doc = await _db.TravelDocuments.FindAsync(documentId);
        if (doc == null) return NotFound();
        _db.TravelDocuments.Remove(doc);
        await _db.SaveChangesAsync();
        return NoContent();
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
            .Select(s => new TripStopDto(s.Id, s.Name, s.Lat, s.Lng, s.ArriveDate, s.DepartDate, s.SortOrder, s.IsStart, s.IsEnd, s.Notes, s.SourceLocationId)).ToListAsync();

    [HttpPost("{id:guid}/stops")]
    public async Task<ActionResult<TripStopDto>> AddStop(Guid id, TripStopCreateDto dto)
    {
        var trip = await _db.Trips.FindAsync(id);
        if (trip == null) return NotFound();
        var stop = new TripStop
        {
            TripId = id, Name = dto.Name, Lat = dto.Lat, Lng = dto.Lng,
            ArriveDate = dto.ArriveDate, DepartDate = dto.DepartDate, SortOrder = dto.SortOrder,
            IsStart = dto.IsStart, IsEnd = dto.IsEnd, Notes = dto.Notes, SourceLocationId = dto.SourceLocationId,
        };
        _db.TripStops.Add(stop);
        await _db.SaveChangesAsync();
        return new TripStopDto(stop.Id, stop.Name, stop.Lat, stop.Lng, stop.ArriveDate, stop.DepartDate, stop.SortOrder, stop.IsStart, stop.IsEnd, stop.Notes, stop.SourceLocationId);
    }

    [HttpPut("stops/{stopId:guid}")]
    public async Task<ActionResult<TripStopDto>> UpdateStop(Guid stopId, TripStopCreateDto dto)
    {
        var stop = await _db.TripStops.FindAsync(stopId);
        if (stop == null) return NotFound();
        stop.Name = dto.Name; stop.Lat = dto.Lat; stop.Lng = dto.Lng; stop.ArriveDate = dto.ArriveDate;
        stop.DepartDate = dto.DepartDate; stop.SortOrder = dto.SortOrder; stop.IsStart = dto.IsStart; stop.IsEnd = dto.IsEnd; stop.Notes = dto.Notes;
        await _db.SaveChangesAsync();
        return new TripStopDto(stop.Id, stop.Name, stop.Lat, stop.Lng, stop.ArriveDate, stop.DepartDate, stop.SortOrder, stop.IsStart, stop.IsEnd, stop.Notes, stop.SourceLocationId);
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

    [HttpPut("{id:guid}/stops/reorder")]
    public async Task<IActionResult> ReorderStops(Guid id, [FromBody] List<Guid> orderedStopIds)
    {
        var stops = await _db.TripStops.Where(s => s.TripId == id).ToListAsync();
        for (var i = 0; i < orderedStopIds.Count; i++)
        {
            var stop = stops.FirstOrDefault(s => s.Id == orderedStopIds[i]);
            if (stop != null) stop.SortOrder = i;
        }
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Bookings
    [HttpGet("{id:guid}/bookings")]
    public async Task<ActionResult<List<BookingDto>>> GetBookings(Guid id)
        => await _db.Bookings.Where(b => b.TripId == id).OrderBy(b => b.StartAt)
            .Select(b => new BookingDto(b.Id, b.Type, b.Title, b.ConfirmationNumber, b.StartAt, b.EndAt, b.Lat, b.Lng, b.DetailsJson, b.Cost)).ToListAsync();

    [HttpPost("{id:guid}/bookings")]
    public async Task<ActionResult<BookingDto>> AddBooking(Guid id, BookingCreateDto dto)
    {
        var trip = await _db.Trips.FindAsync(id);
        if (trip == null) return NotFound();
        var booking = new Booking
        {
            TripId = id, Type = dto.Type, Title = dto.Title, ConfirmationNumber = dto.ConfirmationNumber,
            StartAt = dto.StartAt, EndAt = dto.EndAt, Lat = dto.Lat, Lng = dto.Lng, DetailsJson = dto.DetailsJson, Cost = dto.Cost,
        };
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();
        return new BookingDto(booking.Id, booking.Type, booking.Title, booking.ConfirmationNumber, booking.StartAt, booking.EndAt, booking.Lat, booking.Lng, booking.DetailsJson, booking.Cost);
    }

    [HttpPut("bookings/{bookingId:guid}")]
    public async Task<ActionResult<BookingDto>> UpdateBooking(Guid bookingId, BookingCreateDto dto)
    {
        var booking = await _db.Bookings.FindAsync(bookingId);
        if (booking == null) return NotFound();
        booking.Type = dto.Type; booking.Title = dto.Title; booking.ConfirmationNumber = dto.ConfirmationNumber;
        booking.StartAt = dto.StartAt; booking.EndAt = dto.EndAt; booking.Lat = dto.Lat; booking.Lng = dto.Lng; booking.DetailsJson = dto.DetailsJson; booking.Cost = dto.Cost;
        await _db.SaveChangesAsync();
        return new BookingDto(booking.Id, booking.Type, booking.Title, booking.ConfirmationNumber, booking.StartAt, booking.EndAt, booking.Lat, booking.Lng, booking.DetailsJson, booking.Cost);
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

    // Packing list
    [HttpGet("{id:guid}/packing")]
    public async Task<ActionResult<List<PackingItemDto>>> GetPackingItems(Guid id)
        => await _db.PackingItems.Where(p => p.TripId == id).OrderBy(p => p.CreatedAt)
            .Select(p => new PackingItemDto(p.Id, p.Name, p.IsPacked)).ToListAsync();

    [HttpPost("{id:guid}/packing")]
    public async Task<ActionResult<PackingItemDto>> AddPackingItem(Guid id, PackingItemCreateDto dto)
    {
        var trip = await _db.Trips.FindAsync(id);
        if (trip == null) return NotFound();
        var item = new PackingItem { TripId = id, Name = dto.Name };
        _db.PackingItems.Add(item);
        await _db.SaveChangesAsync();
        return new PackingItemDto(item.Id, item.Name, item.IsPacked);
    }

    [HttpPost("packing/{itemId:guid}/toggle")]
    public async Task<ActionResult<PackingItemDto>> TogglePackingItem(Guid itemId, [FromQuery] bool packed)
    {
        var item = await _db.PackingItems.FindAsync(itemId);
        if (item == null) return NotFound();
        item.IsPacked = packed;
        await _db.SaveChangesAsync();
        return new PackingItemDto(item.Id, item.Name, item.IsPacked);
    }

    [HttpDelete("packing/{itemId:guid}")]
    public async Task<IActionResult> DeletePackingItem(Guid itemId)
    {
        var item = await _db.PackingItems.FindAsync(itemId);
        if (item == null) return NotFound();
        _db.PackingItems.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Expenses
    [HttpGet("{id:guid}/expenses")]
    public async Task<ActionResult<List<ExpenseDto>>> GetExpenses(Guid id)
        => (await _db.Expenses.Where(e => e.TripId == id).OrderBy(e => e.Date).ToListAsync())
            .Select(ToExpenseDto).ToList();

    [HttpPost("{id:guid}/expenses")]
    public async Task<ActionResult<ExpenseDto>> AddExpense(Guid id, ExpenseCreateDto dto)
    {
        var trip = await _db.Trips.FindAsync(id);
        if (trip == null) return NotFound();
        var expense = new Expense
        {
            TripId = id, Category = dto.Category, Amount = dto.Amount, Currency = dto.Currency,
            Date = dto.Date, Note = dto.Note, BookingId = dto.BookingId, PaidByCompanionId = dto.PaidByCompanionId,
            SplitCompanionIds = JoinIds(dto.SplitCompanionIds),
        };
        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync();
        return ToExpenseDto(expense);
    }

    [HttpPut("expenses/{expenseId:guid}")]
    public async Task<ActionResult<ExpenseDto>> UpdateExpense(Guid expenseId, ExpenseCreateDto dto)
    {
        var expense = await _db.Expenses.FindAsync(expenseId);
        if (expense == null) return NotFound();
        expense.Category = dto.Category; expense.Amount = dto.Amount; expense.Currency = dto.Currency;
        expense.Date = dto.Date; expense.Note = dto.Note; expense.BookingId = dto.BookingId; expense.PaidByCompanionId = dto.PaidByCompanionId;
        expense.SplitCompanionIds = JoinIds(dto.SplitCompanionIds);
        await _db.SaveChangesAsync();
        return ToExpenseDto(expense);
    }

    [HttpDelete("expenses/{expenseId:guid}")]
    public async Task<IActionResult> DeleteExpense(Guid expenseId)
    {
        var expense = await _db.Expenses.FindAsync(expenseId);
        if (expense == null) return NotFound();
        _db.Expenses.Remove(expense);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static TripDto ToDto(Trip t, List<TagDto> tags) =>
        new(t.Id, t.Name, t.Description, t.StartDate, t.EndDate, t.Status, t.Budget, t.BudgetCurrency, t.ShareSlug, t.EmergencyInfo, tags);

    private static ExpenseDto ToExpenseDto(Expense e) =>
        new(e.Id, e.Category, e.Amount, e.Currency, e.Date, e.Note, e.BookingId, e.PaidByCompanionId, SplitIds(e.SplitCompanionIds));

    private static string? JoinIds(List<Guid>? ids) => ids == null || ids.Count == 0 ? null : string.Join(',', ids);

    private static List<Guid> SplitIds(string? csv) => string.IsNullOrWhiteSpace(csv)
        ? new List<Guid>()
        : csv.Split(',').Select(Guid.Parse).ToList();
}
