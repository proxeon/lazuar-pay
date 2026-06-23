using System;
using BuildingBlocks.Application;

namespace Modules.Community.Contracts;

public record CommunityManualPaymentRecordedIntegrationEvent(
    Guid OrganizationId,
    Guid SubscriptionId,
    decimal AmountPaid,
    string Currency,
    string PaymentMethod,
    string? ReferenceNumber) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
