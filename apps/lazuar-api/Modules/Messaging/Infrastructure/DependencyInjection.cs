using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.Messaging.Application;
using Modules.Messaging.Application.EventHandlers;
using Modules.Messaging.Contracts;
using Modules.Tenant.Contracts;
using Modules.Community.Contracts;

namespace Modules.Messaging.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMessagingModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MessagingConnection")
            ?? throw new InvalidOperationException("MessagingConnection string not found.");

        services.AddDbContext<MessagingDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "messaging");
            }));

        // Isolate Dapper read connection pool for Messaging read models
        services.AddKeyedScoped<ISqlConnectionFactory, NpgsqlConnectionFactory>("MessagingSqlConnectionFactory", (sp, key) => 
            new NpgsqlConnectionFactory(connectionString));

        services.AddScoped<ITenantReplicaRepository, TenantReplicaRepository>();
        
        // Register cross-module read service for Community module to access templates
        services.AddScoped<IMessageTemplateQueryService, MessageTemplateQueryService>();

        // Register Inbox Handlers
        services.AddTransient<TenantCreatedIntegrationEventHandler>();
        services.AddTransient<TenantUpdatedIntegrationEventHandler>();
        services.AddTransient<CommunityIntegrationEventHandlers>();

        services.AddHostedService<MessagingOutboxPublisherJob>();
        services.AddHostedService<MessagingInboxConsumerJob>();

        return services;
    }

    public static IApplicationBuilder UseMessagingSubscriptions(this IApplicationBuilder app)
    {
        var eventBus = app.ApplicationServices.GetRequiredService<IEventBus>();
        
        eventBus.Subscribe<TenantCreatedIntegrationEvent, TenantCreatedIntegrationEventHandler>();
        eventBus.Subscribe<TenantUpdatedIntegrationEvent, TenantUpdatedIntegrationEventHandler>();

        // Community Event Subscriptions
        eventBus.Subscribe<CommunitySubscriptionActivatedIntegrationEvent, CommunityIntegrationEventHandlers>();
        eventBus.Subscribe<CommunitySubscriptionCancelledIntegrationEvent, CommunityIntegrationEventHandlers>();
        eventBus.Subscribe<CommunityCheckoutInitiatedIntegrationEvent, CommunityIntegrationEventHandlers>();
        eventBus.Subscribe<CommunityRenewalReminderDueIntegrationEvent, CommunityIntegrationEventHandlers>();
        eventBus.Subscribe<CommunityMagicLinkRequestedIntegrationEvent, CommunityIntegrationEventHandlers>();
        
        // Subscribe to manual one-off reminders
        eventBus.Subscribe<CommunityOneOffReminderRequestedIntegrationEvent, CommunityIntegrationEventHandlers>();

        return app;
    }
}
