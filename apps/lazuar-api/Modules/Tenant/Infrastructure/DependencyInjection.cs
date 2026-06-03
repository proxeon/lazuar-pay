using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Tenant.Contracts;

namespace Modules.Tenant.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTenantModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        services.AddDbContext<TenantDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "tenant");
            }));

        services.AddScoped<ITenantQueryService, TenantQueryService>();

        // Register local schema background worker for Outbox
        services.AddHostedService<TenantOutboxPublisherJob>();

        return services;
    }
}
