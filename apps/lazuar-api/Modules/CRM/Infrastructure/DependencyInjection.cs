using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.CRM.Contracts;

namespace Modules.CRM.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCrmModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default") 
            ?? throw new InvalidOperationException("Default connection string was not found.");

        // Register CRM DB Context
        services.AddDbContext<CrmDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "crm");
            }));

        // Override default global event bus to hook up with the transactional outbox of CrmDbContext
        services.AddKeyedScoped<IEventBus, OutboxEventBus<CrmDbContext>>("CrmEventBus");

        // Register Query Service
        services.AddScoped<ICrmQueryService, CrmQueryService>();

        return services;
    }
}
