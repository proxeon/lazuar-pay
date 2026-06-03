using MediatR;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;

namespace BuildingBlocks.Infrastructure;

public abstract class PlatformDbContext : DbContext
{
    protected readonly IExecutionContextAccessor ExecutionContext;
    protected readonly IMediator Mediator;
    protected readonly DatabaseJobTrigger JobTrigger;

    protected PlatformDbContext(
        DbContextOptions options, 
        IExecutionContextAccessor executionContext, 
        IMediator mediator, 
        DatabaseJobTrigger jobTrigger) : base(options)
    {
        ExecutionContext = executionContext;
        Mediator = mediator;
        JobTrigger = jobTrigger;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IMustHaveTenant).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(PlatformDbContext)
                    .GetMethod(nameof(ConfigureGlobalFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.MakeGenericMethod(entityType.ClrType);
                method?.Invoke(this, new object[] { modelBuilder });
            }
        }
    }

    private void ConfigureGlobalFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, IMustHaveTenant
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => 
            ExecutionContext.TenantId == Guid.Empty || e.OrganizationId == ExecutionContext.TenantId);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added && entry.Entity is IMustHaveTenant tenantEntity)
            {
                if (tenantEntity.OrganizationId == Guid.Empty && ExecutionContext.TenantId != Guid.Empty)
                {
                    tenantEntity.OrganizationId = ExecutionContext.TenantId;
                }
            }
        }

        var entitiesWithEvents = ChangeTracker.Entries<Entity>()
            .Where(entry => entry.Entity.DomainEvents.Any())
            .Select(entry => entry.Entity)
            .ToList();

        foreach (var entity in entitiesWithEvents)
        {
            var events = entity.DomainEvents.ToList();
            entity.ClearDomainEvents();
            
            foreach (var domainEvent in events)
            {
                await Mediator.Publish(domainEvent, cancellationToken);
            }
        }

        var result = await base.SaveChangesAsync(cancellationToken);
        
        // Phase 2: If rows were modified, wake up the background workers instantly!
        if (result > 0)
        {
            JobTrigger.Trigger();
        }

        return result;
    }
}
