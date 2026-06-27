using Microsoft.Extensions.Logging;

namespace Orchestrator.Infrastructure.Engines;

/// <summary>
/// Gives every decomposed plan (a root task and all its child steps) its own git worktree and
/// branch, so concurrent tasks against the same repo never trample each other's working tree.
/// The worktree path/branch are pure functions of (repoPath, rootTaskId) -- nothing is persisted,
/// so a crash/restart can always re-derive (or re-create, if missing) the same location.
/// </summary>
public class GitWorktreeService(IProcessRunner processRunner, ILogger<GitWorktreeService> logger)
{
    public string GetWorktreePath(string repoPath, Guid rootTaskId) =>
        Path.Combine(repoPath, ".orchestrator-worktrees", rootTaskId.ToString("N"));

    public string GetBranchName(Guid rootTaskId) => $"orchestrator/{rootTaskId:N}";

    /// <summary>Creates the worktree/branch if they don't already exist; idempotent.</summary>
    public async Task<string> EnsureWorktreeAsync(string repoPath, Guid rootTaskId, CancellationToken ct)
    {
        var worktreePath = GetWorktreePath(repoPath, rootTaskId);
        if (Directory.Exists(worktreePath))
        {
            return worktreePath;
        }

        var branch = GetBranchName(rootTaskId);
        logger.LogInformation("Creating git worktree {Path} on branch {Branch} for root task {RootTaskId}",
            worktreePath, branch, rootTaskId);

        var result = await processRunner.RunAsync(
            new ProcessSpec("git", new[] { "worktree", "add", "-b", branch, worktreePath }, repoPath),
            onStdoutLine: _ => Task.CompletedTask,
            onStderrLine: line => { logger.LogWarning("git worktree add: {Line}", line); return Task.CompletedTask; },
            timeout: TimeSpan.FromMinutes(2),
            cancellationToken: ct);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to create git worktree at {worktreePath} (exit code {result.ExitCode}).");
        }

        return worktreePath;
    }

    /// <summary>
    /// Discards all uncommitted edits and untracked files in a task's worktree, reverting it back
    /// to the branch's last commit -- the rollback affordance for when an agent's run went off the
    /// rails and the changes shouldn't be kept. Does not touch commit history: if the agent itself
    /// committed along the way, those commits survive (this only undoes uncommitted work).
    /// </summary>
    public async Task DiscardChangesAsync(string repoPath, Guid rootTaskId, CancellationToken ct)
    {
        var worktreePath = GetWorktreePath(repoPath, rootTaskId);
        if (!Directory.Exists(worktreePath))
        {
            throw new InvalidOperationException($"No worktree found at {worktreePath} for root task {rootTaskId}.");
        }

        logger.LogInformation("Discarding uncommitted changes in worktree {Path} for root task {RootTaskId}",
            worktreePath, rootTaskId);

        async Task RunGit(string[] args)
        {
            var result = await processRunner.RunAsync(
                new ProcessSpec("git", args, worktreePath),
                onStdoutLine: _ => Task.CompletedTask,
                onStderrLine: line => { logger.LogWarning("git {Args}: {Line}", string.Join(' ', args), line); return Task.CompletedTask; },
                timeout: TimeSpan.FromMinutes(1),
                cancellationToken: ct);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"git {string.Join(' ', args)} failed in {worktreePath} (exit code {result.ExitCode}).");
            }
        }

        await RunGit(["reset", "--hard", "HEAD"]);
        await RunGit(["clean", "-fd"]);
    }

    public async Task RemoveWorktreeAsync(string repoPath, Guid rootTaskId, CancellationToken ct)
    {
        var worktreePath = GetWorktreePath(repoPath, rootTaskId);
        if (!Directory.Exists(worktreePath))
        {
            return;
        }

        await processRunner.RunAsync(
            new ProcessSpec("git", new[] { "worktree", "remove", "--force", worktreePath }, repoPath),
            onStdoutLine: _ => Task.CompletedTask,
            onStderrLine: line => { logger.LogWarning("git worktree remove: {Line}", line); return Task.CompletedTask; },
            timeout: TimeSpan.FromMinutes(1),
            cancellationToken: ct);
    }
}
