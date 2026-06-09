using Lazuar.Api.Modules.Tenant.Data.Entities;
using Lazuar.Api.Modules.Tenant.Data.Configurations;
using Lazuar.Api.Modules.SaaSBilling.Data.Entities;
using Lazuar.Api.Modules.SaaSBilling.Data.Configurations;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Application;

namespace Lazuar.Api.Infrastructure.Data;

public class AppDbContext : PlatformDbContext
{
    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        IExecutionContextAccessor executionContext) : base(options, executionContext)
    {
    }

    public DbSet<OrganizationEntity> Organizations => Set<OrganizationEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<BranchEntity> Branches => Set<BranchEntity>();
    public DbSet<PackageEntity> Packages => Set<PackageEntity>();
    public DbSet<SystemConfigEntity> SystemConfigs => Set<SystemConfigEntity>();
    public DbSet<BranchConfigEntity> BranchConfigs => Set<BranchConfigEntity>();
    public DbSet<PlatformConfigEntity> PlatformConfigs => Set<PlatformConfigEntity>();
    public DbSet<AddonEntity> Addons => Set<AddonEntity>();
    public DbSet<ClientProfileEntity> ClientProfiles => Set<ClientProfileEntity>();
    public DbSet<TenantPaymentConfigEntity> TenantPaymentConfigs => Set<TenantPaymentConfigEntity>();
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();

    public DbSet<OnboardingStepEntity> OnboardingSteps => Set<OnboardingStepEntity>();
    public DbSet<TrialCodeEntity> TrialCodes => Set<TrialCodeEntity>();

    public DbSet<BillingEventLogEntity> BillingEventLogs => Set<BillingEventLogEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        bool isInMemory = Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";

        b.Entity<OrganizationEntity>().HasQueryFilter(e => TenantId == Guid.Empty || e.Id == TenantId);
        b.Entity<UserEntity>().HasQueryFilter(e => !e.IsDeleted && (TenantId == Guid.Empty || e.OrganizationId == TenantId));
        b.Entity<BranchEntity>().HasQueryFilter(e => !e.IsDeleted && (TenantId == Guid.Empty || e.OrganizationId == TenantId));
        b.Entity<PackageEntity>().HasQueryFilter(e => !e.IsDeleted && (TenantId == Guid.Empty || e.OrganizationId == TenantId));
        b.Entity<SystemConfigEntity>().HasQueryFilter(e => TenantId == Guid.Empty || e.OrganizationId == TenantId);
        b.Entity<AddonEntity>().HasQueryFilter(e => !e.IsDeleted && (TenantId == Guid.Empty || e.OrganizationId == TenantId));
        b.Entity<ClientProfileEntity>().HasQueryFilter(e => TenantId == Guid.Empty || e.OrganizationId == TenantId);
        b.Entity<TenantPaymentConfigEntity>().HasQueryFilter(e => TenantId == Guid.Empty || e.OrganizationId == TenantId);
        b.Entity<OnboardingStepEntity>().HasQueryFilter(e => TenantId == Guid.Empty || e.OrganizationId == TenantId);

        b.ApplyConfiguration(new OrganizationConfiguration());
        b.ApplyConfiguration(new UserConfiguration(isInMemory));
        b.ApplyConfiguration(new BranchConfiguration(isInMemory));
        b.ApplyConfiguration(new PackageConfiguration());
        b.ApplyConfiguration(new SystemConfigConfiguration(isInMemory));
        b.ApplyConfiguration(new BranchConfigConfiguration());
        b.ApplyConfiguration(new PlatformConfigConfiguration(isInMemory));
        b.ApplyConfiguration(new AddonConfiguration());
        b.ApplyConfiguration(new ClientProfileConfiguration());
        b.ApplyConfiguration(new TenantPaymentConfigConfiguration());
        b.ApplyConfiguration(new AuditLogConfiguration());

        b.ApplyConfiguration(new BillingEventLogConfiguration());
        b.ApplyConfiguration(new TrialCodeConfiguration());
        b.ApplyConfiguration(new OnboardingStepConfiguration());
    }
}
