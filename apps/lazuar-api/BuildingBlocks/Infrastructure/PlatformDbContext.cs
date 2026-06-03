using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;

namespace BuildingBlocks.Infrastructure;

public abstract class PlatformDbContext : DbContext
{
    protected readonly IExecutionContextAccessor ExecutionContext;

    protected PlatformDbContext(DbContextOptions options, IExecutionContextAccessor executionContext) : base(options)
    {
        ExecutionContext = executionContext;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 1. Automatically populate OrganizationId for Multi-Tenancy
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                var orgIdProp = entry.Entity.GetType().GetProperty("OrganizationId");
                if (orgIdProp != null && ExecutionContext.TenantId != Guid.Empty)
                {
                    var currentVal = (Guid)orgIdProp.GetValue(entry.Entity)!;
                    if (currentVal == Guid.Empty)
                    {
                        orgIdProp.SetValue(entry.Entity, ExecutionContext.TenantId);
                    }
                }
            }
        }

        // 2. Automatically capture and serialize Domain Events to the local Outbox
        var entitiesWithEvents = ChangeTracker.Entries<Entity>()
            .Where(entry => entry.Entity.DomainEvents.Any())
            .Select(entry => entry.Entity)
            .ToList();

        var outboxMessages = new List<OutboxMessage>();

        foreach (var entity in entitiesWithEvents)
        {
            foreach (var domainEvent in entity.DomainEvents)
            {
                outboxMessages.Add(new OutboxMessage
                {
                    Id = domainEvent.Id,
                    Type = domainEvent.GetType().AssemblyQualifiedName ?? domainEvent.GetType().FullName!,
                    Data = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                    OccurredOn = domainEvent.OccurredOn
                });
            }

            entity.ClearDomainEvents();
        }

        if (outboxMessages.Count > 0)
        {
            await Set<OutboxMessage>().AddRangeAsync(outboxMessages, cancellationToken);
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
