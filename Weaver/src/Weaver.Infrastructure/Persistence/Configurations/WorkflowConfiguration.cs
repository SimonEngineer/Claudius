using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Persistence.Configurations;

public class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
{
    public void Configure(EntityTypeBuilder<Workflow> builder)
    {
        builder.ToTable("workflows");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);

        builder.HasMany(x => x.Nodes)
            .WithOne()
            .HasForeignKey(n => n.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Edges)
            .WithOne()
            .HasForeignKey(e => e.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Runs)
            .WithOne(r => r.Workflow)
            .HasForeignKey(r => r.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkflowNodeConfiguration : IEntityTypeConfiguration<WorkflowNode>
{
    public void Configure(EntityTypeBuilder<WorkflowNode> builder)
    {
        builder.ToTable("workflow_nodes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ConfigJson).HasColumnType("jsonb");
        builder.HasIndex(x => new { x.WorkflowId, x.Type });
    }
}

public class WorkflowEdgeConfiguration : IEntityTypeConfiguration<WorkflowEdge>
{
    public void Configure(EntityTypeBuilder<WorkflowEdge> builder)
    {
        builder.ToTable("workflow_edges");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.SourceNodeId);
        builder.HasIndex(x => x.TargetNodeId);
    }
}

public class WorkflowRunConfiguration : IEntityTypeConfiguration<WorkflowRun>
{
    public void Configure(EntityTypeBuilder<WorkflowRun> builder)
    {
        builder.ToTable("workflow_runs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TriggerPayloadJson).HasColumnType("jsonb");

        builder.HasMany(x => x.NodeRuns)
            .WithOne()
            .HasForeignKey(n => n.WorkflowRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class NodeRunConfiguration : IEntityTypeConfiguration<NodeRun>
{
    public void Configure(EntityTypeBuilder<NodeRun> builder)
    {
        builder.ToTable("node_runs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.InputJson).HasColumnType("jsonb");
        builder.Property(x => x.OutputJson).HasColumnType("jsonb");
    }
}
