using Hangfire;
using Hangfire.Dashboard;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Orchestrator.Api.Hubs;
using Orchestrator.Api.Streaming;
using Orchestrator.Domain.Streaming;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Discord;
using Orchestrator.Infrastructure.Persistence;
using Orchestrator.Infrastructure.Scheduling;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Structured logging: every log line is JSON with task/run/project correlation ids attached
// via ILogger scopes inside the scheduling/engine code.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

builder.Services.AddCors(options => options.AddPolicy("Dashboard", policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddOrchestratorInfrastructure(builder.Configuration);

// Fan every broadcast out to SignalR (live dashboard) and Discord (approval/attention
// notifications + "!status"); both sinks are resolved from the same scope so Discord's DB
// lookups share the request's/job's DbContext.
builder.Services.AddScoped<SignalREventBroadcaster>();
builder.Services.AddScoped<IEventBroadcaster>(sp => new CompositeEventBroadcaster([
    sp.GetRequiredService<SignalREventBroadcaster>(),
    sp.GetRequiredService<DiscordEventBroadcaster>()
]));

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("orchestrator-api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

// Two Hangfire servers, one per lane, each with its own worker-count cap -- this is the
// concurrency control described in the design doc: the worker lane is capped low because the
// local model host is slow, the supervisor lane can run several cloud calls in parallel.
var schedulerSection = builder.Configuration.GetSection(SchedulerOptions.SectionName);
var supervisorConcurrency = schedulerSection.GetValue("SupervisorConcurrency", 3);
var workerConcurrency = schedulerSection.GetValue("WorkerConcurrency", 1);

builder.Services.AddHangfireServer(opts =>
{
    opts.Queues = ["supervisor"];
    opts.WorkerCount = supervisorConcurrency;
    opts.ServerName = "supervisor-lane";
});

builder.Services.AddHangfireServer(opts =>
{
    opts.Queues = ["worker"];
    opts.WorkerCount = workerConcurrency;
    opts.ServerName = "worker-lane";
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseCors("Dashboard");
app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.MapHub<TaskStreamHub>("/hubs/tasks");

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new LocalhostOnlyAuthorizationFilter()]
});

// The scheduler tick itself runs on a tight PeriodicTimer via SchedulerTickHostedService
// (registered in AddOrchestratorInfrastructure) rather than a Hangfire recurring job, since
// Hangfire's cron resolution is minute-level -- too coarse for prompt slot reuse.

app.Run();

/// <summary>Single-user/self-hosted assumption: Hangfire dashboard only reachable from localhost.
/// Swap for real auth before exposing this off-box.</summary>
file class LocalhostOnlyAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.Connection.RemoteIpAddress is null
            || System.Net.IPAddress.IsLoopback(httpContext.Connection.RemoteIpAddress);
    }
}
