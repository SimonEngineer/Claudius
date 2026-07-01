using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Persistence.Configurations;

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("audit_log_entries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ResourceType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ResourceName).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => new { x.OwnerUserId, x.CreatedAt });
    }
}
