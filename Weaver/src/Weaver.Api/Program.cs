using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
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

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
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
            // uniquely accepts the token via ?access_token= instead (same pattern ASP.NET Core
            // uses for SignalR's WebSocket handshake, for the same underlying reason).
            OnMessageReceived = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api/proxy") && context.Request.Query.TryGetValue("access_token", out var token))
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
app.MapControllers();

app.Run();

public partial class Program;
