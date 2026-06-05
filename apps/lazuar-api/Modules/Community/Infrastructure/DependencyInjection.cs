using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Community.Application;
using Modules.Community.Application.IntegrationEvents;
using Modules.Community.Application.Queries;
using Modules.Community.Infrastructure.Repositories;
using Modules.Community.Infrastructure.Services;
using Modules.Community.Infrastructure.Workers;
using Modules.Payments.Contracts.Events;

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

        // Isolate Dapper read connection pool
        services.AddKeyedScoped<ISqlConnectionFactory, NpgsqlConnectionFactory>("CommunitySqlConnectionFactory", (sp, key) => 
            new NpgsqlConnectionFactory(connectionString));

        // Repositories & Services
        services.AddScoped<ICommunityPlanRepository, CommunityPlanRepository>();
        services.AddScoped<ICommunitySubscriptionRepository, CommunitySubscriptionRepository>();
        services.AddScoped<ICommunityReminderScheduleRepository, CommunityReminderScheduleRepository>();
        
        services.AddSingleton<IMagicLinkTokenService, MagicLinkTokenService>();
        services.AddScoped<ICommunityQueryService, CommunityQueryService>();
        services.AddSingleton<ICommunityLinkService, CommunityLinkService>();

        // Overrides global IEventBus with keyed scoped outbox-backed writer for Community DbContext
        services.AddKeyedScoped<IEventBus, OutboxEventBus<CommunityDbContext>>("CommunityEventBus");

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
        var eventBus = app.ApplicationServices.GetRequiredService<IEventBusSubscriptions>();
        
        eventBus.Subscribe<GatewayPaymentCompletedIntegrationEvent, GatewayPaymentCompletedIntegrationEventHandler>();
        
        return app;
    }
}
