using System;
using System.Collections.Generic;
using BuildingBlocks.Application;

namespace Modules.Billing.Contracts.Events;

public record ConsolidatedInvoiceIssuedIntegrationEvent(
    Guid OrganizationId,
    string InternalReferenceId,
    DateTime IssueDate,
    List<ConsolidatedLineItemDto> Items,
    decimal TotalExcludingTax,
    decimal TotalTax,
    decimal TotalIncludingTax
) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

public record ConsolidatedLineItemDto(
    string Description,
    string ClassificationCode,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRate,
    decimal TaxAmount,
    decimal Subtotal,
    string TaxTypeCode
);
