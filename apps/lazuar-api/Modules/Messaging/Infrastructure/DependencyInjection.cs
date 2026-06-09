using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.Messaging.Application;
using Modules.Messaging.Infrastructure.EventHandlers;
using Modules.Messaging.Contracts;
using Modules.One.Contracts;

namespace Modules.Messaging.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMessagingModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MessagingConnection") ?? throw new InvalidOperationException("MessagingConnection string not found.");

        services.AddDbContext<MessagingDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "messaging");
            }));

        services.AddKeyedScoped<ISqlConnectionFactory, NpgsqlConnectionFactory>("MessagingSqlConnectionFactory", (sp, key) => new NpgsqlConnectionFactory(connectionString));
        services.AddScoped<ITenantReplicaRepository, TenantReplicaRepository>();

        services.AddKeyedScoped<IEventBus, OutboxEventBus<MessagingDbContext>>("MessagingEventBus");

        services.AddTransient<TenantProvisionedIntegrationEventHandler>();
        services.AddTransient<TenantUpdatedIntegrationEventHandler>();
        services.AddTransient<WorkspaceUpdatedIntegrationEventHandler>();
        services.AddTransient<DispatchMessageIntegrationEventHandler>();

        services.AddHostedService<MessagingOutboxPublisherJob>();
        services.AddHostedService<MessagingInboxConsumerJob>();

        return services;
    }

    public static IApplicationBuilder UseMessagingSubscriptions(this IApplicationBuilder app)
    {
        var eventBus = app.ApplicationServices.GetRequiredService<IEventBusSubscriptions>();
        
        eventBus.Subscribe<TenantProvisionedIntegrationEvent, TenantProvisionedIntegrationEventHandler>();
        eventBus.Subscribe<TenantUpdatedIntegrationEvent, TenantUpdatedIntegrationEventHandler>();
        eventBus.Subscribe<WorkspaceUpdatedIntegrationEvent, WorkspaceUpdatedIntegrationEventHandler>();
        eventBus.Subscribe<DispatchMessageIntegrationEvent, DispatchMessageIntegrationEventHandler>();

        return app;
    }
}
