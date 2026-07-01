using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Weaver.Infrastructure.Auth;
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

        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        var signingKey = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            // Dev/first-run convenience: tokens issued with this key stop working the next time
            // the process restarts. Set Jwt:SigningKey (or the JWT__SIGNINGKEY env var) to a
            // real secret for any deployment where that matters.
            Console.WriteLine("WARNING: Jwt:SigningKey is not configured -- generating an ephemeral key for this process. Existing logins will be invalidated on every restart. Set Jwt:SigningKey for production use.");
            var ephemeralKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            services.PostConfigure<JwtOptions>(o => o.SigningKey = ephemeralKey);
        }

        services.AddScoped<IPasswordHashingService, PasswordHashingService>();
        services.AddScoped<ITokenService, TokenService>();

        return services;
    }
}
