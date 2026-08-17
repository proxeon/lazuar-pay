using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Application;
using Modules.CRM.Contracts;
using Modules.Payments.Contracts.Events;

namespace Modules.Commerce.Infrastructure.EventHandlers;

public partial class GatewayPaymentCompletedIntegrationEventHandler : IIntegrationEventHandler<GatewayPaymentCompletedIntegrationEvent>
{
    private readonly ICommerceRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly ICrmQueryService _crmQueryService;
    private readonly CommerceDbContext _dbContext;

    public GatewayPaymentCompletedIntegrationEventHandler(
        ICommerceRepository repository,
        [FromKeyedServices("CommerceEventBus")] IEventBus eventBus,
        ICrmQueryService crmQueryService,
        CommerceDbContext dbContext)
    {
        _repository = repository;
        _eventBus = eventBus;
        _crmQueryService = crmQueryService;
        _dbContext = dbContext;
    }

    public async Task HandleAsync(GatewayPaymentCompletedIntegrationEvent @event)
    {
        var type = @event.Metadata.GetValueOrDefault("type");
        if (!CommerceCheckoutMetadata.IsCommerceSubscriptionType(type) && type != "custom_payment_link")
        {
            return;
        }

        // Optional metadata tenant_id must match event OrganizationId when present.
        if (@event.Metadata.TryGetValue("tenant_id", out var metaTenantStr)
            && Guid.TryParse(metaTenantStr, out var metaTenantId)
            && metaTenantId != @event.OrganizationId)
        {
            return;
        }

        if (!TryResolveCorrelationId(@event, out var correlationId))
        {
            return;
        }

        // Session path: open checkout session (initial subscribe / custom payment link).
        // Org-scoped load under fail-closed global filter (workers have empty ambient TenantId).
        var session = await _dbContext.CheckoutSessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == correlationId && s.OrganizationId == @event.OrganizationId);

        if (session != null && session.CanFulfillFromPayment)
        {
            await HandleOpenCheckoutSessionAsync(@event, session, type!);
            return;
        }

        // Subscription recovery / renewal path (off-session charge, update-payment, etc.).
        await HandleSubscriptionPaymentAsync(@event, correlationId);
    }
}
