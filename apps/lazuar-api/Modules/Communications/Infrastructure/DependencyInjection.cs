using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using BuildingBlocks.Infrastructure.Observability;
using Modules.Commerce.Contracts.Events;
using Modules.Billing.Contracts.Events;
using Modules.One.Contracts;
using Modules.CRM.Contracts;
using Modules.Communications.Application;
using Modules.Communications.Contracts;
using Modules.Communications.Infrastructure.Repositories;
using Modules.Communications.Infrastructure.Services;
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

        services.AddKeyedScoped<ISqlConnectionFactory, NpgsqlConnectionFactory>("CommunicationsSqlConnectionFactory", (sp, key) =>
            new NpgsqlConnectionFactory(connectionString));

        services.AddScoped<ICommunicationsRepository, CommunicationsRepository>();
        services.AddScoped<ICommunicationsQueryService, CommunicationsQueryService>();
        services.AddScoped<ISuppressionService, SuppressionService>();

        services.AddKeyedScoped<IEventBus, OutboxEventBus<CommunicationsDbContext>>("CommunicationsEventBus");

        services.AddHostedService<CommunicationsInboxConsumerJob>();
        services.AddOutboxSchemaMetrics("communications");
        services.AddHostedService<CommunicationsOutboxPublisherJob>();
        services.AddHostedService<BroadcastFanoutJob>();

        services.AddTransient<AppEntitlementGrantedIntegrationEventHandler>();
        services.AddTransient<LifecycleEventHandlers>();
        services.AddTransient<FulfillmentRequestedIntegrationEventHandler>();
        services.AddTransient<DocumentPublishedIntegrationEventHandler>();
        services.AddTransient<ClientProfileAnonymizedIntegrationEventHandler>();
        services.AddTransient<OrderCompletedDigitalDeliveryHandler>();

        return services;
    }

    public static IApplicationBuilder UseCommunicationsSubscriptions(this IApplicationBuilder app)
    {
        var eventBus = app.ApplicationServices.GetRequiredService<IEventBusSubscriptions>();
        eventBus.Subscribe<AppEntitlementGrantedIntegrationEvent, AppEntitlementGrantedIntegrationEventHandler>();
        eventBus.Subscribe<SubscriptionSuspendedIntegrationEvent, LifecycleEventHandlers>();
        eventBus.Subscribe<SubscriptionCanceledIntegrationEvent, LifecycleEventHandlers>();
        eventBus.Subscribe<FulfillmentRequestedIntegrationEvent, FulfillmentRequestedIntegrationEventHandler>();
        eventBus.Subscribe<DocumentPublishedIntegrationEvent, DocumentPublishedIntegrationEventHandler>();
        eventBus.Subscribe<ClientProfileAnonymizedIntegrationEvent, ClientProfileAnonymizedIntegrationEventHandler>();
        eventBus.Subscribe<OrderCompletedIntegrationEvent, OrderCompletedDigitalDeliveryHandler>();
        return app;
    }
}
