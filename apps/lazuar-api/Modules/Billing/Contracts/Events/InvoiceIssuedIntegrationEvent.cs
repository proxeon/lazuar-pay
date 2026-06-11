using System;
using BuildingBlocks.Application;

namespace Modules.Billing.Contracts.Events;

public record InvoiceIssuedIntegrationEvent(
    Guid OrganizationId,
    string InvoiceNumber,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    DateTime IssueDate,
    DateTime DueDate) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
