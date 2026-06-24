using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Orchestrator.Domain;

namespace Orchestrator.Infrastructure.Persistence;

public class OrchestratorDbContext(DbContextOptions<OrchestratorDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<AgentTask> Tasks => Set<AgentTask>();
    public DbSet<Run> Runs => Set<Run>();
    public DbSet<TaskEvent> Events => Set<TaskEvent>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<Skill> Skills => Set<Skill>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Sqlite (used by the in-memory test fixture, never in production where Npgsql is
        // used) can't order/filter on DateTimeOffset natively, so store it as a sortable
        // binary representation there. This conversion is a no-op for Npgsql.
        if (Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Store enums as strings: keeps the raw SQL used by the task-claiming SKIP LOCKED
        // query (which writes State via plain string interpolation) in sync with what EF
        // reads/writes through the normal LINQ path.
        modelBuilder.Entity<AgentTask>().Property(t => t.Lane).HasConversion<string>();
        modelBuilder.Entity<AgentTask>().Property(t => t.State).HasConversion<string>();
        modelBuilder.Entity<Run>().Property(r => r.Engine).HasConversion<string>();
        modelBuilder.Entity<Run>().Property(r => r.Status).HasConversion<string>();
        modelBuilder.Entity<TaskEvent>().Property(e => e.Type).HasConversion<string>();
        modelBuilder.Entity<Approval>().Property(a => a.Status).HasConversion<string>();
        modelBuilder.Entity<Goal>().Property(g => g.Status).HasConversion<string>();

        modelBuilder.Entity<Project>(e =>
        {
            e.HasMany(p => p.Goals).WithOne(g => g.Project!).HasForeignKey(g => g.ProjectId);
            e.HasMany(p => p.Tasks).WithOne(t => t.Project!).HasForeignKey(t => t.ProjectId);
            e.HasMany(p => p.Skills).WithOne(s => s.Project!).HasForeignKey(s => s.ProjectId);
        });

        modelBuilder.Entity<AgentTask>(e =>
        {
            e.HasOne(t => t.Goal).WithMany(g => g.Tasks).HasForeignKey(t => t.GoalId);
            e.HasOne(t => t.ParentTask).WithMany().HasForeignKey(t => t.ParentTaskId);
            e.HasMany(t => t.Runs).WithOne(r => r.Task!).HasForeignKey(r => r.TaskId);
            e.HasMany(t => t.Approvals).WithOne(a => a.Task!).HasForeignKey(a => a.TaskId);
            e.HasIndex(t => new { t.Lane, t.State, t.Priority });
        });

        modelBuilder.Entity<Run>(e =>
        {
            e.HasMany(r => r.Events).WithOne(ev => ev.Run!).HasForeignKey(ev => ev.RunId);
        });

        modelBuilder.Entity<TaskEvent>(e =>
        {
            e.HasIndex(ev => new { ev.RunId, ev.Id });
        });
    }
}
