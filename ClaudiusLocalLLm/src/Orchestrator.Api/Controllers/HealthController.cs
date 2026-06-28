using Microsoft.AspNetCore.Mvc;
using Orchestrator.Infrastructure.Persistence;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController(OrchestratorDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var dbHealthy = await db.Database.CanConnectAsync(ct);
        if (!dbHealthy)
        {
            return StatusCode(503, new { status = "unhealthy", database = false });
        }

        return Ok(new { status = "healthy", database = true });
    }
}
