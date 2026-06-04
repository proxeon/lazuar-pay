using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Community.Domain.Aggregates;
using Modules.Community.Domain.Entities;

namespace Modules.Community.Infrastructure;

public class CommunityDbContext : PlatformDbContext
{
    public DbSet<CommunityPlan> Plans { get; set; } = null!;
    public DbSet<CommunitySubscription> Subscriptions { get; set; } = null!;
    public DbSet<PaymentRecord> PaymentRecords { get; set; } = null!;
    public DbSet<CommunityReminderSchedule> ReminderSchedules { get; set; } = null!;
    
    // Platform Box pattern tables
    public DbSet<InboxMessage> InboxMessages { get; set; } = null!;
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

    public CommunityDbContext(
        DbContextOptions<CommunityDbContext> options, 
        IExecutionContextAccessor executionContext, 
        IMediator mediator, 
        DatabaseJobTrigger jobTrigger) 
        : base(options, executionContext, mediator, jobTrigger)
    {
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Intercept any new PaymentRecord entities added to the tracked navigation collection.
        // Force their state to Added to prevent the EF Core tracking bug from executing an UPDATE statement.
        foreach (var entry in ChangeTracker.Entries<PaymentRecord>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.State = EntityState.Added;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Isolate module tables into their own schema
        modelBuilder.HasDefaultSchema("community");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommunityDbContext).Assembly);

        // Configure Inbox/Outbox
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("OutboxMessages");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.ProcessedAt, x.OccurredOn }).HasFilter("\"ProcessedAt\" IS NULL");
        });
        
        modelBuilder.Entity<InboxMessage>(builder =>
        {
            builder.ToTable("InboxMessages");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.ProcessedAt, x.ReceivedAt }).HasFilter("\"ProcessedAt\" IS NULL");
        });
    }
}
