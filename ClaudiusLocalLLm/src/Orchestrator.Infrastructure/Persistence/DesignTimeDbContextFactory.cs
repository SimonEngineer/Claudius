using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Orchestrator.Infrastructure.Persistence;

/// <summary>Lets `dotnet ef migrations add` construct the DbContext without spinning up the full host.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrchestratorDbContext>
{
    public OrchestratorDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ORCHESTRATOR_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=orchestrator;Username=orchestrator;Password=orchestrator";

        var optionsBuilder = new DbContextOptionsBuilder<OrchestratorDbContext>()
            .UseNpgsql(connectionString);

        return new OrchestratorDbContext(optionsBuilder.Options);
    }
}
