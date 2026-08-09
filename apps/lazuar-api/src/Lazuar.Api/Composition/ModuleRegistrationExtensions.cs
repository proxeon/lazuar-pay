using BuildingBlocks.Application;
using Modules.Billing.Infrastructure;
using Modules.Commerce.Infrastructure;
using Modules.Communications.Infrastructure;
using Modules.CRM.Infrastructure;
using Modules.Lhdn.Infrastructure;
using Modules.Messaging.Infrastructure;
using Modules.One.Infrastructure;
using Modules.Ops.Infrastructure;
using Modules.Payments.Infrastructure;

namespace Lazuar.Api.Composition;

/// <summary>
/// Module DI, integration-event subscriptions, and HTTP endpoint maps.
/// Order of Add*/Use*/Map* calls matches historical Program.cs (do not reorder without reason).
/// </summary>
public static class ModuleRegistrationExtensions
{
    public static IServiceCollection AddAllModules(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOneModule(configuration);
        services.AddMessagingModule(configuration);
        services.AddCrmModule(configuration);
        services.AddPaymentsModule(configuration);
        services.AddOpsModule(configuration);
        services.AddBillingModule(configuration);
        services.AddLhdnModule(configuration);
        services.AddCommerceModule(configuration);
        services.AddCommunicationsModule(configuration);
        return services;
    }

    public static WebApplication UseAllModuleSubscriptions(this WebApplication app)
    {
        app.UseOneSubscriptions();
        app.UseMessagingSubscriptions();
        app.UseCrmSubscriptions();
        app.UsePaymentsSubscriptions();
        app.UseOpsSubscriptions();
        app.UseBillingSubscriptions();
        app.UseLhdnSubscriptions();
        app.UseCommerceSubscriptions();
        app.UseCommunicationsSubscriptions();
        return app;
    }

    /// <summary>
    /// Host-owned integration-event subscriptions for API key cache eviction + workspace updates.
    /// R05 One-only: subscribe <c>Modules.One.Contracts.Events.ApiKeyRevokedIntegrationEvent</c> only
    /// (Lhdn dual-subscribe window closed). Keep <c>WorkspaceUpdatedIntegrationEvent</c>.
    /// <b>DEPLOY ONLY</b> after env Q8 <c>active_legacy_only = 0</c> (or signed residual quarantine).
    /// Table drop of <c>lhdn.DeveloperApiKeys</c> is R06 — not this registration.
    /// </summary>
    public static WebApplication UseHostEventSubscriptions(this WebApplication app)
    {
        var eventBus = app.Services.GetRequiredService<IEventBusSubscriptions>();
        eventBus.Subscribe<Modules.One.Contracts.Events.ApiKeyRevokedIntegrationEvent, EventHandlers.ApiKeyRevokedIntegrationEventHandler>();
        eventBus.Subscribe<Modules.One.Contracts.WorkspaceUpdatedIntegrationEvent, EventHandlers.WorkspaceUpdatedIntegrationEventHandler>();
        return app;
    }

    public static WebApplication MapAllModuleEndpoints(this WebApplication app)
    {
        var apiGroup = app.MapGroup("/api/v1").RequireCors();

        apiGroup.MapOneEndpoints();
        apiGroup.MapMessagingEndpoints();
        apiGroup.MapPaymentsEndpoints();
        apiGroup.MapPaymentsIntegrationEndpoints();
        apiGroup.MapOpsEndpoints();
        apiGroup.MapBillingEndpoints();
        apiGroup.MapLhdnEndpoints();
        apiGroup.MapCommerceEndpoints();
        apiGroup.MapCommunicationsEndpoints();

        var platformGroup = app.MapGroup("/api/v1/platform")
            .RequireCors()
            .RequireAuthorization(policy => policy.RequireRole("SUPER_ADMIN"));

        platformGroup.MapPlatformEndpoints();

        return app;
    }
}
