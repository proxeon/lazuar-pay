using BuildingBlocks.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Community.Application;
using Modules.Community.Application.IntegrationEvents;
using Modules.Community.Infrastructure.Repositories;
using Modules.Community.Infrastructure.Services;
using Modules.Community.Infrastructure.Workers;
using Modules.Payments.Contracts.Events; 

namespace Modules.Community.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCommunityModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        services.AddDbContext<CommunityDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "community");
            }));

        services.AddScoped<ICommunityPlanRepository, CommunityPlanRepository>();
        services.AddScoped<ICommunitySubscriptionRepository, CommunitySubscriptionRepository>();
        services.AddSingleton<IMagicLinkTokenService, MagicLinkTokenService>();

        // Background Workers
        services.AddHostedService<CommunityInboxConsumerJob>();
        services.AddHostedService<CommunityOutboxPublisherJob>();
        services.AddHostedService<CommunityLifecycleJob>();

        // Inbox Handlers
        services.AddTransient<GatewayPaymentCompletedIntegrationEventHandler>();

        return services;
    }

    public static IApplicationBuilder UseCommunitySubscriptions(this IApplicationBuilder app)
    {
        var eventBus = app.ApplicationServices.GetRequiredService<IEventBus>();
        
        // Listen to events from the Payments module
        eventBus.Subscribe<GatewayPaymentCompletedIntegrationEvent, GatewayPaymentCompletedIntegrationEventHandler>();

        return app;
    }
}
