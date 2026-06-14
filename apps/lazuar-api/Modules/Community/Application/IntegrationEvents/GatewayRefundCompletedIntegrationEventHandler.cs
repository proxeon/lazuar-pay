using BuildingBlocks.Application;
using Modules.Payments.Contracts.Events;

namespace Modules.Community.Application.IntegrationEvents;

public class GatewayRefundCompletedIntegrationEventHandler : IIntegrationEventHandler<GatewayRefundCompletedIntegrationEvent>
{
    private readonly ICommunitySubscriptionRepository _repository;

    public GatewayRefundCompletedIntegrationEventHandler(ICommunitySubscriptionRepository repository)
    {
        _repository = repository;
    }

    public async Task HandleAsync(GatewayRefundCompletedIntegrationEvent @event)
    {
        var subscription = await _repository.GetByIdAsync(@event.SubscriptionId);

        if (subscription == null || subscription.OrganizationId != @event.OrganizationId)
            return;

        var originalPayment = subscription.PaymentRecords.FirstOrDefault(p => p.Id == @event.PaymentRecordId);
        if (originalPayment == null) return;

        var refundAmount = @event.RefundedAmount == 0 ? originalPayment.Amount : @event.RefundedAmount;

        subscription.RecordRefund(refundAmount, @event.Currency, originalPayment.ExternalReference ?? originalPayment.Id.ToString());

        await _repository.SaveChangesAsync();
    }
}
