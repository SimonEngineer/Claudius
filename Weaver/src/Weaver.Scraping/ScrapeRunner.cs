using Microsoft.EntityFrameworkCore;
using Weaver.Domain;
using Weaver.Infrastructure.Persistence;
using Weaver.Infrastructure.Realtime;

namespace Weaver.Scraping;

public interface IWorkflowEventPublisher
{
    Task PublishAsync(string eventName, object payload, CancellationToken cancellationToken = default);
}

/// <summary>
/// Runs a ScrapingProject end-to-end: executes the engine, persists a ScrapeRun + ScrapedItems,
/// diffs against the previous run to detect changed/new items, and raises workflow events so
/// automations (e.g. "email me when price drops") can react without polling the database.
/// </summary>
public class ScrapeRunner
{
    public const string ItemFoundEvent = "scrape.item.found";
    public const string ItemChangedEvent = "scrape.item.changed";
    public const string RunCompletedEvent = "scrape.run.completed";
    public const string RunFailedEvent = "scrape.run.failed";

    private readonly WeaverDbContext _db;
    private readonly ScraperEngine _engine;
    private readonly IWorkflowEventPublisher _events;
    private readonly IRunStatusPublisher _runStatus;

    public ScrapeRunner(WeaverDbContext db, ScraperEngine engine, IWorkflowEventPublisher events, IRunStatusPublisher runStatus)
    {
        _db = db;
        _engine = engine;
        _events = events;
        _runStatus = runStatus;
    }

    public async Task<ScrapeRun> ExecuteAsync(Guid scrapingProjectId, TriggerKind triggeredBy, Guid? workflowRunId, CancellationToken cancellationToken = default)
    {
        var project = await _db.ScrapingProjects
            .Include(p => p.Fields)
            .Include(p => p.RateLimitPolicy)
            .FirstOrDefaultAsync(p => p.Id == scrapingProjectId, cancellationToken)
            ?? throw new InvalidOperationException($"Scraping project {scrapingProjectId} not found.");

        var run = new ScrapeRun
        {
            ScrapingProjectId = project.Id,
            TriggeredBy = triggeredBy,
            WorkflowRunId = workflowRunId,
            Status = RunStatus.Running,
            StartedAt = DateTimeOffset.UtcNow,
        };
        _db.ScrapeRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);
        await _runStatus.PublishAsync(project.OwnerUserId, "scrape", run.Id, run.Status.ToString(), cancellationToken);

        var result = await _engine.RunAsync(project, cancellationToken);

        run.PagesCrawled = result.PagesCrawled;
        run.ItemsFound = result.Items.Count;
        run.CompletedAt = DateTimeOffset.UtcNow;

        if (result.ErrorMessage is not null && result.Items.Count == 0)
        {
            run.Status = RunStatus.Failed;
            run.ErrorMessage = result.ErrorMessage;
            await _db.SaveChangesAsync(cancellationToken);
            await _runStatus.PublishAsync(project.OwnerUserId, "scrape", run.Id, run.Status.ToString(), cancellationToken);
            await _events.PublishAsync(RunFailedEvent, new { ScrapingProjectId = project.Id, RunId = run.Id, result.ErrorMessage }, cancellationToken);
            return run;
        }

        var changedCount = await PersistItemsAndDetectChangesAsync(project, run, result, cancellationToken);
        run.ItemsChanged = changedCount;
        run.Status = result.ErrorMessage is null ? RunStatus.Succeeded : RunStatus.Failed;
        run.ErrorMessage = result.ErrorMessage;
        await _db.SaveChangesAsync(cancellationToken);
        await _runStatus.PublishAsync(project.OwnerUserId, "scrape", run.Id, run.Status.ToString(), cancellationToken);

        await _events.PublishAsync(RunCompletedEvent, new
        {
            ScrapingProjectId = project.Id,
            RunId = run.Id,
            result.PagesCrawled,
            ItemsFound = result.Items.Count,
            ItemsChanged = changedCount
        }, cancellationToken);

        return run;
    }

    private async Task<int> PersistItemsAndDetectChangesAsync(ScrapingProject project, ScrapeRun run, ScrapeResult result, CancellationToken cancellationToken)
    {
        var changedCount = 0;
        var ordinalByUrl = new Dictionary<string, int>();

        foreach (var extracted in result.Items)
        {
            var ordinal = ordinalByUrl.GetValueOrDefault(extracted.SourceUrl, 0);
            ordinalByUrl[extracted.SourceUrl] = ordinal + 1;

            var itemKey = ItemHasher.ComputeItemKey(extracted.Data, project.Fields, extracted.SourceUrl, ordinal);
            var contentHash = ItemHasher.ComputeContentHash(extracted.Data);

            var previous = await _db.ScrapedItems
                .Where(i => i.ScrapingProjectId == project.Id && i.ItemKey == itemKey)
                .OrderByDescending(i => i.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            var item = new ScrapedItem
            {
                ScrapeRunId = run.Id,
                ScrapingProjectId = project.Id,
                SourceUrl = extracted.SourceUrl,
                ItemKey = itemKey,
                Data = extracted.Data,
                ContentHash = contentHash,
            };
            _db.ScrapedItems.Add(item);

            if (previous is null)
            {
                await _events.PublishAsync(ItemFoundEvent, new { ScrapingProjectId = project.Id, RunId = run.Id, Item = extracted.Data }, cancellationToken);
            }
            else if (previous.ContentHash != contentHash)
            {
                changedCount++;
                await _events.PublishAsync(ItemChangedEvent, new
                {
                    ScrapingProjectId = project.Id,
                    RunId = run.Id,
                    Previous = previous.Data,
                    Current = extracted.Data
                }, cancellationToken);
            }
        }

        return changedCount;
    }
}
