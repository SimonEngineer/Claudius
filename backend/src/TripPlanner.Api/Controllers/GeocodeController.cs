using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using TripPlanner.Api.Dtos;

namespace TripPlanner.Api.Controllers;

/// <summary>Proxies the free, keyless OpenStreetMap Nominatim search API (with a proper User-Agent per its usage policy) so the frontend can offer place autocomplete without an API key.</summary>
[ApiController]
[Route("api/geocode")]
public class GeocodeController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    public GeocodeController(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    [HttpGet("search")]
    public async Task<ActionResult<List<GeocodeResultDto>>> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return new List<GeocodeResultDto>();

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TripPlanner/1.0 (personal travel planning app)");
        var url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(q)}&format=jsonv2&addressdetails=1&limit=5";
        var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode) return StatusCode(502, new { error = "Geocoding lookup failed" });

        var results = await response.Content.ReadFromJsonAsync<List<NominatimResult>>() ?? new();
        return results.Select(r => new GeocodeResultDto(r.DisplayName, double.Parse(r.Lat, System.Globalization.CultureInfo.InvariantCulture), double.Parse(r.Lon, System.Globalization.CultureInfo.InvariantCulture), r.Address?.Country)).ToList();
    }

    private record NominatimResult([property: JsonPropertyName("display_name")] string DisplayName, string Lat, string Lon, NominatimAddress? Address);
    private record NominatimAddress(string? Country);
}
