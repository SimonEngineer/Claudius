using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Persistence.Configurations;

public class CredentialConfiguration : IEntityTypeConfiguration<Credential>
{
    public void Configure(EntityTypeBuilder<Credential> builder)
    {
        builder.ToTable("credentials");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.EncryptedValue).IsRequired();
        builder.HasIndex(x => new { x.OwnerUserId, x.Name }).IsUnique();
    }
}

public class WorkflowRevisionConfiguration : IEntityTypeConfiguration<WorkflowRevision>
{
    public void Configure(EntityTypeBuilder<WorkflowRevision> builder)
    {
        builder.ToTable("workflow_revisions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.WorkflowName).IsRequired().HasMaxLength(300);
        builder.HasIndex(x => x.WorkflowId);

        // Revisions die with their workflow.
        builder.HasOne<Workflow>()
            .WithMany()
            .HasForeignKey(x => x.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class NotificationSettingsConfiguration : IEntityTypeConfiguration<NotificationSettings>
{
    public void Configure(EntityTypeBuilder<NotificationSettings> builder)
    {
        builder.ToTable("notification_settings");
        builder.HasKey(x => x.UserId);

        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<NotificationSettings>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
