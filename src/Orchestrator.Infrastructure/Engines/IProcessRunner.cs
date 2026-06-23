namespace Orchestrator.Infrastructure.Engines;

public record ProcessSpec(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string>? EnvironmentVariables = null);

public record ProcessOutcome(int ExitCode);

/// <summary>
/// Thin wrapper around starting an external process and streaming its stdout line by line.
/// Exists as an interface so engine adapters can be unit tested without spawning real
/// `claude`/`aider` binaries.
/// </summary>
public interface IProcessRunner
{
    Task<ProcessOutcome> RunAsync(
        ProcessSpec spec,
        Func<string, Task> onStdoutLine,
        Func<string, Task> onStderrLine,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
