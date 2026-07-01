using Weaver.Infrastructure;
using Weaver.Scraping;
using Weaver.Scraping.Fetching;
using Weaver.Worker;
using Weaver.Workflows;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWeaverInfrastructure(builder.Configuration);
builder.Services.AddWeaverWorkflows(builder.Configuration);

builder.Services.AddHttpClient<IPageFetcher, HttpPageFetcher>();
builder.Services.AddScoped<ScraperEngine>();
builder.Services.AddScoped<ScrapeRunner>();
builder.Services.AddSingleton<RateLimitGate>();

builder.Services.AddHostedService<ScrapeJobConsumerService>();
builder.Services.AddHostedService<CronTriggerSchedulerService>();

var host = builder.Build();
host.Run();
