using MediatR;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.Lhdn.Domain.Aggregates;
using Modules.Lhdn.Domain.Entities;
using System;

namespace Modules.Lhdn.Infrastructure;

public class LhdnDbContext : PlatformDbContext
{
    public DbSet<LhdnTenantConfig> TenantConfigs { get; set; } = null!;
    public DbSet<TaxDocument> TaxDocuments { get; set; } = null!;
    public DbSet<WebhookSubscription> WebhookSubscriptions { get; set; } = null!;
    public DbSet<DeveloperApiKey> DeveloperApiKeys { get; set; } = null!;
    
    public DbSet<MsicCode> MsicCodes { get; set; } = null!;
    public DbSet<CountryCode> CountryCodes { get; set; } = null!;
    public DbSet<TaxType> TaxTypes { get; set; } = null!;
    
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

            // SEED DATA FOR DEVELOPMENT (lazuar-hq tenant)
            builder.HasData(new
            {
                Id = new Guid("00000000-0000-0000-0000-000000000001"),
                OrganizationId = new Guid("7d97963c-063c-4598-86cc-9ddd9d47d9b1"), // Matches lazuar-hq
                IntermediaryMode = true,
                EncryptedPfxBase64 = (string?)null,
                PfxPasswordCiphertext = (string?)null,
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
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

        modelBuilder.Entity<DeveloperApiKey>(builder =>
        {
            builder.ToTable("DeveloperApiKeys");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.OrganizationId);
            builder.HasIndex(x => x.KeyHash).IsUnique();
        });

        modelBuilder.Entity<MsicCode>(builder =>
        {
            builder.ToTable("MsicCodes");
            builder.HasKey(x => x.Code);
        });

        modelBuilder.Entity<CountryCode>(builder =>
        {
            builder.ToTable("CountryCodes");
            builder.HasKey(x => x.Code);
        });

        modelBuilder.Entity<TaxType>(builder =>
        {
            builder.ToTable("TaxTypes");
            builder.HasKey(x => x.Code);
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
