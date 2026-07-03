using Microsoft.EntityFrameworkCore;
using Weaver.Domain;

namespace Weaver.Infrastructure.Persistence;

public class WeaverDbContext : DbContext
{
    public WeaverDbContext(DbContextOptions<WeaverDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<Credential> Credentials => Set<Credential>();
    public DbSet<NotificationSettings> NotificationSettings => Set<NotificationSettings>();

    public DbSet<ScrapingProject> ScrapingProjects => Set<ScrapingProject>();
    public DbSet<FieldSelector> FieldSelectors => Set<FieldSelector>();
    public DbSet<RateLimitPolicy> RateLimitPolicies => Set<RateLimitPolicy>();
    public DbSet<ScrapeRun> ScrapeRuns => Set<ScrapeRun>();
    public DbSet<ScrapedItem> ScrapedItems => Set<ScrapedItem>();
    public DbSet<ScrapeJob> ScrapeJobs => Set<ScrapeJob>();

    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<WorkflowNode> WorkflowNodes => Set<WorkflowNode>();
    public DbSet<WorkflowEdge> WorkflowEdges => Set<WorkflowEdge>();
    public DbSet<WorkflowRun> WorkflowRuns => Set<WorkflowRun>();
    public DbSet<NodeRun> NodeRuns => Set<NodeRun>();
    public DbSet<WorkflowRevision> WorkflowRevisions => Set<WorkflowRevision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WeaverDbContext).Assembly);
    }
}
