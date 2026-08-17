using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Billing.Application;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Commerce.Contracts.Events;

namespace Modules.Billing.Infrastructure.EventHandlers;

public class ZeroAmountCheckoutHandler : IIntegrationEventHandler<ZeroAmountCheckoutCompletedIntegrationEvent>
{
    private readonly ILedgerRepository _repository;

    public ZeroAmountCheckoutHandler(ILedgerRepository repository)
    {
        _repository = repository;
    }

    public async Task HandleAsync(ZeroAmountCheckoutCompletedIntegrationEvent @event)
    {
        var referenceType = LedgerReferenceTypes.ZeroAmountCheckout;
        var referenceId = @event.CheckoutSessionId.ToString();

        if (await _repository.HasEntryBeenProcessedAsync(@event.OrganizationId, referenceType, referenceId))
            return;

        var entry = new LedgerEntry(
            @event.OrganizationId,
            referenceType,
            referenceId,
            $"100% off coupon applied: {@event.CouponCode}");

        if (@event.OriginalAmount > 0)
        {
            // Zero-amount checkout is always 100% off (coupon or trial). Use list as discount
            // so a trial that published DiscountAmount=0 still balances.
            var discount = @event.OriginalAmount;
            entry.AddLine(AccountTypes.ExpenseDiscount, discount, @event.Currency, discount, @event.Currency);
            entry.AddLine(AccountTypes.RevenueGross, -@event.OriginalAmount, @event.Currency, -@event.OriginalAmount, @event.Currency);
        }

        entry.ValidateBalanced();
        entry.MarkConsolidationNotRequired();
        _repository.Add(entry);
        await _repository.SaveChangesAsync();
    }
}
