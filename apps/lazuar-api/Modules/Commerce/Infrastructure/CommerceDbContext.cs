using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;
using Modules.Commerce.Domain.ValueObjects;

namespace Modules.Commerce.Infrastructure;

public class CommerceDbContext : PlatformDbContext
{
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<Coupon> Coupons { get; set; } = null!;
    public DbSet<Subscription> Subscriptions { get; set; } = null!;
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<CheckoutSession> CheckoutSessions { get; set; } = null!;
    public DbSet<ChargeAttemptLog> ChargeAttemptLogs { get; set; } = null!;
    public DbSet<DunningCampaign> DunningCampaigns { get; set; } = null!;
    public DbSet<DunningStep> DunningSteps { get; set; } = null!;
    public DbSet<ReminderDispatchLog> ReminderDispatchLogs { get; set; } = null!;
    public DbSet<CommerceTransactionLog> TransactionLogs { get; set; } = null!;

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

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<DunningStep>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.State = EntityState.Added;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ReminderDispatchLog>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.State = EntityState.Added;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ChargeAttemptLog>())
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
        modelBuilder.HasDefaultSchema("commerce");

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        var stringListConverter = new ValueConverter<IReadOnlyCollection<string>, string>(
            v => JsonSerializer.Serialize(v, jsonOptions),
            v => JsonSerializer.Deserialize<List<string>>(v, jsonOptions) ?? new List<string>()
        );

        var stringListComparer = new ValueComparer<IReadOnlyCollection<string>>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList()
        );

        var guidListConverter = new ValueConverter<IReadOnlyCollection<Guid>, string>(
            v => JsonSerializer.Serialize(v, jsonOptions),
            v => JsonSerializer.Deserialize<List<Guid>>(v, jsonOptions) ?? new List<Guid>()
        );

        var guidListComparer = new ValueComparer<IReadOnlyCollection<Guid>>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList()
        );

        var adHocLineItemConverter = new ValueConverter<IReadOnlyCollection<AdHocLineItem>, string>(
            v => JsonSerializer.Serialize(v, jsonOptions),
            v => JsonSerializer.Deserialize<List<AdHocLineItem>>(v, jsonOptions) ?? new List<AdHocLineItem>()
        );

        var adHocLineItemComparer = new ValueComparer<IReadOnlyCollection<AdHocLineItem>>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList()
        );

        modelBuilder.Entity<Product>(builder =>
        {
            builder.ToTable("Products");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.OrganizationId, x.Slug }).IsUnique();
            builder.Property(x => x.Price).HasPrecision(18, 4);
            
            builder.Property(x => x.PricingModel).HasMaxLength(50).HasDefaultValue("FIXED");
            builder.Property(x => x.MinimumPrice).HasPrecision(18, 4).HasDefaultValue(0m);

            builder.Property(x => x.FulfillmentTargets)
                .HasField("_fulfillmentTargets")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .HasConversion(stringListConverter, stringListComparer)
                .HasColumnType("jsonb");

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

            builder.Property(x => x.ApplicableProductIds)
                .HasField("_applicableProductIds")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .HasConversion(guidListConverter, guidListComparer)
                .HasColumnType("jsonb");
        });

        modelBuilder.Entity<Subscription>(builder =>
        {
            builder.ToTable("Subscriptions");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.OrganizationId);
            builder.HasIndex(x => x.Status);
            builder.HasIndex(x => x.NextBillingDate);
            builder.Property(x => x.IsReminderOnly).HasDefaultValue(false);

            builder.HasMany(x => x.ReminderLogs)
                .WithOne()
                .HasForeignKey(x => x.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Metadata.FindNavigation("ReminderLogs")?.SetPropertyAccessMode(PropertyAccessMode.Field);
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

            builder.Property(x => x.AdHocLineItems)
                .HasField("_adHocLineItems")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .HasConversion(adHocLineItemConverter, adHocLineItemComparer)
                .HasColumnType("jsonb");
        });

        modelBuilder.Entity<ChargeAttemptLog>(builder =>
        {
            builder.ToTable("ChargeAttemptLogs");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.SubscriptionId, x.TargetBillingDate }).IsUnique();
        });

        modelBuilder.Entity<DunningCampaign>(builder =>
        {
            builder.ToTable("DunningCampaigns");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.OrganizationId);

            builder.Property(x => x.RecoveredRevenue).HasPrecision(18, 4);

            builder.Property(x => x.TargetProductIds)
                .HasField("_targetProductIds")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .HasConversion(guidListConverter, guidListComparer)
                .HasColumnType("jsonb");

            builder.Property(x => x.TargetPaymentMethods)
                .HasField("_targetPaymentMethods")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .HasConversion(stringListConverter, stringListComparer)
                .HasColumnType("jsonb");

            builder.HasMany(x => x.Steps)
                .WithOne()
                .HasForeignKey(x => x.DunningCampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Metadata.FindNavigation("Steps")?.SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<DunningStep>(builder =>
        {
            builder.ToTable("DunningSteps");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.ActionType).HasMaxLength(50);
        });

        modelBuilder.Entity<ReminderDispatchLog>(builder =>
        {
            builder.ToTable("ReminderDispatchLogs");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.SubscriptionId, x.ScheduleId, x.TargetBillingDate }).IsUnique();
        });

        modelBuilder.Entity<CommerceTransactionLog>(builder =>
        {
            builder.ToTable("TransactionLogs");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.OrganizationId);
            builder.HasIndex(x => x.CreatedAt);
            builder.Property(x => x.Amount).HasPrecision(18, 4);
            builder.Property(x => x.FeeAmount).HasPrecision(18, 4);
            builder.Property(x => x.NetAmount).HasPrecision(18, 4);
            builder.Property(x => x.Currency).HasMaxLength(10);
            builder.Property(x => x.Status).HasMaxLength(50);
            builder.Property(x => x.CustomerName).HasMaxLength(255);
            builder.Property(x => x.CustomerEmail).HasMaxLength(255);
            builder.Property(x => x.ProductName).HasMaxLength(255);
            builder.Property(x => x.RecordedByName).HasMaxLength(255);
            builder.Property(x => x.ExternalReference).HasMaxLength(255);
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
