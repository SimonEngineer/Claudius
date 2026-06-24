using Microsoft.EntityFrameworkCore;
using TripPlanner.Domain.Entities;

namespace TripPlanner.Infrastructure.Persistence;

public class TripPlannerDbContext : DbContext
{
    public TripPlannerDbContext(DbContextOptions<TripPlannerDbContext> options) : base(options) { }

    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<TaggedItem> TaggedItems => Set<TaggedItem>();
    public DbSet<MediaItem> MediaItems => Set<MediaItem>();

    public DbSet<WishlistLocation> WishlistLocations => Set<WishlistLocation>();
    public DbSet<WishlistNote> WishlistNotes => Set<WishlistNote>();
    public DbSet<WishlistLink> WishlistLinks => Set<WishlistLink>();
    public DbSet<WishlistPlan> WishlistPlans => Set<WishlistPlan>();
    public DbSet<Whiteboard> Whiteboards => Set<Whiteboard>();

    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<GoalFieldDefinition> GoalFieldDefinitions => Set<GoalFieldDefinition>();
    public DbSet<GoalItem> GoalItems => Set<GoalItem>();
    public DbSet<GoalItemFieldValue> GoalItemFieldValues => Set<GoalItemFieldValue>();
    public DbSet<GoalItemNote> GoalItemNotes => Set<GoalItemNote>();
    public DbSet<GoalItemLink> GoalItemLinks => Set<GoalItemLink>();

    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripStop> TripStops => Set<TripStop>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<TimelineEntry> TimelineEntries => Set<TimelineEntry>();
    public DbSet<PackingItem> PackingItems => Set<PackingItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TaggedItem>(b =>
        {
            b.HasKey(t => new { t.TagId, t.EntityType, t.EntityId });
            b.HasOne(t => t.Tag).WithMany(t => t.TaggedItems).HasForeignKey(t => t.TagId);
            b.HasIndex(t => new { t.EntityType, t.EntityId });
        });

        modelBuilder.Entity<MediaItem>(b =>
        {
            b.HasIndex(m => new { m.EntityType, m.EntityId });
        });

        modelBuilder.Entity<Tag>(b =>
        {
            b.HasIndex(t => t.Name).IsUnique();
        });

        modelBuilder.Entity<WishlistLocation>(b =>
        {
            b.HasMany(l => l.Notes).WithOne(n => n.WishlistLocation).HasForeignKey(n => n.WishlistLocationId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(l => l.Links).WithOne(n => n.WishlistLocation).HasForeignKey(n => n.WishlistLocationId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(l => l.Plan).WithOne(p => p.WishlistLocation).HasForeignKey<WishlistPlan>(p => p.WishlistLocationId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(l => l.Whiteboard).WithOne(w => w.WishlistLocation).HasForeignKey<Whiteboard>(w => w.WishlistLocationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Goal>(b =>
        {
            b.HasMany(g => g.FieldDefinitions).WithOne(f => f.Goal).HasForeignKey(f => f.GoalId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(g => g.Items).WithOne(i => i.Goal).HasForeignKey(i => i.GoalId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(g => g.LinkedTrip).WithMany().HasForeignKey(g => g.LinkedTripId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<GoalItem>(b =>
        {
            b.HasMany(i => i.FieldValues).WithOne(v => v.GoalItem).HasForeignKey(v => v.GoalItemId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(i => i.Notes).WithOne(n => n.GoalItem).HasForeignKey(n => n.GoalItemId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(i => i.Links).WithOne(n => n.GoalItem).HasForeignKey(n => n.GoalItemId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GoalFieldDefinition>(b =>
        {
            b.HasMany<GoalItemFieldValue>().WithOne(v => v.GoalFieldDefinition).HasForeignKey(v => v.GoalFieldDefinitionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Trip>(b =>
        {
            b.HasMany(t => t.Stops).WithOne(s => s.Trip).HasForeignKey(s => s.TripId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(t => t.Bookings).WithOne(s => s.Trip).HasForeignKey(s => s.TripId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(t => t.Timeline).WithOne(s => s.Trip).HasForeignKey(s => s.TripId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(t => t.PackingItems).WithOne(p => p.Trip).HasForeignKey(p => p.TripId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
