using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Billing.Application;
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
        var referenceType = "ZERO_AMOUNT_CHECKOUT";
        var referenceId = @event.CheckoutSessionId.ToString();

        if (await _repository.HasEntryBeenProcessedAsync(referenceType, referenceId))
            return;

        var entry = new LedgerEntry(
            @event.OrganizationId,
            referenceType,
            referenceId,
            $"100% off coupon applied: {@event.CouponCode}");

        if (@event.OriginalAmount > 0)
        {
            entry.AddLine("EXPENSE_DISCOUNT", @event.DiscountAmount, @event.Currency, @event.DiscountAmount, @event.Currency);
            entry.AddLine("REVENUE_GROSS", -@event.OriginalAmount, @event.Currency, -@event.OriginalAmount, @event.Currency);
        }

        entry.ValidateBalanced();
        _repository.Add(entry);
        await _repository.SaveChangesAsync();
    }
}
