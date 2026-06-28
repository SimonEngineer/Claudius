using System.Text.Json;
using Orchestrator.Infrastructure.Workflows;
using Xunit;

namespace Orchestrator.Tests.Workflows;

public class WorkflowTemplatingTests
{
    [Fact]
    public void Render_SubstitutesNestedPath()
    {
        using var doc = JsonDocument.Parse("""{"task":{"title":"Fix the bug"}}""");

        var result = WorkflowTemplating.Render("Task done: {{trigger.task.title}}", doc.RootElement);

        Assert.Equal("Task done: Fix the bug", result);
    }

    [Fact]
    public void Render_LeavesPlaceholderUnchanged_WhenPathMissing()
    {
        using var doc = JsonDocument.Parse("""{"task":{"title":"Fix the bug"}}""");

        var result = WorkflowTemplating.Render("{{trigger.nope}}", doc.RootElement);

        Assert.Equal("{{trigger.nope}}", result);
    }

    [Fact]
    public void Render_WholePayload_WhenNoPathGiven()
    {
        using var doc = JsonDocument.Parse("""{"a":1}""");

        var result = WorkflowTemplating.Render("{{trigger}}", doc.RootElement);

        Assert.Contains("\"a\":1", result);
    }
}
