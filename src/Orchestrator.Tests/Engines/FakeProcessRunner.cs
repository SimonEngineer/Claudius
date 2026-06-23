using Orchestrator.Infrastructure.Engines;

namespace Orchestrator.Tests.Engines;

/// <summary>Replays canned stdout/stderr lines instead of spawning a real process, so adapter
/// parsing logic can be tested without `claude`/`aider` binaries being installed.</summary>
public class FakeProcessRunner(IReadOnlyList<string> stdoutLines, int exitCode = 0) : IProcessRunner
{
    public ProcessSpec? LastSpec { get; private set; }

    public async Task<ProcessOutcome> RunAsync(
        ProcessSpec spec,
        Func<string, Task> onStdoutLine,
        Func<string, Task> onStderrLine,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        LastSpec = spec;
        foreach (var line in stdoutLines)
        {
            await onStdoutLine(line);
        }
        return new ProcessOutcome(exitCode);
    }
}
