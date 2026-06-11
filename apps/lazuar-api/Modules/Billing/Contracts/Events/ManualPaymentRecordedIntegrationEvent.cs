using System;
using BuildingBlocks.Application;

namespace Modules.Billing.Contracts.Events;

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
