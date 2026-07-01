using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Persistence.Configurations;

public class ScrapingProjectConfiguration : IEntityTypeConfiguration<ScrapingProject>
{
    public void Configure(EntityTypeBuilder<ScrapingProject> builder)
    {
        builder.ToTable("scraping_projects");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.StartUrl).IsRequired();
        builder.HasIndex(x => x.OwnerUserId);

        builder.HasOne(x => x.RateLimitPolicy)
            .WithMany()
            .HasForeignKey(x => x.RateLimitPolicyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Fields)
            .WithOne()
            .HasForeignKey(f => f.ScrapingProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Runs)
            .WithOne(r => r.ScrapingProject)
            .HasForeignKey(r => r.ScrapingProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class FieldSelectorConfiguration : IEntityTypeConfiguration<FieldSelector>
{
    public void Configure(EntityTypeBuilder<FieldSelector> builder)
    {
        builder.ToTable("field_selectors");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Selector).IsRequired();
        builder.HasIndex(x => new { x.ScrapingProjectId, x.Name });
    }
}

public class RateLimitPolicyConfiguration : IEntityTypeConfiguration<RateLimitPolicy>
{
    public void Configure(EntityTypeBuilder<RateLimitPolicy> builder)
    {
        builder.ToTable("rate_limit_policies");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.OwnerUserId);
    }
}
