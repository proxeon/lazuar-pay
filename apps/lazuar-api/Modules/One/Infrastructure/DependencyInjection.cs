using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.One.Application; // <-- ADDED
using Modules.One.Contracts;
using Modules.One.Infrastructure.Services;
using Modules.One.Infrastructure.Repositories; // <-- ADDED
using Modules.One.Infrastructure.Workers;
using Modules.One.Application.IntegrationEvents;
using Modules.Community.Contracts;
using Microsoft.AspNetCore.Builder;

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

        services.AddKeyedScoped<IEventBus, OutboxEventBus<OneDbContext>>("OneEventBus");

        services.AddHostedService<OneInboxConsumerJob>();
        services.AddHostedService<OneOutboxPublisherJob>();

        services.AddTransient<CommunitySubscriptionActivatedIntegrationEventHandler>();

        return services;
    }

    public static IApplicationBuilder UseOneSubscriptions(this IApplicationBuilder app)
    {
        var eventBus = app.ApplicationServices.GetRequiredService<IEventBusSubscriptions>();
        
        eventBus.Subscribe<CommunitySubscriptionActivatedIntegrationEvent, CommunitySubscriptionActivatedIntegrationEventHandler>();

        return app;
    }
}
