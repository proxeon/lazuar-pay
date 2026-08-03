using System;
using BuildingBlocks.Application;

namespace Modules.Payments.Contracts.Events;

public record GatewayRefundCompletedIntegrationEvent(
    Guid OrganizationId,
    Guid SubscriptionId,
    Guid PaymentRecordId,
    string GatewayTransactionId,
    decimal RefundedAmount,
    string Currency,
    decimal RefundedFee,
    decimal NetRefundedAmount,
    decimal TaxAmount = 0m) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
