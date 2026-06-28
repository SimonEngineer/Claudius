using Microsoft.AspNetCore.Mvc;

namespace TripPlanner.Api.Controllers;

/// <summary>Proxies the free, keyless frankfurter.app exchange-rate API so the frontend avoids CORS/key management.</summary>
[ApiController]
[Route("api/fx")]
public class FxController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    public FxController(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    [HttpGet("rate")]
    public async Task<IActionResult> GetRate([FromQuery] string from, [FromQuery] string to)
    {
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return Ok(new { rate = 1.0 });

        var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync($"https://api.frankfurter.app/latest?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}");
        if (!response.IsSuccessStatusCode) return StatusCode(502, new { error = "Exchange rate lookup failed" });

        var json = await response.Content.ReadFromJsonAsync<FrankfurterResponse>();
        if (json?.Rates == null || !json.Rates.TryGetValue(to.ToUpperInvariant(), out var rate)) return StatusCode(502, new { error = "Unknown currency" });

        return Ok(new { rate });
    }

    private record FrankfurterResponse(Dictionary<string, double> Rates);
}
