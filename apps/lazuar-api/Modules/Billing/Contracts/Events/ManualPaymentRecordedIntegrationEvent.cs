using System;
using BuildingBlocks.Application;

namespace Modules.Billing.Contracts.Events;

/// <summary>
/// Parked. Nothing in production publishes or handles this type.
/// Offline / clerk cash is <c>ManualSubscriberEnrolledIntegrationEvent</c>.
/// Do not add a second cash journal here.
/// </summary>
public record ManualPaymentRecordedIntegrationEvent(
    Guid OrganizationId,
    string InvoiceNumber,
    decimal AmountPaid,
    string Currency,
    string PaymentMethod,
    string? ReferenceNumber) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
