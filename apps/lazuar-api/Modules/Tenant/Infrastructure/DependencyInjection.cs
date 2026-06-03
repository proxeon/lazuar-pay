// apps/lazuar-api/Modules/Tenant/Infrastructure/DependencyInjection.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Tenant.Contracts;

namespace Modules.Tenant.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTenantModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind to Tenant-specific connection pool
        var connectionString = configuration.GetConnectionString("TenantConnection");

        services.AddDbContext<TenantDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "tenant");
            }));

        services.AddScoped<ITenantQueryService, TenantQueryService>();
        services.AddHostedService<TenantOutboxPublisherJob>();

        return services;
    }
}
