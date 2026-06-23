namespace Orchestrator.Domain.Engines;

/// <summary>
/// Adapter contract the scheduler dispatches Runs through. Implementations wrap an
/// external CLI tool (Claude Code for the supervisor lane, Aider for the worker lane)
/// as a subprocess and translate its output into EngineEvents.
/// </summary>
public interface IEngineAdapter
{
    EngineType EngineType { get; }

    /// <summary>
    /// Starts the run, streaming events as they happen (each is persisted + broadcast by the
    /// caller). Completes with the final EngineResult once the engine process exits.
    /// </summary>
    Task<EngineResult> RunAsync(
        RunContext context,
        Func<EngineEvent, Task> onEvent,
        CancellationToken cancellationToken);
}
