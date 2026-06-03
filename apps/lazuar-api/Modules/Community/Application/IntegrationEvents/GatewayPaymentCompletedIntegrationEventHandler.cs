using BuildingBlocks.Application;
using MediatR;
using Modules.Community.Application.Commands;

namespace Modules.Community.Application.IntegrationEvents;

// -------------------------------------------------------------------------
// PROXY CONTRACT: This record conceptually belongs to `Modules.Payments.Contracts`.
// We define it here temporarily until the Payments module is migrated.
// -------------------------------------------------------------------------
public record GatewayPaymentCompletedIntegrationEvent(
    Guid OrganizationId,
    string GatewayTransactionId,
    decimal AmountPaid,
    string Currency,
    Dictionary<string, string> Metadata) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

// -------------------------------------------------------------------------
// INBOX HANDLER
// -------------------------------------------------------------------------
public class GatewayPaymentCompletedIntegrationEventHandler 
    : IIntegrationEventHandler<GatewayPaymentCompletedIntegrationEvent>
{
    private readonly IMediator _mediator;

    public GatewayPaymentCompletedIntegrationEventHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task HandleAsync(GatewayPaymentCompletedIntegrationEvent @event)
    {
        // 1. Check if this payment actually belongs to the Community module
        if (!@event.Metadata.TryGetValue("type", out var type) || type != "community_subscription")
        {
            return; // Ignore payments for Shop, Vault, Consult, etc.
        }

        // 2. Extract the Subscription ID
        if (!@event.Metadata.TryGetValue("subscription_id", out var subIdStr) || 
            !Guid.TryParse(subIdStr, out var subscriptionId))
        {
            throw new InvalidOperationException("Gateway payment completed for community, but missing valid subscription_id in metadata.");
        }

        // 3. Dispatch the internal Command to record the payment
        var command = new RecordSubscriptionPaymentCommand(
            OrganizationId: @event.OrganizationId,
            SubscriptionId: subscriptionId,
            Amount: @event.AmountPaid,
            Currency: @event.Currency,
            PaymentMethod: "ONLINE_GATEWAY",
            ExternalReference: @event.GatewayTransactionId,
            RecordedBy: "SYSTEM"
        );

        await _mediator.Send(command);
    }
}
