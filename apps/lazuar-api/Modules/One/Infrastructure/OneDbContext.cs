using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
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
    public DbSet<AppAccessRequest> AppAccessRequests { get; set; } = null!;

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
            builder.HasIndex(x => new { x.OrganizationId, x.Email }).HasFilter("\"Status\" = 'PENDING'");
        });

        modelBuilder.Entity<AppAccessRequest>(builder =>
        {
            builder.ToTable("AppAccessRequests");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.Status).HasFilter("\"Status\" = 'PENDING'");

            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
            builder.Property(x => x.RequestedApps)
                   .HasConversion(
                       v => JsonSerializer.Serialize(v, jsonOptions),
                       v => JsonSerializer.Deserialize<List<string>>(v, jsonOptions) ?? new List<string>()
                   )
                   .HasColumnType("jsonb");
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
