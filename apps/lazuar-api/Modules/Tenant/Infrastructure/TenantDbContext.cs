// apps/lazuar-api/Modules/Tenant/Infrastructure/TenantDbContext.cs
using MediatR;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.Tenant.Domain;

namespace Modules.Tenant.Infrastructure;

public class TenantDbContext : PlatformDbContext
{
    public DbSet<OrganizationEntity> Organizations { get; set; } = null!;
    public DbSet<BranchEntity> Branches { get; set; } = null!;
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
    public DbSet<InboxMessage> InboxMessages { get; set; } = null!;

    public TenantDbContext(DbContextOptions<TenantDbContext> options, IExecutionContextAccessor executionContext, IMediator mediator) 
        : base(options, executionContext, mediator)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("tenant");

        modelBuilder.Entity<OrganizationEntity>(builder =>
        {
            builder.ToTable("Organizations");
            builder.HasKey(x => x.Id);
        });

        modelBuilder.Entity<BranchEntity>(builder =>
        {
            builder.ToTable("Branches");
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
