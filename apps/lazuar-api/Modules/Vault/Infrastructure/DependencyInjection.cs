using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.Commerce.Contracts.Events;
using Modules.Vault.Application.Queries;
using Modules.Vault.Infrastructure.EventHandlers;
using Modules.Vault.Infrastructure.Services;
using Modules.Vault.Infrastructure.Workers;

namespace Modules.Vault.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddVaultModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Default connection string not found.");

        services.AddDbContext<VaultDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "vault");
            }));

        services.AddKeyedScoped<ISqlConnectionFactory, NpgsqlConnectionFactory>("VaultSqlConnectionFactory", (sp, key) =>
            new NpgsqlConnectionFactory(connectionString));

        services.AddKeyedScoped<IEventBus, OutboxEventBus<VaultDbContext>>("VaultEventBus");

        services.AddScoped<Modules.Vault.Application.IVaultRepository, Modules.Vault.Infrastructure.Repositories.VaultRepository>();
        services.AddScoped<IVaultQueryService, VaultQueryService>();

        services.AddHostedService<VaultInboxConsumerJob>();
        services.AddHostedService<VaultOutboxPublisherJob>();

        services.AddTransient<FulfillmentRequestedIntegrationEventHandler>();

        return services;
    }

    public static IApplicationBuilder UseVaultSubscriptions(this IApplicationBuilder app)
    {
        var eventBus = app.ApplicationServices.GetRequiredService<IEventBusSubscriptions>();
        eventBus.Subscribe<FulfillmentRequestedIntegrationEvent, FulfillmentRequestedIntegrationEventHandler>();
        return app;
    }
}
