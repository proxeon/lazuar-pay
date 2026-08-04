using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Domain.Entities;

namespace Modules.Billing.Infrastructure;

public class BillingDbContext : PlatformDbContext
{
    public DbSet<LedgerEntry> LedgerEntries { get; set; } = null!;
    public DbSet<LedgerLine> LedgerLines { get; set; } = null!;
    public DbSet<DeferredRevenueSchedule> DeferredRevenueSchedules { get; set; } = null!;
    public DbSet<TenantCreditBalance> TenantCreditBalances { get; set; } = null!;
    public DbSet<CreditLedger> CreditLedgers { get; set; } = null!;
    public DbSet<CreditHold> CreditHolds { get; set; } = null!;
    public DbSet<CreditDeductionIdempotencyLog> CreditDeductionIdempotencyLogs { get; set; } = null!;
    public DbSet<TenantBillingProfile> TenantBillingProfiles { get; set; } = null!;
    public DbSet<DocumentSequence> DocumentSequences { get; set; } = null!;
    
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
    public DbSet<InboxMessage> InboxMessages { get; set; } = null!;

    public BillingDbContext(
        DbContextOptions<BillingDbContext> options,
        IExecutionContextAccessor executionContext,
        IMediator mediator,
        DatabaseJobTrigger jobTrigger)
        : base(options, executionContext, mediator, jobTrigger)
    {
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<CreditLedger>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.State = EntityState.Added;
            }
        }
        
        foreach (var entry in ChangeTracker.Entries<LedgerLine>())
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
        
        modelBuilder.HasDefaultSchema("billing");

        modelBuilder.Entity<LedgerEntry>(builder =>
        {
            builder.ToTable("LedgerEntries");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId }).IsUnique();
            builder.Property(x => x.TaxInvoiceId).HasMaxLength(100);
            builder.Property(x => x.CustomerDocumentNumber).HasMaxLength(100);
            builder.Property(x => x.LhdnDocumentUuid).HasMaxLength(100);
            builder.Property(x => x.LhdnValidationStatus).HasMaxLength(50);
            builder.Property(x => x.ConsolidationStatus).HasMaxLength(30);
            builder.HasIndex(x => new { x.OrganizationId, x.ConsolidationStatus, x.Timestamp });
            builder.HasMany(x => x.Lines).WithOne().HasForeignKey("LedgerEntryId").OnDelete(DeleteBehavior.Cascade);
            builder.Metadata.FindNavigation("Lines")?.SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<LedgerLine>(builder =>
        {
            builder.ToTable("LedgerLines");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Amount).HasPrecision(18, 4);
            builder.Property(x => x.BaseCurrencyAmount).HasPrecision(18, 4);
        });

        modelBuilder.Entity<DeferredRevenueSchedule>(builder =>
        {
            builder.ToTable("DeferredRevenueSchedules");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.LedgerEntryId);
            builder.Property(x => x.TotalDeferredAmount).HasPrecision(18, 4);
            builder.Property(x => x.RecognizedAmount).HasPrecision(18, 4);
        });

        modelBuilder.Entity<TenantCreditBalance>(builder =>
        {
            builder.ToTable("TenantCreditBalances");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.OrganizationId).IsUnique();

            // PostgreSQL system xmin (xid) as optimistic concurrency token.
            // Must be uint/xid — Npgsql 10 rejects reading xid as byte[] (IsRowVersion).
            builder.Property<uint>("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();

            builder.HasMany(x => x.Transactions)
                   .WithOne()
                   .HasForeignKey("TenantCreditBalanceId")
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Metadata.FindNavigation("Transactions")?.SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<CreditHold>(builder =>
        {
            builder.ToTable("CreditHolds");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.OrganizationId, x.CorrelationId });
            builder.Property<uint>("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
            builder.Property(x => x.Status).HasMaxLength(20);
            builder.Property(x => x.CorrelationId).HasMaxLength(100);
            builder.Property(x => x.Reference).HasMaxLength(300);
        });

        modelBuilder.Entity<CreditDeductionIdempotencyLog>(builder =>
        {
            builder.ToTable("CreditDeductionIdempotencyLogs");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.OrganizationId, x.IdempotencyKey }).IsUnique();
            builder.Property(x => x.IdempotencyKey).HasMaxLength(200);
            builder.Property(x => x.Reference).HasMaxLength(300);
        });

        modelBuilder.Entity<CreditLedger>(builder =>
        {
            builder.ToTable("CreditLedgers");
            builder.HasKey(x => x.Id);
        });

        modelBuilder.Entity<TenantBillingProfile>(builder =>
        {
            builder.ToTable("TenantBillingProfiles");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.OrganizationId).IsUnique();

            builder.OwnsOne(x => x.Address, a =>
            {
                a.Property(p => p.Line1).HasColumnName("AddressLine1").HasMaxLength(150);
                a.Property(p => p.Line2).HasColumnName("AddressLine2").HasMaxLength(150);
                a.Property(p => p.Line3).HasColumnName("AddressLine3").HasMaxLength(150);
                a.Property(p => p.City).HasColumnName("City").HasMaxLength(50);
                a.Property(p => p.PostalCode).HasColumnName("PostalCode").HasMaxLength(20);
                a.Property(p => p.StateCode).HasColumnName("StateCode").HasMaxLength(10);
                a.Property(p => p.CountryCode).HasColumnName("CountryCode").HasMaxLength(10);
            });
        });

        modelBuilder.Entity<DocumentSequence>(builder =>
        {
            builder.ToTable("DocumentSequences");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.OrganizationId, x.Prefix }).IsUnique();
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
