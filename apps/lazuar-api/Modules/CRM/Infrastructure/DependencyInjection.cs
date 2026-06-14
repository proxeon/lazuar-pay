using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.CRM.Contracts;
using Modules.CRM.Infrastructure.EventHandlers;
using Modules.One.Contracts;

namespace Modules.CRM.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCrmModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Default connection string was not found.");

        services.AddDbContext<CrmDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "crm");
            }));

        services.AddKeyedScoped<IEventBus, OutboxEventBus<CrmDbContext>>("CrmEventBus");
        services.AddScoped<ICrmQueryService, CrmQueryService>();

        services.AddTransient<GlobalUserProfileUpdatedIntegrationEventHandler>();

        return services;
    }

    public static IApplicationBuilder UseCrmSubscriptions(this IApplicationBuilder app)
    {
        var eventBus = app.ApplicationServices.GetRequiredService<IEventBusSubscriptions>();

        eventBus.Subscribe<GlobalUserProfileUpdatedIntegrationEvent, GlobalUserProfileUpdatedIntegrationEventHandler>();

        return app;
    }
}
