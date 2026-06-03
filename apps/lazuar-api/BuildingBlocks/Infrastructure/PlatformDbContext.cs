using MediatR;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;

namespace BuildingBlocks.Infrastructure;

public abstract class PlatformDbContext : DbContext
{
    protected readonly IExecutionContextAccessor ExecutionContext;
    protected readonly IMediator Mediator;

    protected PlatformDbContext(DbContextOptions options, IExecutionContextAccessor executionContext, IMediator mediator) : base(options)
    {
        ExecutionContext = executionContext;
        Mediator = mediator;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Dynamically apply the Global Query Filter to all entities implementing IMustHaveTenant
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
        // 1. Automatically populate OrganizationId for Multi-Tenancy
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

        // 2. Pre-commit Domain Event Dispatch (Synchronous/In-Transaction)
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
                // Handlers for these can mutate other entities in the same transaction
                // or explicitly add an IIntegrationEvent to the DbSet<OutboxMessage>
                await Mediator.Publish(domainEvent, cancellationToken);
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
