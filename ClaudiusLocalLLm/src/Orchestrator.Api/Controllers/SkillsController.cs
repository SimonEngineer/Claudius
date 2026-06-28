using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Domain;
using Orchestrator.Infrastructure.Persistence;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/skills")]
public class SkillsController(OrchestratorDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Skill>>> List(Guid projectId, CancellationToken ct)
    {
        var skills = await db.Skills
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
        return Ok(skills);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid id, CancellationToken ct)
    {
        var skill = await db.Skills.FirstOrDefaultAsync(s => s.Id == id && s.ProjectId == projectId, ct);
        if (skill is null)
        {
            return NotFound();
        }

        db.Skills.Remove(skill);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }
}
