using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Billing.Application;
using Modules.Billing.Infrastructure.EventHandlers;
using Modules.Billing.Infrastructure.Repositories;
using Modules.Billing.Infrastructure.Workers;
using Modules.Community.Contracts;
using Modules.Payments.Contracts.Events;

namespace Modules.Billing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBillingModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Default connection string was not found.");

        services.AddDbContext<BillingDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "billing");
            }));

        services.AddKeyedScoped<ISqlConnectionFactory, NpgsqlConnectionFactory>("BillingSqlConnectionFactory", (sp, key) =>
            new NpgsqlConnectionFactory(connectionString));

        services.AddKeyedScoped<IEventBus, OutboxEventBus<BillingDbContext>>("BillingEventBus");
        
        services.AddScoped<ILedgerRepository, LedgerRepository>();

        services.AddTransient<GatewayPaymentCompletedHandler>();
        services.AddTransient<ZeroAmountCheckoutHandler>();
        services.AddTransient<GatewayRefundCompletedHandler>();

        services.AddHostedService<BillingInboxConsumerJob>();
        services.AddHostedService<BillingOutboxPublisherJob>();

        return services;
    }

    public static IApplicationBuilder UseBillingSubscriptions(this IApplicationBuilder app)
    {
        var eventBus = app.ApplicationServices.GetRequiredService<IEventBusSubscriptions>();
        
        eventBus.Subscribe<GatewayPaymentCompletedIntegrationEvent, GatewayPaymentCompletedHandler>();
        eventBus.Subscribe<ZeroAmountCheckoutCompletedIntegrationEvent, ZeroAmountCheckoutHandler>();
        eventBus.Subscribe<GatewayRefundCompletedIntegrationEvent, GatewayRefundCompletedHandler>();

        return app;
    }
}
