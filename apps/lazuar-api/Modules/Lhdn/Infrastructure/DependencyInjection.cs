using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.Billing.Contracts.Events;
using Modules.Payments.Contracts.Events;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Infrastructure.EventHandlers;
using Modules.Lhdn.Infrastructure.Gateways;
using Modules.Lhdn.Infrastructure.Repositories;
using Modules.Lhdn.Infrastructure.Services;
using Modules.Lhdn.Infrastructure.Services.Strategies;
using Modules.Lhdn.Infrastructure.Workers;
using System;

namespace Modules.Lhdn.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLhdnModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default") ?? throw new InvalidOperationException("Default connection string not found.");

        services.AddDbContext<LhdnDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "lhdn");
            }));

        services.AddKeyedScoped<ISqlConnectionFactory, NpgsqlConnectionFactory>("LhdnSqlConnectionFactory", (sp, key) =>
            new NpgsqlConnectionFactory(connectionString));

        services.AddKeyedScoped<IEventBus, OutboxEventBus<LhdnDbContext>>("LhdnEventBus");

        services.AddScoped<ILhdnRepository, LhdnRepository>();
        services.AddScoped<ILhdnQueryService, LhdnQueryService>();
        services.AddScoped<ICertificateVaultService, CertificateVaultService>();
        
        services.AddSingleton<ITemplateRendererService, ScribanTemplateRendererService>();
        services.AddSingleton<IUblValidatorService, UblValidatorService>();
        
        services.AddScoped<IDocumentStrategyFactory, DocumentStrategyFactory>();
        services.AddKeyedScoped<IUblDocumentStrategy, StandardInvoiceStrategy>("B2BStandardInvoice");
        services.AddKeyedScoped<IUblDocumentStrategy, ConsolidatedInvoiceStrategy>("B2CConsolidatedInvoice");
        services.AddKeyedScoped<IUblDocumentStrategy, CreditNoteStrategy>("CreditNote");
        services.AddKeyedScoped<IUblDocumentStrategy, SelfBilledInvoiceStrategy>("SelfBilledInvoice");
        services.AddKeyedScoped<IUblDocumentStrategy, SelfBilledCreditNoteStrategy>("SelfBilledCredit");

        services.AddScoped<ILhdnGatewayAdapter, LhdnGatewayAdapter>();
        services.AddScoped<IWebhookSenderService, WebhookSenderService>();
        services.AddScoped<ITaxpayerValidationService, TaxpayerValidationService>();
        services.AddSingleton<ILhdnLinkService, LhdnLinkService>();

        services.AddTransient<InvoiceIssuedIntegrationEventHandler>();
        services.AddTransient<GatewayRefundCompletedIntegrationEventHandler>();
        services.AddTransient<ConsolidatedInvoiceIssuedIntegrationEventHandler>();

        services.AddHostedService<LhdnSubmissionJob>();
        services.AddHostedService<LhdnStatusPollingJob>();
        services.AddHostedService<LhdnReferenceDataSeederJob>();

        return services;
    }

    public static IApplicationBuilder UseLhdnSubscriptions(this IApplicationBuilder app)
    {
        var eventBus = app.ApplicationServices.GetRequiredService<IEventBusSubscriptions>();
        
        eventBus.Subscribe<InvoiceIssuedIntegrationEvent, InvoiceIssuedIntegrationEventHandler>();
        eventBus.Subscribe<GatewayRefundCompletedIntegrationEvent, GatewayRefundCompletedIntegrationEventHandler>();
        eventBus.Subscribe<ConsolidatedInvoiceIssuedIntegrationEvent, ConsolidatedInvoiceIssuedIntegrationEventHandler>();

        return app;
    }
}
