using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Infrastructure.Persistence;

namespace Orchestrator.Tests.Scheduling;

/// <summary>
/// Sqlite in-memory connection kept open for the test's lifetime, so the raw
/// ExecuteSqlInterpolatedAsync claim query in TaskClaimingService (real production code, not a
/// fake) runs against a real ADO.NET provider instead of EF's InMemory provider, which doesn't
/// support raw SQL at all. Disposing the connection drops the database.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;
    public OrchestratorDbContext Context { get; }

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<OrchestratorDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new OrchestratorDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
