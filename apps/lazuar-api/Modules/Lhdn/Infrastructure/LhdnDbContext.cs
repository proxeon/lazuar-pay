using MediatR;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Infrastructure;

public class LhdnDbContext : PlatformDbContext
{
    public DbSet<LhdnTenantConfig> TenantConfigs { get; set; } = null!;
    public DbSet<TaxDocument> TaxDocuments { get; set; } = null!;
    public DbSet<WebhookSubscription> WebhookSubscriptions { get; set; } = null!;
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
    public DbSet<InboxMessage> InboxMessages { get; set; } = null!;

    public LhdnDbContext(
        DbContextOptions<LhdnDbContext> options,
        IExecutionContextAccessor executionContext,
        IMediator mediator,
        DatabaseJobTrigger jobTrigger) : base(options, executionContext, mediator, jobTrigger)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("lhdn");

        modelBuilder.Entity<LhdnTenantConfig>(builder =>
        {
            builder.ToTable("TenantConfigs");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.OrganizationId).IsUnique();
        });

        modelBuilder.Entity<TaxDocument>(builder =>
        {
            builder.ToTable("TaxDocuments");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.OrganizationId, x.ValidationStatus });
            builder.HasIndex(x => x.ValidationStatus);
        });

        modelBuilder.Entity<WebhookSubscription>(builder =>
        {
            builder.ToTable("WebhookSubscriptions");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.OrganizationId);
        });

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
