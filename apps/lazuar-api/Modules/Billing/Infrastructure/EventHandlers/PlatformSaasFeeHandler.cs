using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Contracts.Events;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure.Services;
using Modules.Payments.Contracts;
using Modules.Payments.Contracts.Events;

namespace Modules.Billing.Infrastructure.EventHandlers;

public class PlatformSaasFeeHandler : IIntegrationEventHandler<GatewayPaymentCompletedIntegrationEvent>
{
    private readonly BillingDbContext _dbContext;
    private readonly IMediator _mediator;
    private readonly IEventBus _eventBus;
    private readonly SaasOptions _saas;
    private readonly ILogger<PlatformSaasFeeHandler> _logger;

    public PlatformSaasFeeHandler(
        BillingDbContext dbContext,
        IMediator mediator,
        [FromKeyedServices("BillingEventBus")] IEventBus eventBus,
        IOptions<SaasOptions> saas,
        ILogger<PlatformSaasFeeHandler> logger)
    {
        _dbContext = dbContext;
        _mediator = mediator;
        _eventBus = eventBus;
        _saas = saas.Value;
        _logger = logger;
    }

    public async Task HandleAsync(GatewayPaymentCompletedIntegrationEvent @event)
    {
        if (!@event.Metadata.TryGetValue("type", out var type)
            || type != PlatformCheckoutTypes.PlatformSaasFee)
            return;

        if (!@event.Metadata.TryGetValue("tenant_id", out var tenantIdStr)
            || !Guid.TryParse(tenantIdStr, out var tenantId)
            || tenantId == Guid.Empty
            || tenantId == PlatformCheckoutTypes.SystemOrganizationId)
            return;

        if (string.IsNullOrWhiteSpace(@event.GatewayTransactionId))
            return;

        var alreadyBooked = await _dbContext.LedgerEntries
            .IgnoreQueryFilters()
            .AnyAsync(e => e.ReferenceType == LedgerReferenceTypes.SystemSaasFee
                           && e.ReferenceId == @event.GatewayTransactionId);
        if (alreadyBooked)
            return;

        var plan = _saas.Plan;
        if (plan.AmountMyr <= 0
            || @event.AmountPaid != plan.AmountMyr
            || !string.Equals(@event.Currency, plan.Currency, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Hub SaaS fee {GatewayTxId} amount/currency {Amount} {Currency} does not match plan {PlanAmount} {PlanCurrency}; not activating.",
                @event.GatewayTransactionId, @event.AmountPaid, @event.Currency, plan.AmountMyr, plan.Currency);
            return;
        }

        var now = DateTime.UtcNow;
        var subscription = await _dbContext.WorkspaceSaasSubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.OrganizationId == tenantId);

        if (subscription == null)
        {
            subscription = new WorkspaceSaasSubscription(tenantId, plan.Code);
            _dbContext.WorkspaceSaasSubscriptions.Add(subscription);
        }

        subscription.ActivateFromPayment(now, plan.Interval, @event.GatewayTransactionId);

        var yearPrefix = $"SAAS-{now:yyyy}";
        var invoiceNumber = await _mediator.Send(
            new GenerateNextSequenceNumberCommand(PlatformCheckoutTypes.SystemOrganizationId, yearPrefix));

        var entry = new LedgerEntry(
            tenantId,
            LedgerReferenceTypes.SystemSaasFee,
            @event.GatewayTransactionId,
            SaasPlanInterval.LineDescription(plan.Name, plan.Interval),
            "B2B");

        entry.AddLine(AccountTypes.ExpenseSoftwareSubscription, @event.AmountPaid, @event.Currency, @event.AmountPaid, @event.Currency);
        entry.AddLine(AccountTypes.AssetCash, -@event.AmountPaid, @event.Currency, -@event.AmountPaid, @event.Currency);
        entry.ValidateBalanced();
        entry.MarkConsolidationNotRequired();
        entry.AssignPlatformDocumentNumber(invoiceNumber);
        _dbContext.LedgerEntries.Add(entry);

        await _dbContext.SaveChangesAsync();

        try
        {
            await _mediator.Send(new GenerateAndStorePlatformSaasInvoiceCommand(tenantId, entry.Id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hub SaaS invoice PDF failed for tenant {TenantId} ledger {LedgerId}.", tenantId, entry.Id);
        }

        // Plane S must never fire tenant MyInvois via InvoiceIssuedIntegrationEvent.
        _ = typeof(InvoiceIssuedIntegrationEvent);
        _ = _eventBus;
        _logger.LogInformation(
            "Hub SaaS fee booked for tenant {TenantId} tx {GatewayTxId}; InvoiceIssued not published.",
            tenantId, @event.GatewayTransactionId);
    }
}
