using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Weaver.Domain;
using Weaver.Infrastructure.Persistence;
using Weaver.Infrastructure.Queue;

namespace Weaver.Workflows.Nodes;

/// <summary>
/// Config: { "scrapingProjectId": "<guid>" }. Enqueues onto the same distributed job queue as
/// the "Run now" button and cron-scraped projects (rather than calling the scraper inline), so a
/// burst of workflow runs against the same project still goes through one shared, rate-limited,
/// single-worker-at-a-time pipeline instead of each run spinning up its own uncoordinated scrape.
/// Polls the resulting ScrapeJob until a worker completes it, then forwards its results downstream.
/// </summary>
public class ScrapeActionNode : INodeHandler
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromMinutes(10);

    public string Type => "action.scrape";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var idText = context.Config?["scrapingProjectId"]?.GetValue<string>();
        if (!Guid.TryParse(idText, out var projectId))
        {
            return NodeExecutionResult.Fail("Scrape action node is missing a valid 'scrapingProjectId' in config.");
        }

        var db = context.Services.GetRequiredService<WeaverDbContext>();
        var queue = context.Services.GetRequiredService<IScrapeJobQueue>();

        var projectExists = await db.ScrapingProjects.AnyAsync(p => p.Id == projectId, context.CancellationToken);
        if (!projectExists)
        {
            return NodeExecutionResult.Fail($"Scraping project {projectId} not found.");
        }

        var job = new ScrapeJob
        {
            ScrapingProjectId = projectId,
            TriggeredBy = TriggerKind.Code,
            WorkflowRunId = context.WorkflowRunId,
            Status = ScrapeJobStatus.Queued,
        };
        db.ScrapeJobs.Add(job);
        await db.SaveChangesAsync(context.CancellationToken);

        var streamId = await queue.EnqueueAsync(
            new ScrapeJobMessage(job.Id, projectId, TriggerKind.Code, context.WorkflowRunId),
            context.CancellationToken);
        job.StreamMessageId = streamId;
        await db.SaveChangesAsync(context.CancellationToken);

        context.Log($"scrape action: queued job {job.Id} for project {projectId}; waiting for a worker to pick it up");

        var finished = await WaitForCompletionAsync(db, job.Id, context.CancellationToken);
        if (finished is null)
        {
            return NodeExecutionResult.Fail($"Timed out after {WaitTimeout.TotalMinutes:0}m waiting for scrape job {job.Id}. Is a Weaver.Worker instance running?");
        }

        if (finished.ScrapeRunId is null)
        {
            return NodeExecutionResult.Fail(finished.ErrorMessage ?? "Scrape job failed before producing a run.");
        }

        var run = await db.ScrapeRuns.AsNoTracking().FirstAsync(r => r.Id == finished.ScrapeRunId, context.CancellationToken);
        var items = await db.ScrapedItems.AsNoTracking()
            .Where(i => i.ScrapeRunId == run.Id)
            .Select(i => i.Data)
            .ToListAsync(context.CancellationToken);

        var output = new JsonObject
        {
            ["runId"] = run.Id.ToString(),
            ["status"] = run.Status.ToString(),
            ["pagesCrawled"] = run.PagesCrawled,
            ["itemsFound"] = run.ItemsFound,
            ["itemsChanged"] = run.ItemsChanged,
            ["errorMessage"] = run.ErrorMessage,
            ["items"] = JsonSerializer.SerializeToNode(items),
        };

        context.Log($"scrape action: project={projectId} status={run.Status} items={run.ItemsFound} changed={run.ItemsChanged}");

        return run.Status == RunStatus.Succeeded
            ? NodeExecutionResult.Ok(output)
            : new NodeExecutionResult(false, output, run.ErrorMessage ?? "Scrape run failed.");
    }

    private static async Task<ScrapeJob?> WaitForCompletionAsync(WeaverDbContext db, Guid jobId, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.Add(WaitTimeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            // AsNoTracking is essential here: the job row is updated by a *different* process (a
            // Weaver.Worker instance), so a tracked query would just keep returning this
            // DbContext's stale cached copy instead of re-reading the current row from Postgres.
            var current = await db.ScrapeJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
            if (current is { Status: ScrapeJobStatus.Completed or ScrapeJobStatus.Failed })
            {
                return current;
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        return null;
    }
}
