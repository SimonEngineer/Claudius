using Microsoft.Extensions.Logging.Abstractions;
using Orchestrator.Infrastructure.Engines;
using Xunit;

namespace Orchestrator.Tests.Engines;

public class GitWorktreeServiceTests : IDisposable
{
    private readonly string _repoPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_repoPath))
        {
            Directory.Delete(_repoPath, recursive: true);
        }
    }

    [Fact]
    public async Task DiscardChangesAsync_Throws_WhenWorktreeDoesNotExist()
    {
        var service = new GitWorktreeService(new FakeProcessRunner([]), NullLogger<GitWorktreeService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DiscardChangesAsync(_repoPath, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task DiscardChangesAsync_RunsResetThenClean_WhenWorktreeExists()
    {
        var rootTaskId = Guid.NewGuid();
        var service = new GitWorktreeService(new FakeProcessRunner([]), NullLogger<GitWorktreeService>.Instance);
        Directory.CreateDirectory(service.GetWorktreePath(_repoPath, rootTaskId));

        var fakeRunner = new FakeProcessRunner([]);
        var serviceWithFake = new GitWorktreeService(fakeRunner, NullLogger<GitWorktreeService>.Instance);

        await serviceWithFake.DiscardChangesAsync(_repoPath, rootTaskId, CancellationToken.None);

        // FakeProcessRunner only remembers the last call; DiscardChangesAsync runs reset then
        // clean, so the last command observed should be the clean.
        Assert.Equal(["clean", "-fd"], fakeRunner.LastSpec!.Arguments);
    }

    [Fact]
    public async Task DiscardChangesAsync_Throws_WhenGitCommandFails()
    {
        var rootTaskId = Guid.NewGuid();
        var probe = new GitWorktreeService(new FakeProcessRunner([]), NullLogger<GitWorktreeService>.Instance);
        Directory.CreateDirectory(probe.GetWorktreePath(_repoPath, rootTaskId));

        var failingRunner = new FakeProcessRunner([], exitCode: 1);
        var service = new GitWorktreeService(failingRunner, NullLogger<GitWorktreeService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DiscardChangesAsync(_repoPath, rootTaskId, CancellationToken.None));
    }
}
