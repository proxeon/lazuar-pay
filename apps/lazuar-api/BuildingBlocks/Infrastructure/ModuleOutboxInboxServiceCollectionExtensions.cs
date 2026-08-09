using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Infrastructure;

/// <summary>
/// Option A outbox/inbox DI helper: keyed <see cref="OutboxEventBus{TDbContext}"/> plus
/// thin concrete hosted job subclasses (preserves arch-test type names and typed loggers).
/// </summary>
public static class ModuleOutboxInboxServiceCollectionExtensions
{
    /// <summary>
    /// Registers module outbox event bus + outbox publisher + inbox consumer hosted services.
    /// Call from each module's <c>Add*Module</c> next to <c>AddOutboxSchemaMetrics</c>.
    /// </summary>
    public static IServiceCollection AddModuleOutboxInbox<TDbContext, TOutboxJob, TInboxJob>(
        this IServiceCollection services,
        string eventBusKey)
        where TDbContext : DbContext
        where TOutboxJob : OutboxPublisherJob<TDbContext>
        where TInboxJob : InboxConsumerJob<TDbContext>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventBusKey);

        services.AddKeyedScoped<IEventBus, OutboxEventBus<TDbContext>>(eventBusKey);
        services.AddHostedService<TOutboxJob>();
        services.AddHostedService<TInboxJob>();
        return services;
    }
}
