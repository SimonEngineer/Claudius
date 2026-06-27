using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orchestrator.Infrastructure.Discord;
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
        services.Configure<DiscordOptions>(configuration.GetSection(DiscordOptions.SectionName));

        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<GitWorktreeService>();
        services.AddSingleton<IRunCancellationRegistry, RunCancellationRegistry>();
        // Singleton: failure counts must be shared across every run for the breaker to mean
        // anything -- a scoped instance would reset on every task.
        services.AddSingleton<IModelCircuitBreaker, ModelCircuitBreaker>();
        services.AddScoped<ClaudeCodeAdapter>();
        services.AddScoped<AiderAdapter>();
        services.AddScoped<TaskClaimingService>();
        services.AddScoped<TaskLifecycleService>();
        services.AddScoped<TaskRunnerJob>();
        services.AddScoped<SchedulerTickJob>();
        services.AddHostedService<SchedulerTickHostedService>();

        // Singleton because it owns the one persistent Discord gateway connection; registered
        // both as itself (so DiscordEventBroadcaster can call SendNotificationAsync) and as the
        // hosted service that drives it, sharing the same instance.
        services.AddSingleton<DiscordBotService>();
        services.AddHostedService<DiscordBotService>(sp => sp.GetRequiredService<DiscordBotService>());
        services.AddScoped<DiscordEventBroadcaster>();

        return services;
    }
}
