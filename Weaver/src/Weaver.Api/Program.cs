using Microsoft.EntityFrameworkCore;
using Weaver.Infrastructure;
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

builder.Services.AddHttpClient<IPageFetcher, HttpPageFetcher>();
builder.Services.AddHttpClient<PageProxyService>();
builder.Services.AddScoped<ScraperEngine>();
builder.Services.AddScoped<ScrapeRunner>();
builder.Services.AddSingleton<RateLimitGate>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"])
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

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
app.MapControllers();

app.Run();

public partial class Program;
