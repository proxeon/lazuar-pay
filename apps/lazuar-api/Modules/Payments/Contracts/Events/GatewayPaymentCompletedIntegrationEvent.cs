using BuildingBlocks.Application;

namespace Modules.Payments.Contracts.Events;

/// <summary>
/// Published by the Payments module when a valid webhook is received confirming a successful payment.
/// Other modules (Community, Shop, Vault) listen to this to fulfill orders/subscriptions.
/// </summary>
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
