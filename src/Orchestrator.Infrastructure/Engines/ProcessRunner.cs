using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Orchestrator.Infrastructure.Engines;

/// <summary>
/// Real process runner. Local-model-backed engine runs can legitimately take many minutes,
/// so the timeout is per-call and configurable rather than a short global default -- the
/// process is only killed if it exceeds `timeout` with no exit, not based on per-line gaps.
/// </summary>
public class ProcessRunner(ILogger<ProcessRunner> logger) : IProcessRunner
{
    public async Task<ProcessOutcome> RunAsync(
        ProcessSpec spec,
        Func<string, Task> onStdoutLine,
        Func<string, Task> onStderrLine,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = spec.FileName,
            WorkingDirectory = spec.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in spec.Arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        if (spec.EnvironmentVariables is not null)
        {
            foreach (var (key, value) in spec.EnvironmentVariables)
            {
                startInfo.Environment[key] = value;
            }
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        logger.LogInformation("Starting process {FileName} in {WorkingDirectory} with timeout {Timeout}",
            spec.FileName, spec.WorkingDirectory, timeout);

        process.Start();

        var stdoutTask = PumpAsync(process.StandardOutput, onStdoutLine, linkedCts.Token);
        var stderrTask = PumpAsync(process.StandardError, onStderrLine, linkedCts.Token);

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            if (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Process {FileName} timed out after {Timeout}", spec.FileName, timeout);
                throw new TimeoutException($"Process '{spec.FileName}' exceeded timeout of {timeout}.");
            }
            throw;
        }

        await Task.WhenAll(stdoutTask, stderrTask);

        return new ProcessOutcome(process.ExitCode);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // best-effort; process may have exited between the check and the kill.
        }
    }

    private static async Task PumpAsync(StreamReader reader, Func<string, Task> onLine, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(token);
            if (line is null)
            {
                break;
            }
            await onLine(line);
        }
    }
}
