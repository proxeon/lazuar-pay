using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.Payments.Application.Ports;
using Modules.Payments.Infrastructure.Gateways;
using Modules.Payments.Infrastructure.Repositories;
using Modules.Payments.Infrastructure.Workers;
using Modules.Payments.Infrastructure.EventHandlers;
using Modules.Payments.Contracts.Events;

namespace Modules.Payments.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        services.AddDbContext<PaymentsDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "payments");
            }));

        services.AddScoped<ITenantPaymentConfigRepository, TenantPaymentConfigRepository>();
        services.AddScoped<IPaymentWebhookLogRepository, PaymentWebhookLogRepository>();

        services.AddScoped<IPaymentGatewayAdapter, StripeGatewayAdapter>();
        services.AddScoped<IPaymentGatewayAdapter, BillplzGatewayAdapter>();
        services.AddScoped<IPaymentGatewayFactory, PaymentGatewayFactory>();

        services.AddKeyedScoped<IEventBus, OutboxEventBus<PaymentsDbContext>>("PaymentsEventBus");

        services.AddHostedService<PaymentsInboxConsumerJob>();
        services.AddHostedService<PaymentsOutboxPublisherJob>();

        services.AddTransient<GatewayRefundRequestedIntegrationEventHandler>();

        return services;
    }

    public static IApplicationBuilder UsePaymentsSubscriptions(this IApplicationBuilder app)
    {
        var eventBus = app.ApplicationServices.GetRequiredService<IEventBusSubscriptions>();
        
        eventBus.Subscribe<GatewayRefundRequestedIntegrationEvent, GatewayRefundRequestedIntegrationEventHandler>();

        return app;
    }
}
