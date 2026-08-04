using MediatR;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.Messaging.Domain;

namespace Modules.Messaging.Infrastructure;

public class MessagingDbContext : PlatformDbContext
{
    public DbSet<TenantReplica> TenantReplicas { get; set; } = null!;
    public DbSet<MessageDeliveryLog> MessageDeliveryLogs { get; set; } = null!;
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
    public DbSet<InboxMessage> InboxMessages { get; set; } = null!;

    public MessagingDbContext(
        DbContextOptions<MessagingDbContext> options,
        IExecutionContextAccessor executionContext,
        IMediator mediator,
        DatabaseJobTrigger jobTrigger) : base(options, executionContext, mediator, jobTrigger)
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

        modelBuilder.Entity<MessageDeliveryLog>(builder =>
        {
            builder.ToTable("MessageDeliveryLogs");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Channel).HasMaxLength(32).IsRequired();
            builder.Property(x => x.Recipient).HasMaxLength(320).IsRequired();
            builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
            builder.Property(x => x.ProviderMessageId).HasMaxLength(128);
            builder.Property(x => x.Error).HasMaxLength(2000);
            builder.HasIndex(x => new { x.OrganizationId, x.CreatedAt });
            builder.HasIndex(x => x.CorrelationEventId);
        });

        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("OutboxMessages");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.NextAttemptAt, x.OccurredOn }).HasFilter("\"ProcessedAt\" IS NULL");
        });

        modelBuilder.Entity<InboxMessage>(builder =>
        {
            builder.ToTable("InboxMessages");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.NextAttemptAt, x.ReceivedAt }).HasFilter("\"ProcessedAt\" IS NULL");
        });
    }
}
