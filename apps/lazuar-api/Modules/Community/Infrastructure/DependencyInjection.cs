using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Community.Application;
using Modules.Community.Application.IntegrationEvents;
using Modules.Community.Application.Queries;
using Modules.Community.Infrastructure.EventHandlers;
using Modules.Community.Infrastructure.Repositories;
using Modules.Community.Infrastructure.Services;
using Modules.Community.Infrastructure.Workers;
using Modules.Payments.Contracts.Events;
using Modules.One.Contracts;
using Modules.CRM.Contracts;

namespace Modules.Community.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCommunityModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Default connection string not found.");

        services.AddDbContext<CommunityDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "community");
            }));

        services.AddKeyedScoped<ISqlConnectionFactory, NpgsqlConnectionFactory>("CommunitySqlConnectionFactory", (sp, key) =>
            new NpgsqlConnectionFactory(connectionString));

        services.AddScoped<ICommunityPlanRepository, CommunityPlanRepository>();
        services.AddScoped<ICommunitySubscriptionRepository, CommunitySubscriptionRepository>();
        services.AddScoped<ICommunityReminderScheduleRepository, CommunityReminderScheduleRepository>();
        services.AddScoped<ICommunityCouponRepository, CommunityCouponRepository>();
        services.AddScoped<IBroadcastCampaignRepository, CommunityBroadcastRepository>();
        
        services.AddSingleton<IMagicLinkTokenService, MagicLinkTokenService>();
        services.AddScoped<ICommunityQueryService, CommunityQueryService>();
        services.AddScoped<IMessageTemplateQueryService, MessageTemplateQueryService>();
        services.AddSingleton<ICommunityLinkService, CommunityLinkService>();
        
        services.AddKeyedScoped<IEventBus, OutboxEventBus<CommunityDbContext>>("CommunityEventBus");
        
        services.AddHostedService<CommunityInboxConsumerJob>();
        services.AddHostedService<CommunityOutboxPublisherJob>();
        services.AddHostedService<CommunityLifecycleJob>();
        services.AddHostedService<BroadcastPublisherJob>();
        
        services.AddTransient<GatewayPaymentCompletedIntegrationEventHandler>();
        services.AddTransient<AppEntitlementGrantedIntegrationEventHandler>();
        services.AddTransient<ClientProfileAnonymizedIntegrationEventHandler>();

        return services;
    }

    public static IApplicationBuilder UseCommunitySubscriptions(this IApplicationBuilder app)
    {
        var eventBus = app.ApplicationServices.GetRequiredService<IEventBusSubscriptions>();
        eventBus.Subscribe<GatewayPaymentCompletedIntegrationEvent, GatewayPaymentCompletedIntegrationEventHandler>();
        eventBus.Subscribe<AppEntitlementGrantedIntegrationEvent, AppEntitlementGrantedIntegrationEventHandler>();
        eventBus.Subscribe<ClientProfileAnonymizedIntegrationEvent, ClientProfileAnonymizedIntegrationEventHandler>();
        return app;
    }
}
