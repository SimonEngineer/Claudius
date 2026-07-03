using Weaver.Infrastructure;
using Weaver.Scraping;
using Weaver.Scraping.Fetching;
using Weaver.Worker;
using Weaver.Workflows;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWeaverInfrastructure(builder.Configuration);
builder.Services.AddWeaverWorkflows(builder.Configuration);

builder.Services.AddHttpClient<HttpPageFetcher>();
builder.Services.AddSingleton<PlaywrightPageFetcher>();
builder.Services.AddScoped<IPageFetcherFactory, PageFetcherFactory>();
builder.Services.AddScoped<ScraperEngine>();
builder.Services.AddScoped<ScrapeRunner>();
builder.Services.AddSingleton<RateLimitGate>();

builder.Services.AddHostedService<ScrapeJobConsumerService>();
builder.Services.AddHostedService<CronTriggerSchedulerService>();
builder.Services.AddHostedService<ScheduledScrapeService>();
builder.Services.AddHostedService<HeartbeatService>();
builder.Services.AddHostedService<RetentionCleanupService>();

var host = builder.Build();

PlaywrightBootstrap.EnsureBrowsersInstalledIfEnabled(host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("PlaywrightBootstrap"));

host.Run();
