using System;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Events;

public record ManualSubscriberEnrolledIntegrationEvent(
    Guid OrganizationId,
    Guid SubscriptionId,
    Guid ClientProfileId,
    Guid ProductId,
    decimal AmountPaid,
    string Currency,
    string PaymentMethod,
    string? ReferenceNumber,
    Guid TransactionLogId = default
) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
