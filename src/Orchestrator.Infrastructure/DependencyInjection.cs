using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orchestrator.Infrastructure.Engines;
using Orchestrator.Infrastructure.Persistence;
using Orchestrator.Infrastructure.Scheduling;

namespace Orchestrator.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrchestratorInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Orchestrator")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:Orchestrator configuration.");

        services.AddDbContext<OrchestratorDbContext>(opts => opts.UseNpgsql(connectionString));

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString)));

        services.Configure<SchedulerOptions>(configuration.GetSection(SchedulerOptions.SectionName));

        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddScoped<ClaudeCodeAdapter>();
        services.AddScoped<AiderAdapter>();
        services.AddScoped<TaskClaimingService>();
        services.AddScoped<TaskRunnerJob>();
        services.AddScoped<SchedulerTickJob>();
        services.AddHostedService<SchedulerTickHostedService>();

        return services;
    }
}
