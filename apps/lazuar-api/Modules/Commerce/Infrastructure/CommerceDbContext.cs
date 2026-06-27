using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;

namespace Modules.Commerce.Infrastructure;

public class CommerceDbContext : PlatformDbContext
{
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<Coupon> Coupons { get; set; } = null!;
    public DbSet<Subscription> Subscriptions { get; set; } = null!;
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<CheckoutSession> CheckoutSessions { get; set; } = null!;
    public DbSet<ChargeAttemptLog> ChargeAttemptLogs { get; set; } = null!;

    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
    public DbSet<InboxMessage> InboxMessages { get; set; } = null!;

    public CommerceDbContext(
        DbContextOptions<CommerceDbContext> options,
        IExecutionContextAccessor executionContext,
        IMediator mediator,
        DatabaseJobTrigger jobTrigger)
        : base(options, executionContext, mediator, jobTrigger)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("commerce");

        modelBuilder.Entity<Product>(builder =>
        {
            builder.ToTable("Products");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.OrganizationId, x.Slug }).IsUnique();
            builder.Property(x => x.Price).HasPrecision(18, 4);

            builder.OwnsOne(x => x.CheckoutConfiguration, c =>
            {
                c.Property(p => p.RequiresAddress).HasColumnName("RequiresAddress");
                c.Property(p => p.RequiresTaxId).HasColumnName("RequiresTaxId");
                c.Property(p => p.RequiresPhone).HasColumnName("RequiresPhone");
            });
        });

        modelBuilder.Entity<Coupon>(builder =>
        {
            builder.ToTable("Coupons");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
            builder.Property(x => x.Amount).HasPrecision(18, 4);
            builder.Property(x => x.MinimumOriginalPrice).HasPrecision(18, 4);
        });

        modelBuilder.Entity<Subscription>(builder =>
        {
            builder.ToTable("Subscriptions");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.OrganizationId);
            builder.HasIndex(x => x.Status);
            builder.HasIndex(x => x.NextBillingDate);
        });

        modelBuilder.Entity<Order>(builder =>
        {
            builder.ToTable("Orders");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.AmountPaid).HasPrecision(18, 4);
        });

        modelBuilder.Entity<CheckoutSession>(builder =>
        {
            builder.ToTable("CheckoutSessions");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<ChargeAttemptLog>(builder =>
        {
            builder.ToTable("ChargeAttemptLogs");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.SubscriptionId, x.TargetBillingDate }).IsUnique();
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
