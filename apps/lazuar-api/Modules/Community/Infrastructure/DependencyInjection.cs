using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.Community.Application;
using Modules.Community.Application.Queries;
using Modules.Community.Infrastructure.EventHandlers;
using Modules.Community.Infrastructure.Services;
using Modules.Community.Infrastructure.Workers;
using Modules.Commerce.Contracts.Events;

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

        services.AddScoped<Modules.Community.Application.ICommunitySpaceRepository, Modules.Community.Infrastructure.Repositories.CommunitySpaceRepository>();
        services.AddScoped<IBroadcastCampaignRepository, CommunityBroadcastRepository>();

        services.AddSingleton<IMagicLinkTokenService, MagicLinkTokenService>();
        services.AddScoped<ICommunityQueryService, CommunityQueryService>();
        services.AddSingleton<ICommunityLinkService, CommunityLinkService>();

        services.AddKeyedScoped<IEventBus, OutboxEventBus<CommunityDbContext>>("CommunityEventBus");

        services.AddHostedService<CommunityInboxConsumerJob>();
        services.AddHostedService<CommunityOutboxPublisherJob>();

        services.AddTransient<FulfillmentRequestedIntegrationEventHandler>();

        return services;
    }

    public static IApplicationBuilder UseCommunitySubscriptions(this IApplicationBuilder app)
    {
        var eventBus = app.ApplicationServices.GetRequiredService<IEventBusSubscriptions>();
        eventBus.Subscribe<FulfillmentRequestedIntegrationEvent, FulfillmentRequestedIntegrationEventHandler>();
        return app;
    }
}
