// apps/lazuar-api/Modules/One/Infrastructure/OneDbContext.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.One.Domain;

namespace Modules.One.Infrastructure;

public class OneDbContext : PlatformDbContext
{
    public DbSet<Organization> Organizations { get; set; } = null!;
    public DbSet<GlobalUser> GlobalUsers { get; set; } = null!;
    public DbSet<TenantMembership> TenantMemberships { get; set; } = null!;
    public DbSet<TenantAppEntitlement> TenantAppEntitlements { get; set; } = null!;
    public DbSet<WorkspaceInvitation> WorkspaceInvitations { get; set; } = null!;
    public DbSet<TenantWebhookEndpoint> TenantWebhookEndpoints { get; set; } = null!;
    public DbSet<WebhookDeliveryOutbox> WebhookDeliveryOutboxes { get; set; } = null!;
    public DbSet<ApiCredential> ApiCredentials { get; set; } = null!;
    public DbSet<AuditEvent> AuditEvents { get; set; } = null!;

    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
    public DbSet<InboxMessage> InboxMessages { get; set; } = null!;

    public OneDbContext(
        DbContextOptions<OneDbContext> options,
        IExecutionContextAccessor executionContext,
        IMediator mediator,
        DatabaseJobTrigger jobTrigger) : base(options, executionContext, mediator, jobTrigger)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("one");

        modelBuilder.Entity<Organization>(builder =>
        {
            builder.ToTable("Organizations");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.Slug).IsUnique();
            // One workspace per (product, external org). Filtered so human-created orgs stay unbound.
            builder.HasIndex(x => new { x.ExternalProduct, x.ExternalOrgId })
                .IsUnique()
                .HasFilter("\"ExternalProduct\" IS NOT NULL AND \"ExternalOrgId\" IS NOT NULL");
            builder.Property(x => x.ExternalProduct).HasMaxLength(64);
            builder.Property(x => x.ExternalOrgId).HasMaxLength(128);
            builder.Property(x => x.LogoUrl).HasColumnType("text");
            builder.Property(x => x.PrimaryColor).HasMaxLength(7);
        });

        modelBuilder.Entity<GlobalUser>(builder =>
        {
            builder.ToTable("GlobalUsers");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.Email).IsUnique();
            builder.HasIndex(x => x.EmailVerificationTokenHash).IsUnique().HasFilter("\"EmailVerificationTokenHash\" IS NOT NULL");
            builder.HasIndex(x => x.PasswordResetTokenHash).IsUnique().HasFilter("\"PasswordResetTokenHash\" IS NOT NULL");
        });

        modelBuilder.Entity<TenantMembership>(builder =>
        {
            builder.ToTable("TenantMemberships");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.GlobalUserId, x.OrganizationId }).IsUnique();
        });

        modelBuilder.Entity<TenantAppEntitlement>(builder =>
        {
            builder.ToTable("TenantAppEntitlements");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.OrganizationId, x.AppId }).IsUnique();
        });

        modelBuilder.Entity<WorkspaceInvitation>(builder =>
        {
            builder.ToTable("WorkspaceInvitations");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.TokenHash).IsUnique();
            builder.HasIndex(x => new { x.OrganizationId, x.Email })
                .IsUnique()
                .HasFilter("\"Status\" = 'PENDING'");
        });

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
            c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c == null ? new List<string>() : c.ToList()
        );

        modelBuilder.Entity<TenantWebhookEndpoint>(builder =>
        {
            builder.ToTable("TenantWebhookEndpoints");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.OrganizationId);
            builder.Property(x => x.EnabledEvents)
                .HasField("_enabledEvents")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .HasConversion(stringListConverter, stringListComparer)
                .HasColumnType("jsonb")
                .HasDefaultValueSql("'[]'::jsonb");
        });

        modelBuilder.Entity<WebhookDeliveryOutbox>(builder =>
        {
            builder.ToTable("WebhookDeliveryOutboxes");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.Status, x.NextAttemptAt });
        });

        modelBuilder.Entity<ApiCredential>(builder =>
        {
            builder.ToTable("ApiCredentials");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.OrganizationId);
            builder.HasIndex(x => x.KeyHash).IsUnique();
            builder.Property(x => x.KeyHint).IsRequired().HasMaxLength(16);
            builder.Property(x => x.Scopes).IsRequired();
            builder.Property(x => x.Name).IsRequired();
            builder.Property(x => x.Prefix).IsRequired();
            builder.Property(x => x.KeyHash).IsRequired();
        });

        modelBuilder.Entity<AuditEvent>(builder =>
        {
            builder.ToTable("AuditEvents");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.OrganizationId, x.CreatedAt });
            builder.Property(x => x.ActorEmail).HasMaxLength(255);
            builder.Property(x => x.Action).HasMaxLength(100).IsRequired();
            builder.Property(x => x.EntityType).HasMaxLength(64).IsRequired();
            builder.Property(x => x.EntityId).HasMaxLength(64).IsRequired();
            builder.Property(x => x.MetadataJson).HasColumnType("jsonb");
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
