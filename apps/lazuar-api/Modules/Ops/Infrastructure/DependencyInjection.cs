using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.Ops.Application;
using Modules.Ops.Application.Llm;
using Modules.Ops.Application.Services;
using Modules.Ops.Infrastructure.Llm;
using Modules.Ops.Infrastructure.Repositories;
using Modules.Ops.Infrastructure.Services;
using Modules.Ops.Infrastructure.Workers;

namespace Modules.Ops.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOpsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Default connection string was not found.");

        services.AddDbContext<OpsDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "ops");
            }));

        services.AddKeyedScoped<ISqlConnectionFactory, NpgsqlConnectionFactory>("OpsSqlConnectionFactory", (sp, key) =>
            new NpgsqlConnectionFactory(connectionString));

        services.AddSingleton<IChatClientFactory, ChatClientFactory>();
        services.AddScoped<ILlmTitleGenerator, LlmTitleGenerator>();
        services.AddSingleton<IToolRegistry, ToolRegistry>();
        services.AddScoped<ILlmOrchestratorService, LlmOrchestratorService>();
        services.AddScoped<IOpsRepository, OpsRepository>();

        services.AddKeyedScoped<IEventBus, OutboxEventBus<OpsDbContext>>("OpsEventBus");

        services.AddHostedService<OpsInboxConsumerJob>();
        services.AddHostedService<OpsOutboxPublisherJob>();

        return services;
    }

    public static IApplicationBuilder UseOpsSubscriptions(this IApplicationBuilder app)
    {
        return app;
    }
}
