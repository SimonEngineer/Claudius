using Microsoft.EntityFrameworkCore;
using NameTags.Data.Entities;

namespace NameTags.Data;

public sealed class NameTagsDbContext(DbContextOptions<NameTagsDbContext> options) : DbContext(options)
{
    public DbSet<TagProject> TagProjects => Set<TagProject>();
    public DbSet<TagName> TagNames => Set<TagName>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TagProject>()
            .Property(p => p.ShapeType)
            .HasConversion<string>();

        modelBuilder.Entity<TagProject>()
            .HasMany(p => p.Names)
            .WithOne(n => n.TagProject)
            .HasForeignKey(n => n.TagProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TagName>()
            .HasIndex(n => new { n.TagProjectId, n.SortOrder });
    }
}
