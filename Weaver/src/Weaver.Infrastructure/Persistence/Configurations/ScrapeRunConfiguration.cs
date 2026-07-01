using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Persistence.Configurations;

public class ScrapeRunConfiguration : IEntityTypeConfiguration<ScrapeRun>
{
    public void Configure(EntityTypeBuilder<ScrapeRun> builder)
    {
        builder.ToTable("scrape_runs");
        builder.HasKey(x => x.Id);

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(i => i.ScrapeRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ScrapedItemConfiguration : IEntityTypeConfiguration<ScrapedItem>
{
    public void Configure(EntityTypeBuilder<ScrapedItem> builder)
    {
        builder.ToTable("scraped_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ItemKey).IsRequired().HasMaxLength(500);
        builder.Property(x => x.ContentHash).IsRequired().HasMaxLength(128);
        builder.HasIndex(x => new { x.ScrapingProjectId, x.ItemKey });

        var comparer = new ValueComparer<Dictionary<string, string?>>(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            d => JsonSerializer.Serialize(d, (JsonSerializerOptions?)null).GetHashCode(),
            d => new Dictionary<string, string?>(d));

        builder.Property(x => x.Data)
            .HasConversion(
                d => JsonSerializer.Serialize(d, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<Dictionary<string, string?>>(json, (JsonSerializerOptions?)null) ?? new())
            .HasColumnType("jsonb")
            .Metadata.SetValueComparer(comparer);
    }
}

public class ScrapeJobConfiguration : IEntityTypeConfiguration<ScrapeJob>
{
    public void Configure(EntityTypeBuilder<ScrapeJob> builder)
    {
        builder.ToTable("scrape_jobs");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Status);
    }
}
