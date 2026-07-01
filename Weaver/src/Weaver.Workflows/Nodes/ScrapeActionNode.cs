using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Weaver.Domain;
using Weaver.Scraping;

namespace Weaver.Workflows.Nodes;

/// <summary>Config: { "scrapingProjectId": "<guid>" }. Runs the project synchronously and forwards its results downstream.</summary>
public class ScrapeActionNode : INodeHandler
{
    public string Type => "action.scrape";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var idText = context.Config?["scrapingProjectId"]?.GetValue<string>();
        if (!Guid.TryParse(idText, out var projectId))
        {
            return NodeExecutionResult.Fail("Scrape action node is missing a valid 'scrapingProjectId' in config.");
        }

        var runner = context.Services.GetRequiredService<ScrapeRunner>();
        var run = await runner.ExecuteAsync(projectId, TriggerKind.Code, context.WorkflowRunId, context.CancellationToken);

        var items = context.Services.GetRequiredService<Weaver.Infrastructure.Persistence.WeaverDbContext>()
            .ScrapedItems.Where(i => i.ScrapeRunId == run.Id).ToList();

        var output = new JsonObject
        {
            ["runId"] = run.Id.ToString(),
            ["status"] = run.Status.ToString(),
            ["pagesCrawled"] = run.PagesCrawled,
            ["itemsFound"] = run.ItemsFound,
            ["itemsChanged"] = run.ItemsChanged,
            ["errorMessage"] = run.ErrorMessage,
            ["items"] = JsonSerializer.SerializeToNode(items.Select(i => i.Data)),
        };

        context.Log($"scrape action: project={projectId} status={run.Status} items={run.ItemsFound} changed={run.ItemsChanged}");

        return run.Status == RunStatus.Succeeded
            ? NodeExecutionResult.Ok(output)
            : new NodeExecutionResult(false, output, run.ErrorMessage ?? "Scrape run failed.");
    }
}
