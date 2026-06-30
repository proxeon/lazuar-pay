using BuildingBlocks.Application;
using BuildingBlocks.Application.Llm;
using BuildingBlocks.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Billing.Application;
using Modules.Billing.Application.Llm;
using Modules.Billing.Contracts;
using Modules.Billing.Contracts.Events;
using Modules.Billing.Infrastructure.EventHandlers;
using Modules.Billing.Infrastructure.Commands;
using Modules.Billing.Infrastructure.Repositories;
using Modules.Billing.Infrastructure.Services;
using Modules.Billing.Infrastructure.Workers;
using Modules.Payments.Contracts.Events;
using Modules.Lhdn.Contracts.Events;
using Modules.Commerce.Contracts.Events;
using QuestPDF.Infrastructure;
using System;

namespace Modules.Billing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBillingModule(this IServiceCollection services, IConfiguration configuration)
    {
        QuestPDF.Settings.License = LicenseType.Community;

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
        services.AddScoped<IBillingQueryService, BillingQueryService>();

        services.AddTransient<GatewayPaymentCompletedHandler>();
        services.AddTransient<PlatformTopUpEventHandler>();
        services.AddTransient<GatewayRefundCompletedHandler>();
        services.AddTransient<InvoiceIssuedHandler>();
        services.AddTransient<CommissionAccruedHandler>();
        services.AddTransient<LhdnDocumentValidatedIntegrationEventHandler>();
        services.AddTransient<LhdnDocumentCancelledIntegrationEventHandler>();
        services.AddTransient<LhdnDocumentSubmittedIntegrationEventHandler>();
        services.AddTransient<ZeroAmountCheckoutHandler>();
        services.AddTransient<ManualSubscriberEnrolledIntegrationEventHandler>();

        services.AddHostedService<BillingInboxConsumerJob>();
        services.AddHostedService<BillingOutboxPublisherJob>();
        services.AddHostedService<RevenueRecognitionJob>();
        services.AddHostedService<B2cConsolidationJob>();

        services.AddSingleton<IAgentPromptProvider, BillingPromptProvider>();

        return services;
    }

    public static IApplicationBuilder UseBillingSubscriptions(this IApplicationBuilder app)
    {
        var eventBus = app.ApplicationServices.GetRequiredService<IEventBusSubscriptions>();
        
        eventBus.Subscribe<GatewayPaymentCompletedIntegrationEvent, GatewayPaymentCompletedHandler>();
        eventBus.Subscribe<GatewayPaymentCompletedIntegrationEvent, PlatformTopUpEventHandler>();
        eventBus.Subscribe<GatewayRefundCompletedIntegrationEvent, GatewayRefundCompletedHandler>();
        eventBus.Subscribe<InvoiceIssuedIntegrationEvent, InvoiceIssuedHandler>();
        eventBus.Subscribe<CommissionAccruedIntegrationEvent, CommissionAccruedHandler>();
        eventBus.Subscribe<LhdnDocumentValidatedIntegrationEvent, LhdnDocumentValidatedIntegrationEventHandler>();
        eventBus.Subscribe<LhdnDocumentCancelledIntegrationEvent, LhdnDocumentCancelledIntegrationEventHandler>();
        eventBus.Subscribe<LhdnDocumentSubmittedIntegrationEvent, LhdnDocumentSubmittedIntegrationEventHandler>();
        eventBus.Subscribe<ZeroAmountCheckoutCompletedIntegrationEvent, ZeroAmountCheckoutHandler>();
        eventBus.Subscribe<ManualSubscriberEnrolledIntegrationEvent, ManualSubscriberEnrolledIntegrationEventHandler>();

        return app;
    }
}
