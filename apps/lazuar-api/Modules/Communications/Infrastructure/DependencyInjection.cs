using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.Commerce.Contracts.Events;
using Modules.One.Contracts;
using Modules.Communications.Infrastructure.EventHandlers;
using Modules.Communications.Infrastructure.Workers;

namespace Modules.Communications.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCommunicationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Default connection string not found.");

        services.AddDbContext<CommunicationsDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "communications");
            }));

        services.AddKeyedScoped<IEventBus, OutboxEventBus<CommunicationsDbContext>>("CommunicationsEventBus");

        services.AddHostedService<CommunicationsInboxConsumerJob>();
        services.AddHostedService<CommunicationsOutboxPublisherJob>();

        services.AddTransient<AppEntitlementGrantedIntegrationEventHandler>();
        services.AddTransient<LifecycleEventHandlers>();

        return services;
    }

    public static IApplicationBuilder UseCommunicationsSubscriptions(this IApplicationBuilder app)
    {
        var eventBus = app.ApplicationServices.GetRequiredService<IEventBusSubscriptions>();
        eventBus.Subscribe<AppEntitlementGrantedIntegrationEvent, AppEntitlementGrantedIntegrationEventHandler>();
        eventBus.Subscribe<SubscriptionSuspendedIntegrationEvent, LifecycleEventHandlers>();
        eventBus.Subscribe<SubscriptionCanceledIntegrationEvent, LifecycleEventHandlers>();
        return app;
    }
}
