// apps/lazuar-api/Modules/Messaging/Infrastructure/MessagingDbContext.cs
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.Messaging.Domain;

namespace Modules.Messaging.Infrastructure;

public class MessagingDbContext : PlatformDbContext
{
    public DbSet<TenantReplica> TenantReplicas { get; set; } = null!;
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
    public DbSet<InboxMessage> InboxMessages { get; set; } = null!;

    public MessagingDbContext(DbContextOptions<MessagingDbContext> options, IExecutionContextAccessor executionContext) 
        : base(options, executionContext)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("messaging");

        modelBuilder.Entity<TenantReplica>(builder =>
        {
            builder.ToTable("TenantReplicas");
            builder.HasKey(x => x.Id);
        });

        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("OutboxMessages");
            builder.HasKey(x => x.Id);

            // Add high-performance partial index for active outbox messages
            builder.HasIndex(x => new { x.ProcessedAt, x.OccurredOn })
                   .HasFilter("\"ProcessedAt\" IS NULL");
        });

        modelBuilder.Entity<InboxMessage>(builder =>
        {
            builder.ToTable("InboxMessages");
            builder.HasKey(x => x.Id);

            // Add high-performance partial index for active inbox messages
            builder.HasIndex(x => new { x.ProcessedAt, x.ReceivedAt })
                   .HasFilter("\"ProcessedAt\" IS NULL");
        });
    }
}
