using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Persistence.Configurations;

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_keys");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.KeyPrefix).IsRequired().HasMaxLength(16);
        builder.Property(x => x.HashedKey).IsRequired().HasMaxLength(128);
        builder.HasIndex(x => x.HashedKey).IsUnique();
        builder.HasIndex(x => x.OwnerUserId);
    }
}
