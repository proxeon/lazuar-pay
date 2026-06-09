using System;
using BuildingBlocks.Application;

namespace Modules.Payments.Contracts.Events;

public record GatewayRefundFailedIntegrationEvent(
    Guid OrganizationId,
    Guid SubscriptionId,
    Guid PaymentRecordId,
    string ErrorMessage) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
