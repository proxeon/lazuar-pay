using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.Tenant.Contracts;

namespace Modules.Tenant.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTenantModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TenantConnection")
            ?? throw new InvalidOperationException("TenantConnection connection string was not found.");

        services.AddDbContext<TenantDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "tenant");
            }));

        // Register the SQL Connection Factory specifically for Dapper queries inside this module
        services.AddKeyedScoped<ISqlConnectionFactory, NpgsqlConnectionFactory>("TenantSqlConnectionFactory", (sp, key) => 
            new NpgsqlConnectionFactory(connectionString));

        services.AddScoped<ITenantQueryService, TenantQueryService>();

        // Overrides global IEventBus with scoped outbox-backed writer for Tenant DbContext
        services.AddScoped<IEventBus, OutboxEventBus<TenantDbContext>>();

        services.AddHostedService<TenantOutboxPublisherJob>();

        return services;
    }
}
