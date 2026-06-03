using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Modules.Payments.Application.Ports;
using Modules.Payments.Infrastructure.Gateways;
using Modules.Payments.Infrastructure.Repositories;
using Modules.Payments.Infrastructure.Workers;

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

        // Repositories
        services.AddScoped<ITenantPaymentConfigRepository, TenantPaymentConfigRepository>();
        services.AddScoped<IPaymentWebhookLogRepository, PaymentWebhookLogRepository>();

        // Gateways
        services.AddScoped<IPaymentGatewayAdapter, StripeGatewayAdapter>();
        services.AddScoped<IPaymentGatewayFactory, PaymentGatewayFactory>();

        // Background Workers
        services.AddHostedService<PaymentsInboxConsumerJob>();
        services.AddHostedService<PaymentsOutboxPublisherJob>();

        return services;
    }
}
