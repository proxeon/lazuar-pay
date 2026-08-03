using MediatR;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.CRM.Domain;

namespace Modules.CRM.Infrastructure;

public class CrmDbContext : PlatformDbContext
{
    public DbSet<ClientProfileEntity> ClientProfiles { get; set; } = null!;

    // Outbox/Inbox tables to satisfy platform job patterns
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
    public DbSet<InboxMessage> InboxMessages { get; set; } = null!;

    public CrmDbContext(
        DbContextOptions<CrmDbContext> options,
        IExecutionContextAccessor executionContext,
        IMediator mediator,
        DatabaseJobTrigger jobTrigger)
        : base(options, executionContext, mediator, jobTrigger)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Isolate CRM context tables inside their own private schema
        modelBuilder.HasDefaultSchema("crm");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);

        // Configure Inbox/Outbox Infrastructure Tables
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
