using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using BuildingBlocks.Infrastructure.Observability;
using Modules.Messaging.Application;
using Modules.Messaging.Infrastructure.Configuration;
using Modules.Messaging.Infrastructure.Email;
using Modules.Messaging.Infrastructure.EventHandlers;
using Modules.Messaging.Infrastructure.Messaging;
using Modules.Messaging.Infrastructure.Workers;
using Modules.Messaging.Contracts;
using Modules.One.Contracts;

namespace Modules.Messaging.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMessagingModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MessagingConnection") ?? throw new InvalidOperationException("MessagingConnection string not found.");

        services.AddDbContext<MessagingDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "messaging");
            }));

        services.AddKeyedScoped<ISqlConnectionFactory, NpgsqlConnectionFactory>("MessagingSqlConnectionFactory", (sp, key) => new NpgsqlConnectionFactory(connectionString));
        services.AddScoped<ITenantReplicaRepository, TenantReplicaRepository>();

        services.AddKeyedScoped<IEventBus, OutboxEventBus<MessagingDbContext>>("MessagingEventBus");

        // R34 — email + channel ports owned by Messaging (not BuildingBlocks).
        // Named HttpClient "Resend" is also used by Communications SaveEmailConfig (domains validation).
        services.AddOptions<ResendOptions>().BindConfiguration(ResendOptions.SectionName);
        services.AddHttpClient("Resend", (sp, client) =>
        {
            client.BaseAddress = new Uri("https://api.resend.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
            var options = sp.GetRequiredService<IOptions<ResendOptions>>().Value;
            if (!string.IsNullOrEmpty(options.ApiKey))
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.ApiKey}");
            }
        });
        services.AddSingleton<IEmailService, ResendEmailService>();
        services.AddSingleton<IMessagingService, ConsoleMessagingService>();

        services.AddTransient<TenantProvisionedIntegrationEventHandler>();
        services.AddTransient<TenantUpdatedIntegrationEventHandler>();
        services.AddTransient<WorkspaceUpdatedIntegrationEventHandler>();
        services.AddTransient<DispatchMessageIntegrationEventHandler>();

        services.AddOutboxSchemaMetrics("messaging");
        services.AddHostedService<MessagingOutboxPublisherJob>();
        services.AddHostedService<MessagingInboxConsumerJob>();

        return services;
    }

    public static IApplicationBuilder UseMessagingSubscriptions(this IApplicationBuilder app)
    {
        var eventBus = app.ApplicationServices.GetRequiredService<IEventBusSubscriptions>();

        eventBus.Subscribe<TenantProvisionedIntegrationEvent, TenantProvisionedIntegrationEventHandler>();
        eventBus.Subscribe<TenantUpdatedIntegrationEvent, TenantUpdatedIntegrationEventHandler>();
        eventBus.Subscribe<WorkspaceUpdatedIntegrationEvent, WorkspaceUpdatedIntegrationEventHandler>();
        eventBus.Subscribe<DispatchMessageIntegrationEvent, DispatchMessageIntegrationEventHandler>();

        return app;
    }
}
