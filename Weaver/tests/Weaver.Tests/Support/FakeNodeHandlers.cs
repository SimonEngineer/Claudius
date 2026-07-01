using System.Text.Json.Nodes;
using Weaver.Infrastructure.Security;
using Weaver.Workflows;

namespace Weaver.Tests.Support;

/// <summary>Echoes its config's "value" (if set) or otherwise its input, unchanged -- a stand-in
/// for any ordinary action node whose exact side effect doesn't matter to the test.</summary>
public class EchoNodeHandler : INodeHandler
{
    public string Type => "test.echo";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var value = context.Config?["value"];
        return Task.FromResult(NodeExecutionResult.Ok(value?.DeepClone() ?? context.Input?.DeepClone()));
    }
}

/// <summary>Always routes to whichever branch its config names, like a pre-decided Condition node.</summary>
public class BranchNodeHandler : INodeHandler
{
    public string Type => "test.branch";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var branch = context.Config?["takeBranch"]?.GetValue<string>() ?? "true";
        return Task.FromResult(NodeExecutionResult.Ok(context.Input?.DeepClone(), branch));
    }
}

/// <summary>Fails on every attempt -- for exercising error-edge routing and unhandled-failure behavior.</summary>
public class AlwaysFailNodeHandler : INodeHandler
{
    public string Type => "test.alwaysFail";
    public int CallCount { get; private set; }

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        CallCount++;
        return Task.FromResult(NodeExecutionResult.Fail("intentional failure for testing"));
    }
}

/// <summary>Fails the first <see cref="FailuresBeforeSuccess"/> calls, then succeeds -- for exercising retry.</summary>
public class FlakyNodeHandler : INodeHandler
{
    public string Type => "test.flaky";
    public int FailuresBeforeSuccess { get; set; } = 2;
    public int CallCount { get; private set; }

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        CallCount++;
        if (CallCount <= FailuresBeforeSuccess)
        {
            return Task.FromResult(NodeExecutionResult.Fail($"flaky failure #{CallCount}"));
        }

        return Task.FromResult(NodeExecutionResult.Ok(JsonValue.Create("recovered")));
    }
}

/// <summary>Test double for the real Data-Protection-backed protector -- the execution engine tests
/// care about graph scheduling, not encryption, so config passes through untouched.</summary>
public class PassthroughSensitiveConfigProtector : ISensitiveConfigProtector
{
    public string EncryptForStorage(string nodeType, string configJson) => configJson;
    public string DecryptForUse(string nodeType, string configJson) => configJson;
    public string RedactForExport(string nodeType, string configJson) => configJson;
}
