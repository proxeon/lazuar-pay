using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.One.Contracts;
using Modules.One.Infrastructure.Services;

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

        services.AddKeyedScoped<IEventBus, OutboxEventBus<OneDbContext>>("OneEventBus");

        return services;
    }
}
