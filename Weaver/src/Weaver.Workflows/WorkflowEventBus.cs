using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Weaver.Domain;
using Weaver.Infrastructure.Persistence;
using Weaver.Scraping;

namespace Weaver.Workflows;

/// <summary>
/// Lets any code -- the scraper, a Code Block node, a controller action -- raise a named event
/// that fans out to every enabled workflow with a matching "trigger.event" node, without either
/// side knowing about the other. This is also the "usable from code, not just the GUI" hook:
/// calling PublishAsync("scrape.item.changed", ...) from a plain C# method has the exact same
/// effect as a workflow author dragging an Event Trigger block onto the canvas.
/// </summary>
public class WorkflowEventBus : IWorkflowEventPublisher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWorkflowExecutionEngine _engine;

    public WorkflowEventBus(IServiceScopeFactory scopeFactory, IWorkflowExecutionEngine engine)
    {
        _scopeFactory = scopeFactory;
        _engine = engine;
    }

    public async Task PublishAsync(string eventName, object payload, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeaverDbContext>();

        var eventTriggerNodes = await db.WorkflowNodes
            .Where(n => n.Type == "trigger.event")
            .ToListAsync(cancellationToken);

        if (eventTriggerNodes.Count == 0)
        {
            return;
        }

        var workflowIds = eventTriggerNodes.Select(n => n.WorkflowId).Distinct().ToList();
        var enabledWorkflowIds = await db.Workflows
            .Where(w => workflowIds.Contains(w.Id) && w.IsEnabled)
            .Select(w => w.Id)
            .ToListAsync(cancellationToken);

        var payloadJson = JsonSerializer.SerializeToNode(payload);

        foreach (var node in eventTriggerNodes)
        {
            if (!enabledWorkflowIds.Contains(node.WorkflowId))
            {
                continue;
            }

            var configuredEventName = ExtractEventName(node.ConfigJson);
            if (!string.Equals(configuredEventName, eventName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            await _engine.StartRunFromNodeAsync(node.WorkflowId, node.Id, TriggerKind.Event, payloadJson, cancellationToken);
        }
    }

    private static string? ExtractEventName(string configJson)
    {
        try
        {
            var node = JsonNode.Parse(configJson);
            return node?["eventName"]?.GetValue<string>();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
