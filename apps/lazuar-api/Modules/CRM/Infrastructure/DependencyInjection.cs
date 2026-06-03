using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.CRM.Contracts;

namespace Modules.CRM.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCrmModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default") 
            ?? throw new InvalidOperationException("Default connection string was not found.");

        // Isolate read connection pool
        services.AddKeyedScoped<ISqlConnectionFactory, NpgsqlConnectionFactory>("CrmSqlConnectionFactory", (sp, key) => 
            new NpgsqlConnectionFactory(connectionString));

        services.AddScoped<ICrmQueryService, CrmQueryService>();

        return services;
    }
}
