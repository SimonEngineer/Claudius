using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Weaver.Infrastructure.Persistence;

/// <summary>Lets `dotnet ef migrations add` run without a fully configured host.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<WeaverDbContext>
{
    public WeaverDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("WEAVER_CONNECTION_STRING")
            ?? "Host=localhost;Database=weaver;Username=weaver;Password=weaver";

        var builder = new DbContextOptionsBuilder<WeaverDbContext>();
        builder.UseNpgsql(connectionString);
        return new WeaverDbContext(builder.Options);
    }
}
