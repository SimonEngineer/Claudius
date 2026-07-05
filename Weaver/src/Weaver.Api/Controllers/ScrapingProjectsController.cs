using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Weaver.Api.Dtos;
using Weaver.Domain;
using Weaver.Infrastructure.Auditing;
using Weaver.Infrastructure.Persistence;
using Weaver.Infrastructure.Queue;
using Weaver.Infrastructure.Security;
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
    private readonly ISensitiveConfigProtector _protector;

    public ScrapingProjectsController(
        WeaverDbContext db, IScrapeJobQueue queue, TestExtractionService testExtraction, IAuditLogger auditLogger, ISensitiveConfigProtector protector)
    {
        _testExtraction = testExtraction;
        _db = db;
        _queue = queue;
        _auditLogger = auditLogger;
        _protector = protector;
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
            var dto = ScrapingProjectDto.FromEntity(p, _protector);
            return lastRunByProject.TryGetValue(p.Id, out var run) ? dto with { LastRunStatus = run.Status, LastRunAt = run.CreatedAt } : dto;
        }).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ScrapingProjectDto>> Get(Guid id, CancellationToken ct)
    {
        var project = await _db.ScrapingProjects.Include(p => p.Fields).FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == UserId, ct);
        return project is null ? NotFound() : ScrapingProjectDto.FromEntity(project, _protector);
    }

    private static string? ValidateSchedule(string? scheduleCron)
    {
        if (string.IsNullOrWhiteSpace(scheduleCron))
        {
            return null;
        }

        try
        {
            Cronos.CronExpression.Parse(scheduleCron.Trim());
            return null;
        }
        catch (Cronos.CronFormatException ex)
        {
            return $"Invalid schedule cron expression: {ex.Message}";
        }
    }

    [HttpPost]
    public async Task<ActionResult<ScrapingProjectDto>> Create(UpsertScrapingProjectRequest request, CancellationToken ct)
    {
        if (!await OwnsPolicyOrNullAsync(request.RateLimitPolicyId, ct))
        {
            return BadRequest("Unknown rate limit policy.");
        }

        if (ValidateSchedule(request.ScheduleCron) is { } scheduleError)
        {
            return BadRequest(scheduleError);
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
            ProxyConfigJson = (request.Proxy ?? ProxyConfigDto.Disabled).ToEncryptedJson(_protector),
            ScheduleCron = string.IsNullOrWhiteSpace(request.ScheduleCron) ? null : request.ScheduleCron.Trim(),
            StartUrlsJson = JsonSerializer.Serialize(request.StartUrls ?? new List<string>()),
            RespectRobotsTxt = request.RespectRobotsTxt,
            SitemapUrl = string.IsNullOrWhiteSpace(request.SitemapUrl) ? null : request.SitemapUrl.Trim(),
            CrawlDelayMs = Math.Clamp(request.CrawlDelayMs, 0, 60_000),
            UserAgent = string.IsNullOrWhiteSpace(request.UserAgent) ? null : request.UserAgent.Trim(),
            DataRetentionDays = request.DataRetentionDays,
            RateLimitPolicyId = request.RateLimitPolicyId,
            IsEnabled = request.IsEnabled,
        };
        ApplyFields(project, request.Fields);

        _db.ScrapingProjects.Add(project);
        _auditLogger.Record(UserId, AuditAction.Created, "ScrapingProject", project.Id, project.Name);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = project.Id }, ScrapingProjectDto.FromEntity(project, _protector));
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

        if (ValidateSchedule(request.ScheduleCron) is { } scheduleError)
        {
            return BadRequest(scheduleError);
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
        project.ProxyConfigJson = (request.Proxy ?? ProxyConfigDto.Disabled).ToEncryptedJson(_protector);
        project.ScheduleCron = string.IsNullOrWhiteSpace(request.ScheduleCron) ? null : request.ScheduleCron.Trim();
        project.StartUrlsJson = JsonSerializer.Serialize(request.StartUrls ?? new List<string>());
        project.RespectRobotsTxt = request.RespectRobotsTxt;
        project.SitemapUrl = string.IsNullOrWhiteSpace(request.SitemapUrl) ? null : request.SitemapUrl.Trim();
        project.CrawlDelayMs = Math.Clamp(request.CrawlDelayMs, 0, 60_000);
        project.UserAgent = string.IsNullOrWhiteSpace(request.UserAgent) ? null : request.UserAgent.Trim();
        project.DataRetentionDays = request.DataRetentionDays;
        project.RateLimitPolicyId = request.RateLimitPolicyId;
        project.IsEnabled = request.IsEnabled;
        project.UpdatedAt = DateTimeOffset.UtcNow;

        _db.FieldSelectors.RemoveRange(project.Fields);
        project.Fields.Clear();
        ApplyFields(project, request.Fields);

        _auditLogger.Record(UserId, AuditAction.Updated, "ScrapingProject", project.Id, project.Name);
        await _db.SaveChangesAsync(ct);
        return ScrapingProjectDto.FromEntity(project, _protector);
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
            ProxyConfigJson = source.ProxyConfigJson,
            // Deliberately NOT copying ScheduleCron -- a duplicated project silently scraping on
            // the original's schedule is the same trap as duplicating an enabled workflow.
            StartUrlsJson = source.StartUrlsJson,
            RespectRobotsTxt = source.RespectRobotsTxt,
            SitemapUrl = source.SitemapUrl,
            CrawlDelayMs = source.CrawlDelayMs,
            UserAgent = source.UserAgent,
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
                TransformsJson = f.TransformsJson,
            });
        }

        _db.ScrapingProjects.Add(copy);
        _auditLogger.Record(UserId, AuditAction.Created, "ScrapingProject", copy.Id, copy.Name);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = copy.Id }, ScrapingProjectDto.FromEntity(copy, _protector));
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

    /// <summary>Downloads a portable JSON definition of this project (credentials blanked).</summary>
    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken ct)
    {
        var project = await _db.ScrapingProjects.Include(p => p.Fields).FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == UserId, ct);
        if (project is null)
        {
            return NotFound();
        }

        var export = ScrapingProjectExportDto.FromDto(ScrapingProjectDto.FromEntity(project, _protector));
        var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var fileNameStem = string.Join("-", project.Name.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", $"{fileNameStem}.weaver-project.json");
    }

    /// <summary>Creates a new (disabled) project from a previously-exported file or a template definition.</summary>
    [HttpPost("import")]
    public async Task<ActionResult<ScrapingProjectDto>> Import([FromBody] ScrapingProjectExportDto import, CancellationToken ct)
    {
        if (import.WeaverProjectExportVersion != ScrapingProjectExportDto.CurrentVersion)
        {
            return BadRequest($"Unsupported export version {import.WeaverProjectExportVersion} (this server understands {ScrapingProjectExportDto.CurrentVersion}).");
        }

        if (string.IsNullOrWhiteSpace(import.Name) || string.IsNullOrWhiteSpace(import.StartUrl))
        {
            return BadRequest("The file is missing a project name or start URL.");
        }

        var project = new ScrapingProject
        {
            OwnerUserId = UserId,
            Name = import.Name,
            Description = import.Description,
            StartUrl = import.StartUrl,
            Mode = import.Mode,
            RenderMode = import.RenderMode,
            ItemSelector = import.ItemSelector,
            PaginationStrategy = import.PaginationStrategy,
            NextPageSelector = import.NextPageSelector,
            PageUrlTemplate = import.PageUrlTemplate,
            MaxPages = import.MaxPages,
            CustomHeadersJson = JsonSerializer.Serialize(import.CustomHeaders ?? new Dictionary<string, string>()),
            ProxyConfigJson = (import.Proxy ?? ProxyConfigDto.Disabled).ToEncryptedJson(_protector),
            StartUrlsJson = JsonSerializer.Serialize(import.StartUrls ?? new List<string>()),
            RespectRobotsTxt = import.RespectRobotsTxt,
            SitemapUrl = import.SitemapUrl,
            CrawlDelayMs = Math.Clamp(import.CrawlDelayMs, 0, 60_000),
            UserAgent = import.UserAgent,
            DataRetentionDays = import.DataRetentionDays,
            IsEnabled = false, // Same safety rule as workflow import: nothing starts running on its own.
        };
        ApplyFields(project, import.Fields ?? new List<FieldSelectorDto>());

        _db.ScrapingProjects.Add(project);
        _auditLogger.Record(UserId, AuditAction.Created, "ScrapingProject", project.Id, project.Name);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = project.Id }, ScrapingProjectDto.FromEntity(project, _protector));
    }

    /// <summary>Predefined example projects (public scraping sandboxes) to start from.</summary>
    [HttpGet("templates")]
    public ActionResult<List<object>> Templates() =>
        ProjectTemplates.All.Select(t => (object)new { t.Key, t.Definition.Name, t.Definition.Description }).ToList();

    [HttpPost("templates/{key}")]
    public Task<ActionResult<ScrapingProjectDto>> CreateFromTemplate(string key, CancellationToken ct)
    {
        var template = ProjectTemplates.All.FirstOrDefault(t => t.Key == key);
        return template is null
            ? Task.FromResult<ActionResult<ScrapingProjectDto>>(NotFound())
            : Import(template.Definition, ct);
    }

    /// <summary>Enables (or rotates) public read-only sharing of this project's items. Returns the
    /// share URL path once; only the token's hash is stored.</summary>
    [HttpPost("{id:guid}/share")]
    public async Task<ActionResult<object>> EnableShare(Guid id, CancellationToken ct)
    {
        var project = await _db.ScrapingProjects.FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == UserId, ct);
        if (project is null)
        {
            return NotFound();
        }

        var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        project.ShareTokenHash = PublicController.HashToken(token);
        _auditLogger.Record(UserId, AuditAction.Updated, "ScrapingProject", project.Id, $"{project.Name} (share link enabled)");
        await _db.SaveChangesAsync(ct);
        return new { token, path = $"/api/public/items/{token}" };
    }

    /// <summary>Disables public sharing (existing links stop working immediately).</summary>
    [HttpDelete("{id:guid}/share")]
    public async Task<IActionResult> DisableShare(Guid id, CancellationToken ct)
    {
        var project = await _db.ScrapingProjects.FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == UserId, ct);
        if (project is null)
        {
            return NotFound();
        }

        project.ShareTokenHash = null;
        _auditLogger.Record(UserId, AuditAction.Updated, "ScrapingProject", project.Id, $"{project.Name} (share link disabled)");
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Flips IsEnabled without a full update round-trip (the list rows' quick toggle).</summary>
    [HttpPost("{id:guid}/toggle-enabled")]
    public async Task<ActionResult<object>> ToggleEnabled(Guid id, CancellationToken ct)
    {
        var project = await _db.ScrapingProjects.FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == UserId, ct);
        if (project is null)
        {
            return NotFound();
        }

        project.IsEnabled = !project.IsEnabled;
        project.UpdatedAt = DateTimeOffset.UtcNow;
        _auditLogger.Record(UserId, AuditAction.Updated, "ScrapingProject", project.Id, $"{project.Name} ({(project.IsEnabled ? "enabled" : "disabled")})");
        await _db.SaveChangesAsync(ct);
        return new { project.IsEnabled };
    }

    /// <summary>Run count, item count and approximate stored bytes -- for the editor's storage strip.</summary>
    [HttpGet("{id:guid}/storage-stats")]
    public async Task<ActionResult<object>> StorageStats(Guid id, CancellationToken ct)
    {
        if (!await _db.ScrapingProjects.AnyAsync(p => p.Id == id && p.OwnerUserId == UserId, ct))
        {
            return NotFound();
        }

        var runCount = await _db.ScrapeRuns.CountAsync(r => r.ScrapingProjectId == id, ct);
        var itemCount = await _db.ScrapedItems.CountAsync(i => i.ScrapingProjectId == id, ct);
        var oldestItem = await _db.ScrapedItems.Where(i => i.ScrapingProjectId == id).MinAsync(i => (DateTimeOffset?)i.CreatedAt, ct);
        var newestItem = await _db.ScrapedItems.Where(i => i.ScrapingProjectId == id).MaxAsync(i => (DateTimeOffset?)i.CreatedAt, ct);
        // jsonb column size is a good-enough proxy for what this project actually costs to keep.
        var approxBytes = await _db.Database
            .SqlQuery<long>($@"SELECT COALESCE(SUM(pg_column_size(""Data"")), 0)::bigint AS ""Value"" FROM scraped_items WHERE ""ScrapingProjectId"" = {id}")
            .FirstOrDefaultAsync(ct);

        return new { runCount, itemCount, oldestItem, newestItem, approxBytes };
    }

    /// <summary>Requests cancellation of a running scrape run (broadcast to whichever Worker owns it).</summary>
    [HttpPost("{id:guid}/runs/{runId:guid}/cancel")]
    public async Task<IActionResult> CancelRun(
        Guid id, Guid runId, [FromServices] Weaver.Infrastructure.Realtime.IRunCancellationService cancellation, CancellationToken ct)
    {
        var run = await _db.ScrapeRuns
            .Join(_db.ScrapingProjects.Where(p => p.OwnerUserId == UserId), r => r.ScrapingProjectId, p => p.Id, (r, p) => r)
            .FirstOrDefaultAsync(r => r.Id == runId && r.ScrapingProjectId == id, ct);
        if (run is null)
        {
            return NotFound();
        }

        if (run.Status is not (RunStatus.Pending or RunStatus.Running))
        {
            return Conflict($"Run is already {run.Status}.");
        }

        await cancellation.RequestCancelAsync(runId, ct);
        return Accepted();
    }

    /// <summary>Deletes every run and scraped item for this project. Irreversible; the project config itself is untouched.</summary>
    [HttpDelete("{id:guid}/runs")]
    public async Task<IActionResult> ClearRunHistory(Guid id, CancellationToken ct)
    {
        var project = await _db.ScrapingProjects.FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == UserId, ct);
        if (project is null)
        {
            return NotFound();
        }

        // Set-based deletes -- a project can have tens of thousands of items, so don't load them.
        await _db.ScrapedItems.Where(i => i.ScrapingProjectId == id).ExecuteDeleteAsync(ct);
        await _db.ScrapeRuns.Where(r => r.ScrapingProjectId == id).ExecuteDeleteAsync(ct);

        _auditLogger.Record(UserId, AuditAction.Deleted, "ScrapingProjectRunHistory", project.Id, project.Name);
        await _db.SaveChangesAsync(ct);
        return NoContent();
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

    /// <summary>Deletes one scraped item (e.g. a junk row from a mis-tuned selector).</summary>
    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> DeleteItem(Guid id, Guid itemId, CancellationToken ct)
    {
        if (!await _db.ScrapingProjects.AnyAsync(p => p.Id == id && p.OwnerUserId == UserId, ct))
        {
            return NotFound();
        }

        var deleted = await _db.ScrapedItems.Where(i => i.Id == itemId && i.ScrapingProjectId == id).ExecuteDeleteAsync(ct);
        return deleted == 0 ? NotFound() : NoContent();
    }

    /// <summary>Full-text search across every extracted field value in this project's scraped items (Postgres jsonb-as-text ILIKE), across all runs.</summary>
    [HttpGet("{id:guid}/items/search")]
    public async Task<ActionResult<PagedResult<ScrapedItemDto>>> SearchItems(
        Guid id, [FromQuery] string q, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        if (!await _db.ScrapingProjects.AnyAsync(p => p.Id == id && p.OwnerUserId == UserId, ct))
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(q))
        {
            return new PagedResult<ScrapedItemDto>(new List<ScrapedItemDto>(), 0, page, pageSize);
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var pattern = "%" + q.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
        var query = _db.ScrapedItems.FromSqlInterpolated(
            $@"SELECT * FROM scraped_items WHERE ""ScrapingProjectId"" = {id} AND ""Data""::text ILIKE {pattern} ESCAPE '\'");

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

        var proxy = request.Proxy is { Enabled: true } p
            ? new ProxyConfig(p.Enabled, p.Protocol, p.Host, p.Port, p.Username, p.Password)
            : null;

        var result = await _testExtraction.ExtractAsync(
            request.Url, request.Mode, request.ItemSelector, fields, policy, request.ScrapingProjectId ?? Guid.Empty,
            request.RenderMode, request.CustomHeaders, proxy, ct);
        return result;
    }

    private async Task<bool> OwnsPolicyOrNullAsync(Guid? policyId, CancellationToken ct) =>
        policyId is null || await _db.RateLimitPolicies.AnyAsync(p => p.Id == policyId && p.OwnerUserId == UserId, ct);

    /// <summary>Adds new FieldSelectors via the DbSet, not project.Fields.Add -- when project is
    /// already tracked (the Update path), EF's change tracker otherwise misclassifies a new child
    /// reached only through a tracked parent's navigation as Modified instead of Added, because
    /// FieldSelector.Id defaults to a non-empty GUID. EF's own relationship fixup (matching
    /// ScrapingProjectId to the tracked parent) adds it to project.Fields automatically.</summary>
    private void ApplyFields(ScrapingProject project, List<FieldSelectorDto> fields)
    {
        foreach (var f in fields)
        {
            _db.FieldSelectors.Add(new FieldSelector
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
                TransformsJson = f.TransformsToJson(),
            });
        }
    }
}
