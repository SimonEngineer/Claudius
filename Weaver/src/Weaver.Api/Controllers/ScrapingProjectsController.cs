using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Weaver.Api.Dtos;
using Weaver.Domain;
using Weaver.Infrastructure.Auditing;
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
    private readonly TestExtractionService _testExtraction;
    private readonly IAuditLogger _auditLogger;

    public ScrapingProjectsController(WeaverDbContext db, IScrapeJobQueue queue, TestExtractionService testExtraction, IAuditLogger auditLogger)
    {
        _testExtraction = testExtraction;
        _db = db;
        _queue = queue;
        _auditLogger = auditLogger;
    }

    private Guid UserId => User.GetUserId();

    [HttpGet]
    public async Task<ActionResult<List<ScrapingProjectDto>>> List(CancellationToken ct)
    {
        var projects = await _db.ScrapingProjects.Include(p => p.Fields)
            .Where(p => p.OwnerUserId == UserId)
            .OrderByDescending(p => p.UpdatedAt).ToListAsync(ct);

        var projectIds = projects.Select(p => p.Id).ToArray();
        // DISTINCT ON is the idiomatic Postgres way to get one (the latest) row per group without
        // an N+1 query per project or fetching every run ever recorded just to keep the newest.
        var lastRuns = await _db.ScrapeRuns
            .FromSqlInterpolated($@"SELECT DISTINCT ON (""ScrapingProjectId"") * FROM scrape_runs WHERE ""ScrapingProjectId"" = ANY({projectIds}) ORDER BY ""ScrapingProjectId"", ""CreatedAt"" DESC")
            .ToListAsync(ct);
        var lastRunByProject = lastRuns.ToDictionary(r => r.ScrapingProjectId);

        return projects.Select(p =>
        {
            var dto = ScrapingProjectDto.FromEntity(p);
            return lastRunByProject.TryGetValue(p.Id, out var run) ? dto with { LastRunStatus = run.Status, LastRunAt = run.CreatedAt } : dto;
        }).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ScrapingProjectDto>> Get(Guid id, CancellationToken ct)
    {
        var project = await _db.ScrapingProjects.Include(p => p.Fields).FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == UserId, ct);
        return project is null ? NotFound() : ScrapingProjectDto.FromEntity(project);
    }

    [HttpPost]
    public async Task<ActionResult<ScrapingProjectDto>> Create(UpsertScrapingProjectRequest request, CancellationToken ct)
    {
        if (!await OwnsPolicyOrNullAsync(request.RateLimitPolicyId, ct))
        {
            return BadRequest("Unknown rate limit policy.");
        }

        var project = new ScrapingProject
        {
            OwnerUserId = UserId,
            Name = request.Name,
            Description = request.Description,
            StartUrl = request.StartUrl,
            Mode = request.Mode,
            RenderMode = request.RenderMode,
            ItemSelector = request.ItemSelector,
            PaginationStrategy = request.PaginationStrategy,
            NextPageSelector = request.NextPageSelector,
            PageUrlTemplate = request.PageUrlTemplate,
            MaxPages = request.MaxPages,
            CustomHeadersJson = JsonSerializer.Serialize(request.CustomHeaders ?? new Dictionary<string, string>()),
            DataRetentionDays = request.DataRetentionDays,
            RateLimitPolicyId = request.RateLimitPolicyId,
            IsEnabled = request.IsEnabled,
        };
        ApplyFields(project, request.Fields);

        _db.ScrapingProjects.Add(project);
        _auditLogger.Record(UserId, AuditAction.Created, "ScrapingProject", project.Id, project.Name);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = project.Id }, ScrapingProjectDto.FromEntity(project));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ScrapingProjectDto>> Update(Guid id, UpsertScrapingProjectRequest request, CancellationToken ct)
    {
        var project = await _db.ScrapingProjects.Include(p => p.Fields).FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == UserId, ct);
        if (project is null)
        {
            return NotFound();
        }

        if (!await OwnsPolicyOrNullAsync(request.RateLimitPolicyId, ct))
        {
            return BadRequest("Unknown rate limit policy.");
        }

        project.Name = request.Name;
        project.Description = request.Description;
        project.StartUrl = request.StartUrl;
        project.Mode = request.Mode;
        project.RenderMode = request.RenderMode;
        project.ItemSelector = request.ItemSelector;
        project.PaginationStrategy = request.PaginationStrategy;
        project.NextPageSelector = request.NextPageSelector;
        project.PageUrlTemplate = request.PageUrlTemplate;
        project.MaxPages = request.MaxPages;
        project.CustomHeadersJson = JsonSerializer.Serialize(request.CustomHeaders ?? new Dictionary<string, string>());
        project.DataRetentionDays = request.DataRetentionDays;
        project.RateLimitPolicyId = request.RateLimitPolicyId;
        project.IsEnabled = request.IsEnabled;
        project.UpdatedAt = DateTimeOffset.UtcNow;

        _db.FieldSelectors.RemoveRange(project.Fields);
        project.Fields.Clear();
        ApplyFields(project, request.Fields);

        _auditLogger.Record(UserId, AuditAction.Updated, "ScrapingProject", project.Id, project.Name);
        await _db.SaveChangesAsync(ct);
        return ScrapingProjectDto.FromEntity(project);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var project = await _db.ScrapingProjects.FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == UserId, ct);
        if (project is null)
        {
            return NotFound();
        }

        _db.ScrapingProjects.Remove(project);
        _auditLogger.Record(UserId, AuditAction.Deleted, "ScrapingProject", project.Id, project.Name);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/duplicate")]
    public async Task<ActionResult<ScrapingProjectDto>> Duplicate(Guid id, CancellationToken ct)
    {
        var source = await _db.ScrapingProjects.Include(p => p.Fields).FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == UserId, ct);
        if (source is null)
        {
            return NotFound();
        }

        var copy = new ScrapingProject
        {
            OwnerUserId = UserId,
            Name = $"{source.Name} (Copy)",
            Description = source.Description,
            StartUrl = source.StartUrl,
            Mode = source.Mode,
            RenderMode = source.RenderMode,
            ItemSelector = source.ItemSelector,
            PaginationStrategy = source.PaginationStrategy,
            NextPageSelector = source.NextPageSelector,
            PageUrlTemplate = source.PageUrlTemplate,
            MaxPages = source.MaxPages,
            CustomHeadersJson = source.CustomHeadersJson,
            DataRetentionDays = source.DataRetentionDays,
            RateLimitPolicyId = source.RateLimitPolicyId,
            IsEnabled = source.IsEnabled,
        };
        foreach (var f in source.Fields)
        {
            copy.Fields.Add(new FieldSelector
            {
                ScrapingProjectId = copy.Id,
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

        _db.ScrapingProjects.Add(copy);
        _auditLogger.Record(UserId, AuditAction.Created, "ScrapingProject", copy.Id, copy.Name);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = copy.Id }, ScrapingProjectDto.FromEntity(copy));
    }

    /// <summary>Enqueues a scrape job onto the distributed queue rather than running inline, so this call stays fast and any worker instance can pick it up.</summary>
    [HttpPost("{id:guid}/run")]
    public async Task<ActionResult<object>> Run(Guid id, CancellationToken ct)
    {
        var exists = await _db.ScrapingProjects.AnyAsync(p => p.Id == id && p.OwnerUserId == UserId, ct);
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
        if (!await _db.ScrapingProjects.AnyAsync(p => p.Id == id && p.OwnerUserId == UserId, ct))
        {
            return NotFound();
        }

        var runs = await _db.ScrapeRuns.Where(r => r.ScrapingProjectId == id).OrderByDescending(r => r.CreatedAt).Take(50).ToListAsync(ct);
        return runs.Select(ScrapeRunDto.FromEntity).ToList();
    }

    /// <summary>Downloads this project's run history (not capped at 50, unlike the list view) as CSV or JSON.</summary>
    [HttpGet("{id:guid}/runs/export")]
    public async Task<IActionResult> ExportRuns(Guid id, [FromQuery] string format = "csv", CancellationToken ct = default)
    {
        var project = await _db.ScrapingProjects.FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == UserId, ct);
        if (project is null)
        {
            return NotFound();
        }

        var runs = await _db.ScrapeRuns.Where(r => r.ScrapingProjectId == id).OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        var fileNameStem = string.Join("-", project.Name.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonSerializer.Serialize(runs.Select(ScrapeRunDto.FromEntity), new JsonSerializerOptions { WriteIndented = true });
            return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", $"{fileNameStem}-runs.json");
        }

        var csv = ScrapeRunsCsvWriter.Write(runs);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"{fileNameStem}-runs.csv");
    }

    [HttpGet("{id:guid}/items")]
    public async Task<ActionResult<PagedResult<ScrapedItemDto>>> Items(
        Guid id, [FromQuery] Guid? runId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        if (!await _db.ScrapingProjects.AnyAsync(p => p.Id == id && p.OwnerUserId == UserId, ct))
        {
            return NotFound();
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var query = _db.ScrapedItems.Where(i => i.ScrapingProjectId == id);
        query = runId is not null ? query.Where(i => i.ScrapeRunId == runId) : query;

        var totalCount = await query.CountAsync(ct);
        var items = await query.OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<ScrapedItemDto>(items.Select(ScrapedItemDto.FromEntity).ToList(), totalCount, page, pageSize);
    }

    /// <summary>Downloads every matching scraped item (not just the current page) as CSV or JSON.</summary>
    [HttpGet("{id:guid}/items/export")]
    public async Task<IActionResult> ExportItems(Guid id, [FromQuery] Guid? runId, [FromQuery] string format = "json", CancellationToken ct = default)
    {
        var project = await _db.ScrapingProjects.Include(p => p.Fields).FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == UserId, ct);
        if (project is null)
        {
            return NotFound();
        }

        var query = _db.ScrapedItems.Where(i => i.ScrapingProjectId == id);
        query = runId is not null ? query.Where(i => i.ScrapeRunId == runId) : query;
        var items = await query.OrderByDescending(i => i.CreatedAt).ToListAsync(ct);

        var fileNameStem = string.Join("-", project.Name.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var csv = ScrapedItemsCsvWriter.Write(project.Fields.OrderBy(f => f.Order).Select(f => f.Name).ToList(), items);
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"{fileNameStem}-items.csv");
        }

        var json = JsonSerializer.Serialize(items.Select(ScrapedItemDto.FromEntity), new JsonSerializerOptions { WriteIndented = true });
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", $"{fileNameStem}-items.json");
    }

    /// <summary>
    /// Every scrape run keeps its own ScrapedItem row rather than overwriting the last one, so an
    /// item's full history (by ItemKey, its stable cross-run identity) is already sitting in the
    /// table -- this just surfaces it, oldest first, with a field-by-field diff against whatever
    /// snapshot came right before each one (the "what actually changed" view behind a price-drop
    /// alert, made visible instead of only ever firing silently into a workflow).
    /// </summary>
    [HttpGet("{id:guid}/items/history/{itemKey}")]
    public async Task<ActionResult<List<ItemSnapshotDto>>> ItemHistory(Guid id, string itemKey, CancellationToken ct)
    {
        if (!await _db.ScrapingProjects.AnyAsync(p => p.Id == id && p.OwnerUserId == UserId, ct))
        {
            return NotFound();
        }

        var snapshots = await _db.ScrapedItems
            .Where(i => i.ScrapingProjectId == id && i.ItemKey == itemKey)
            .OrderBy(i => i.CreatedAt)
            .ToListAsync(ct);

        if (snapshots.Count == 0)
        {
            return NotFound();
        }

        var result = new List<ItemSnapshotDto>();
        Dictionary<string, string?>? previousData = null;
        foreach (var snapshot in snapshots)
        {
            Dictionary<string, FieldDiffDto>? changes = null;
            if (previousData is not null)
            {
                changes = new Dictionary<string, FieldDiffDto>();
                foreach (var key in previousData.Keys.Union(snapshot.Data.Keys))
                {
                    var before = previousData.GetValueOrDefault(key);
                    var after = snapshot.Data.GetValueOrDefault(key);
                    if (before != after)
                    {
                        changes[key] = new FieldDiffDto(before, after);
                    }
                }
            }

            result.Add(new ItemSnapshotDto(snapshot.Id, snapshot.CreatedAt, snapshot.Data, changes));
            previousData = snapshot.Data;
        }

        return result;
    }

    /// <summary>Runs the given (not-yet-saved) selectors against one live page and returns what they'd extract, without persisting anything -- for iterating on selectors before committing to a real run.</summary>
    [HttpPost("test-extract")]
    public async Task<ActionResult<TestExtractionResult>> TestExtract(TestExtractionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            return BadRequest("A url is required.");
        }

        var policy = request.RateLimitPolicyId is not null
            ? await _db.RateLimitPolicies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.RateLimitPolicyId && p.OwnerUserId == UserId, ct)
            : null;

        var fields = request.Fields.Select(f => new FieldSelector
        {
            Name = f.Name,
            Selector = f.Selector,
            Attribute = f.Attribute,
            AttributeName = f.AttributeName,
            ResolveUrl = f.ResolveUrl,
            IsKey = f.IsKey,
            Required = f.Required,
            Order = f.Order,
        }).ToList();

        var result = await _testExtraction.ExtractAsync(
            request.Url, request.Mode, request.ItemSelector, fields, policy, request.ScrapingProjectId ?? Guid.Empty,
            request.RenderMode, request.CustomHeaders, ct);
        return result;
    }

    private async Task<bool> OwnsPolicyOrNullAsync(Guid? policyId, CancellationToken ct) =>
        policyId is null || await _db.RateLimitPolicies.AnyAsync(p => p.Id == policyId && p.OwnerUserId == UserId, ct);

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
