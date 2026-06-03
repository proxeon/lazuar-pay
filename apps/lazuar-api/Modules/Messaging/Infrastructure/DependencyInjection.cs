using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlocks.Application;
using Modules.Messaging.Application;
using Modules.Tenant.Contracts;

namespace Modules.Messaging.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMessagingModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        services.AddDbContext<MessagingDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "messaging");
            }));

        services.AddScoped<ITenantReplicaRepository, TenantReplicaRepository>();

        // Register Transient Integration Event Handlers for memory event bus resolution
        services.AddTransient<TenantCreatedIntegrationEventHandler>();
        services.AddTransient<TenantUpdatedIntegrationEventHandler>();

        // Register local background worker for Outbox
        services.AddHostedService<MessagingOutboxPublisherJob>();

        return services;
    }

    public static IApplicationBuilder UseMessagingSubscriptions(this IApplicationBuilder app)
    {
        var eventBus = app.ApplicationServices.GetRequiredService<IEventBus>();
        
        eventBus.Subscribe<TenantCreatedIntegrationEvent, TenantCreatedIntegrationEventHandler>();
        eventBus.Subscribe<TenantUpdatedIntegrationEvent, TenantUpdatedIntegrationEventHandler>();

        return app;
    }
}
