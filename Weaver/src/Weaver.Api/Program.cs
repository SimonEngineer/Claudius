using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Weaver.Api.Realtime;
using Weaver.Infrastructure;
using Weaver.Infrastructure.Auth;
using Weaver.Infrastructure.Persistence;
using Weaver.Scraping;
using Weaver.Scraping.Fetching;
using Weaver.Scraping.Proxy;
using Weaver.Workflows;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddHostedService<RunStatusRelayService>();

builder.Services.AddWeaverInfrastructure(builder.Configuration);
builder.Services.AddWeaverWorkflows(builder.Configuration);

builder.Services.AddHttpClient<HttpPageFetcher>();
builder.Services.AddSingleton<PlaywrightPageFetcher>();
builder.Services.AddScoped<IPageFetcherFactory, PageFetcherFactory>();
builder.Services.AddScoped<PageProxyService>();
builder.Services.AddScoped<ScraperEngine>();
builder.Services.AddScoped<ScrapeRunner>();
builder.Services.AddScoped<TestExtractionService>();
builder.Services.AddSingleton<RateLimitGate>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"])
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// A request authenticates with either a JWT (interactive frontend login) or a personal API key
// (server-to-server automation, Authorization: Bearer wvr_...); the policy scheme below picks
// which underlying handler actually runs based on the token's shape, so every existing endpoint
// (already just checking User.GetUserId()) works unmodified with either credential type.
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Smart";
    options.DefaultChallengeScheme = "Smart";
})
    .AddPolicyScheme("Smart", "JWT or API Key", policyOptions =>
    {
        policyOptions.ForwardDefaultSelector = context =>
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader["Bearer ".Length..].Trim()
                : authHeader;
            return token.StartsWith(Weaver.Infrastructure.Auth.ApiKeyService.KeyPrefixLiteral, StringComparison.Ordinal)
                ? Weaver.Api.Auth.ApiKeyAuthenticationHandler.SchemeName
                : JwtBearerDefaults.AuthenticationScheme;
        };
    })
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, Weaver.Api.Auth.ApiKeyAuthenticationHandler>(
        Weaver.Api.Auth.ApiKeyAuthenticationHandler.SchemeName, null)
    .AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptions) =>
    {
        var options = jwtOptions.Value;
        bearerOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
        };
        bearerOptions.Events = new JwtBearerEvents
        {
            // The picker iframe's `src` GET can't carry an Authorization header, so /api/proxy
            // accepts the token via ?access_token= instead; the SignalR JS client does the same
            // thing for its own transports (WebSocket/SSE can't set arbitrary headers either),
            // sending the token the same way when configured with accessTokenFactory.
            OnMessageReceived = context =>
            {
                var isRealtimeHub = context.Request.Path.StartsWithSegments("/hubs");
                if ((context.Request.Path.StartsWithSegments("/api/proxy") || isRealtimeHub)
                    && context.Request.Query.TryGetValue("access_token", out var token))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            },
        };
    });

// Secure by default: every endpoint requires a valid JWT unless explicitly marked [AllowAnonymous]
// (auth register/login, and the webhook endpoint, which is authenticated by its own per-node secret instead).
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Blunts credential-stuffing/brute-force attempts against login and registration: 10 attempts
// per minute per client IP, rejecting the rest outright (no queueing) rather than slowing them down.
builder.Services.AddRateLimiter(options =>
{
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync("Too many attempts. Please wait a moment and try again.", cancellationToken);
    };

    options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

var app = builder.Build();

PlaywrightBootstrap.EnsureBrowsersInstalledIfEnabled(app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("PlaywrightBootstrap"));

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WeaverDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapHub<RunStatusHub>("/hubs/run-status");
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

public partial class Program;
