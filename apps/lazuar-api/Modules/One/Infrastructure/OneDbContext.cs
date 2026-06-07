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
            // Ensures a tenant can only have one entitlement record per app (which toggles true/false)
            builder.HasIndex(x => new { x.OrganizationId, x.AppId }).IsUnique();
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
