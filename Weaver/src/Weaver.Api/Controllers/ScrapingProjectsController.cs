using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Weaver.Api.Dtos;
using Weaver.Domain;
using Weaver.Infrastructure.Persistence;
using Weaver.Infrastructure.Queue;
using Weaver.Scraping;

namespace Weaver.Api.Controllers;

[ApiController]
[Route("api/scraping-projects")]
public class ScrapingProjectsController : ControllerBase
{
    private readonly WeaverDbContext _db;
    private readonly IScrapeJobQueue _queue;

    public ScrapingProjectsController(WeaverDbContext db, IScrapeJobQueue queue)
    {
        _db = db;
        _queue = queue;
    }

    [HttpGet]
    public async Task<ActionResult<List<ScrapingProjectDto>>> List(CancellationToken ct)
    {
        var projects = await _db.ScrapingProjects.Include(p => p.Fields).OrderByDescending(p => p.UpdatedAt).ToListAsync(ct);
        return projects.Select(ScrapingProjectDto.FromEntity).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ScrapingProjectDto>> Get(Guid id, CancellationToken ct)
    {
        var project = await _db.ScrapingProjects.Include(p => p.Fields).FirstOrDefaultAsync(p => p.Id == id, ct);
        return project is null ? NotFound() : ScrapingProjectDto.FromEntity(project);
    }

    [HttpPost]
    public async Task<ActionResult<ScrapingProjectDto>> Create(UpsertScrapingProjectRequest request, CancellationToken ct)
    {
        var project = new ScrapingProject
        {
            Name = request.Name,
            Description = request.Description,
            StartUrl = request.StartUrl,
            Mode = request.Mode,
            ItemSelector = request.ItemSelector,
            PaginationStrategy = request.PaginationStrategy,
            NextPageSelector = request.NextPageSelector,
            PageUrlTemplate = request.PageUrlTemplate,
            MaxPages = request.MaxPages,
            RateLimitPolicyId = request.RateLimitPolicyId,
            IsEnabled = request.IsEnabled,
        };
        ApplyFields(project, request.Fields);

        _db.ScrapingProjects.Add(project);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = project.Id }, ScrapingProjectDto.FromEntity(project));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ScrapingProjectDto>> Update(Guid id, UpsertScrapingProjectRequest request, CancellationToken ct)
    {
        var project = await _db.ScrapingProjects.Include(p => p.Fields).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null)
        {
            return NotFound();
        }

        project.Name = request.Name;
        project.Description = request.Description;
        project.StartUrl = request.StartUrl;
        project.Mode = request.Mode;
        project.ItemSelector = request.ItemSelector;
        project.PaginationStrategy = request.PaginationStrategy;
        project.NextPageSelector = request.NextPageSelector;
        project.PageUrlTemplate = request.PageUrlTemplate;
        project.MaxPages = request.MaxPages;
        project.RateLimitPolicyId = request.RateLimitPolicyId;
        project.IsEnabled = request.IsEnabled;
        project.UpdatedAt = DateTimeOffset.UtcNow;

        _db.FieldSelectors.RemoveRange(project.Fields);
        project.Fields.Clear();
        ApplyFields(project, request.Fields);

        await _db.SaveChangesAsync(ct);
        return ScrapingProjectDto.FromEntity(project);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var project = await _db.ScrapingProjects.FindAsync([id], ct);
        if (project is null)
        {
            return NotFound();
        }

        _db.ScrapingProjects.Remove(project);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Enqueues a scrape job onto the distributed queue rather than running inline, so this call stays fast and any worker instance can pick it up.</summary>
    [HttpPost("{id:guid}/run")]
    public async Task<ActionResult<object>> Run(Guid id, CancellationToken ct)
    {
        var exists = await _db.ScrapingProjects.AnyAsync(p => p.Id == id, ct);
        if (!exists)
        {
            return NotFound();
        }

        var job = new ScrapeJob { ScrapingProjectId = id, TriggeredBy = TriggerKind.Manual, Status = ScrapeJobStatus.Queued };
        _db.ScrapeJobs.Add(job);
        await _db.SaveChangesAsync(ct);

        var streamId = await _queue.EnqueueAsync(new ScrapeJobMessage(job.Id, id, TriggerKind.Manual, null), ct);
        job.StreamMessageId = streamId;
        await _db.SaveChangesAsync(ct);

        return Accepted(new { jobId = job.Id });
    }

    [HttpGet("{id:guid}/runs")]
    public async Task<ActionResult<List<ScrapeRunDto>>> Runs(Guid id, CancellationToken ct)
    {
        var runs = await _db.ScrapeRuns.Where(r => r.ScrapingProjectId == id).OrderByDescending(r => r.CreatedAt).Take(50).ToListAsync(ct);
        return runs.Select(ScrapeRunDto.FromEntity).ToList();
    }

    [HttpGet("{id:guid}/items")]
    public async Task<ActionResult<List<ScrapedItemDto>>> Items(Guid id, [FromQuery] Guid? runId, CancellationToken ct)
    {
        var query = _db.ScrapedItems.Where(i => i.ScrapingProjectId == id);
        query = runId is not null ? query.Where(i => i.ScrapeRunId == runId) : query;
        var items = await query.OrderByDescending(i => i.CreatedAt).Take(500).ToListAsync(ct);
        return items.Select(ScrapedItemDto.FromEntity).ToList();
    }

    private static void ApplyFields(ScrapingProject project, List<FieldSelectorDto> fields)
    {
        foreach (var f in fields)
        {
            project.Fields.Add(new FieldSelector
            {
                ScrapingProjectId = project.Id,
                Name = f.Name,
                Selector = f.Selector,
                Attribute = f.Attribute,
                AttributeName = f.AttributeName,
                ResolveUrl = f.ResolveUrl,
                IsKey = f.IsKey,
                Required = f.Required,
                Order = f.Order,
            });
        }
    }
}
