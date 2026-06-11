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
            builder.Property(x => x.LhdnValidationStatus).HasMaxLength(50);
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
