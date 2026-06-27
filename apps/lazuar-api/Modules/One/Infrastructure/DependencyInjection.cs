using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.One.Application;
using Modules.One.Contracts;
using Modules.One.Infrastructure.Services;
using Modules.One.Infrastructure.Repositories;
using Modules.One.Infrastructure.Workers;
using Modules.One.Infrastructure.EventHandlers;
using Modules.Payments.Contracts.Events;
using Microsoft.AspNetCore.Builder;
using System;

namespace Modules.One.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOneModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Default connection string was not found.");

        services.AddDbContext<OneDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "one");
            }));

        services.AddKeyedScoped<ISqlConnectionFactory, NpgsqlConnectionFactory>("OneSqlConnectionFactory", (sp, key) =>
            new NpgsqlConnectionFactory(connectionString));

        services.AddScoped<IOneQueryService, OneQueryService>();
        services.AddScoped<IOneRepository, OneRepository>();

        services.AddSingleton<ITokenGeneratorService, TokenGeneratorService>();
        services.AddSingleton<IOneLinkService, OneLinkService>();

        services.AddHttpClient("DeveloperWebhooks", client => {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddKeyedScoped<IEventBus, OutboxEventBus<OneDbContext>>("OneEventBus");

        services.AddHostedService<SystemGenesisBootstrapperJob>();
        services.AddHostedService<OneInboxConsumerJob>();
        services.AddHostedService<OneOutboxPublisherJob>();
        services.AddHostedService<OutboundWebhookDispatcherJob>();

        services.AddTransient<OutboundWebhookEventHandlers>();

        return services;
    }

    public static IApplicationBuilder UseOneSubscriptions(this IApplicationBuilder app)
    {
        var eventBus = app.ApplicationServices.GetRequiredService<IEventBusSubscriptions>();
        eventBus.Subscribe<GatewayPaymentCompletedIntegrationEvent, OutboundWebhookEventHandlers>();

        return app;
    }
}
