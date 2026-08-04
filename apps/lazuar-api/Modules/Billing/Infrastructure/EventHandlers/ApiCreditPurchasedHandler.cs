// apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ApiCreditPurchasedHandler.cs
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Payments.Contracts.Events;

namespace Modules.Billing.Infrastructure.EventHandlers;

public class ApiCreditPurchasedHandler : IIntegrationEventHandler<ApiCreditPurchasedIntegrationEvent>
{
    private readonly BillingDbContext _dbContext;

    public ApiCreditPurchasedHandler(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task HandleAsync(ApiCreditPurchasedIntegrationEvent @event)
    {
        var wallet = await _dbContext.TenantCreditBalances
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.OrganizationId == @event.OrganizationId);

        if (wallet == null)
        {
            wallet = new TenantCreditBalance(@event.OrganizationId);
            _dbContext.TenantCreditBalances.Add(wallet);
        }

        var reference = $"Stripe Top-Up: {@event.GatewayTransactionId}";
        wallet.TopUp(@event.CreditAmount, reference);

        // Record the system expense in the double-entry ledger
        var ledgerEntry = new LedgerEntry(
            @event.OrganizationId,
            LedgerReferenceTypes.SystemCreditTopup,
            @event.GatewayTransactionId,
            $"Purchased {@event.CreditAmount} Utility Credits via Lazuar Platform",
            "B2B");

        ledgerEntry.AddLine(AccountTypes.ExpenseSoftwareSubscription, @event.AmountPaid, @event.Currency, @event.AmountPaid, @event.Currency);
        ledgerEntry.AddLine(AccountTypes.AssetCash, -@event.AmountPaid, @event.Currency, -@event.AmountPaid, @event.Currency);

        ledgerEntry.ValidateBalanced();
        ledgerEntry.MarkConsolidationNotRequired();
        _dbContext.LedgerEntries.Add(ledgerEntry);

        await _dbContext.SaveChangesAsync();
    }
}
