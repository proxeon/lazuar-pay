using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure;

/// <summary>
/// Shared EF fluent config for platform outbox/inbox tables.
/// Filter SQL and index columns must stay byte-identical across modules to avoid migration noise.
/// </summary>
public static class OutboxInboxModelBuilderExtensions
{
    /// <summary>
    /// Configures <see cref="OutboxMessage"/> and <see cref="InboxMessage"/> with pending-poll indexes.
    /// Invoke from each module DbContext <c>OnModelCreating</c> after schema/default config.
    /// </summary>
    public static void ApplyOutboxInbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("OutboxMessages");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.NextAttemptAt, x.OccurredOn })
                .HasFilter("\"ProcessedAt\" IS NULL");
        });

        modelBuilder.Entity<InboxMessage>(builder =>
        {
            builder.ToTable("InboxMessages");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.NextAttemptAt, x.ReceivedAt })
                .HasFilter("\"ProcessedAt\" IS NULL");
        });
    }
}
