using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Weaver.Infrastructure.Persistence;
using Weaver.Infrastructure.Queue;
using Weaver.Infrastructure.RateLimiting;

namespace Weaver.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWeaverInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? configuration["WEAVER_CONNECTION_STRING"]
            ?? "Host=localhost;Database=weaver;Username=weaver;Password=weaver";

        services.AddDbContext<WeaverDbContext>(options => options.UseNpgsql(connectionString));

        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? configuration["WEAVER_REDIS_CONNECTION_STRING"]
            ?? "localhost:6379";

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnectionString));

        services.AddSingleton<IDistributedRateLimiter, RedisTokenBucketRateLimiter>();
        services.AddSingleton<IScrapeJobQueue, RedisStreamScrapeJobQueue>();

        return services;
    }
}
