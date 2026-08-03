using System;
using BuildingBlocks.Application;

namespace Modules.Payments.Contracts.Events;

public record GatewayRefundRequestedIntegrationEvent(
    Guid OrganizationId,
    Guid SubscriptionId,
    Guid PaymentRecordId,
    string GatewayTransactionId,
    decimal Amount,
    string Currency,
    string GatewayName = "STRIPE",
    decimal TaxAmount = 0m) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
