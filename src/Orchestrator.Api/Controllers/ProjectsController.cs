using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Api.Dtos;
using Orchestrator.Domain;
using Orchestrator.Infrastructure.Persistence;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController(OrchestratorDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Project>>> List(CancellationToken ct)
    {
        var projects = await db.Projects.OrderBy(p => p.Name).ToListAsync(ct);
        return Ok(projects);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Project>> Get(Guid id, CancellationToken ct)
    {
        var project = await db.Projects.FindAsync([id], ct);
        return project is null ? NotFound() : Ok(project);
    }

    [HttpPost]
    public async Task<ActionResult<Project>> Create(CreateProjectRequest request, CancellationToken ct)
    {
        var project = new Project
        {
            Name = request.Name,
            RepoPath = request.RepoPath,
            GitRemote = request.GitRemote,
            WorkerModel = request.WorkerModel,
            SupervisorModel = request.SupervisorModel,
            MaxWorkerConcurrency = request.MaxWorkerConcurrency,
            Priority = request.Priority
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = project.Id }, project);
    }
}
