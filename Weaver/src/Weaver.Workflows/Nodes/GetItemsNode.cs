using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Weaver.Infrastructure.Persistence;

namespace Weaver.Workflows.Nodes;

/// <summary>
/// Config: { "scrapingProjectId": "...", "limit": 50 }. Loads the newest stored items of one of
/// the owner's scraping projects into the workflow as an array of field-value objects -- so a
/// workflow can act on already-scraped data (digest emails, aggregations) without re-scraping.
/// The project must belong to the same user as the workflow being run.
/// </summary>
public class GetItemsNode : INodeHandler
{
    public string Type => "action.getItems";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var projectIdText = context.Config?["scrapingProjectId"]?.GetValue<string>();
        var limit = Math.Clamp(context.Config?["limit"]?.GetValue<int>() ?? 50, 1, 500);

        if (!Guid.TryParse(projectIdText, out var projectId))
        {
            return NodeExecutionResult.Fail("Get Items node is missing a valid 'scrapingProjectId' in config.");
        }

        var db = context.Services.GetRequiredService<WeaverDbContext>();

        // The config is owner-authored, but never trust a raw id: the referenced project must
        // belong to the same user that owns the workflow this run executes.
        var ownerUserId = await db.WorkflowRuns
            .Where(r => r.Id == context.WorkflowRunId)
            .Join(db.Workflows, r => r.WorkflowId, w => w.Id, (r, w) => w.OwnerUserId)
            .FirstOrDefaultAsync(context.CancellationToken);

        var project = await db.ScrapingProjects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId && p.OwnerUserId == ownerUserId, context.CancellationToken);
        if (project is null)
        {
            return NodeExecutionResult.Fail("Get Items node's project was not found (or belongs to another user).");
        }

        var items = await db.ScrapedItems.AsNoTracking()
            .Where(i => i.ScrapingProjectId == projectId)
            .OrderByDescending(i => i.CreatedAt)
            .Take(limit)
            .ToListAsync(context.CancellationToken);

        var array = new JsonArray();
        foreach (var item in items)
        {
            var node = JsonSerializer.SerializeToNode(item.Data) as JsonObject ?? new JsonObject();
            node["_sourceUrl"] = item.SourceUrl;
            node["_scrapedAt"] = item.CreatedAt.ToString("O");
            array.Add(node);
        }

        context.Log($"getItems: loaded {array.Count} item(s) from \"{project.Name}\"");
        return NodeExecutionResult.Ok(array);
    }
}
