using BuildingBlocks.Application;
using BuildingBlocks.Application.Llm;
using BuildingBlocks.Infrastructure;
using Modules.Commerce.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Application;
using Modules.Commerce.Application.EventHandlers;
using Modules.Commerce.Application.Queries;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Infrastructure.Repositories;
using Modules.Commerce.Infrastructure.Services;
using Modules.Commerce.Infrastructure.Workers;
using Modules.Commerce.Infrastructure.EventHandlers;
using Modules.Payments.Contracts.Events;
using Modules.Communications.Contracts.Events;
using System;

namespace Modules.Commerce.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCommerceModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Default connection string not found.");

        services.AddDbContext<CommerceDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "commerce");
            }));

        services.AddKeyedScoped<ISqlConnectionFactory, NpgsqlConnectionFactory>("CommerceSqlConnectionFactory", (sp, key) =>
            new NpgsqlConnectionFactory(connectionString));

        services.AddScoped<ICommerceRepository, CommerceRepository>();
        services.AddScoped<ICommerceQueryService, CommerceQueryService>();
        services.AddScoped<ISubscriberQueryService, SubscriberQueryService>();

        services.AddKeyedScoped<IEventBus, OutboxEventBus<CommerceDbContext>>("CommerceEventBus");

        services.AddHostedService<CommerceInboxConsumerJob>();
        services.AddHostedService<CommerceOutboxPublisherJob>();
        
        // Dunning & Billing Deterministic Engines
        services.AddHostedService<BillingEngineJob>();
        services.AddHostedService<DunningEngineJob>();

        services.AddTransient<GatewayPaymentCompletedIntegrationEventHandler>();
        services.AddTransient<GatewayRefundCompletedIntegrationEventHandler>();
        services.AddTransient<OrderCompletedIntegrationEventHandler>();
        services.AddTransient<SubscriptionLifecycleIntegrationEventHandlers>();
        services.AddTransient<DefaultTemplatesSeededIntegrationEventHandler>();

        return services;
    }

    public static IApplicationBuilder UseCommerceSubscriptions(this IApplicationBuilder app)
    {
        var eventBus = app.ApplicationServices.GetRequiredService<IEventBusSubscriptions>();
        eventBus.Subscribe<GatewayPaymentCompletedIntegrationEvent, GatewayPaymentCompletedIntegrationEventHandler>();
        eventBus.Subscribe<GatewayRefundCompletedIntegrationEvent, GatewayRefundCompletedIntegrationEventHandler>();
        eventBus.Subscribe<OrderCompletedIntegrationEvent, OrderCompletedIntegrationEventHandler>();
        eventBus.Subscribe<SubscriptionActivatedIntegrationEvent, SubscriptionLifecycleIntegrationEventHandlers>();
        eventBus.Subscribe<SubscriptionSuspendedIntegrationEvent, SubscriptionLifecycleIntegrationEventHandlers>();
        eventBus.Subscribe<SubscriptionCanceledIntegrationEvent, SubscriptionLifecycleIntegrationEventHandlers>();
        eventBus.Subscribe<DefaultTemplatesSeededIntegrationEvent, DefaultTemplatesSeededIntegrationEventHandler>();
        return app;
    }
}
