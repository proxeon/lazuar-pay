using System;
using BuildingBlocks.Application;

namespace Modules.Billing.Contracts.Events;

/// <summary>
/// B2B paid sale is ready for MyInvois type 01. Buyer fields must come from CRM —
/// do not pair this with stub TINs. <see cref="TaxAmount"/> is resolved SST
/// (event field or metadata sst_tax_amount), not the raw gateway tax.
/// </summary>
public record B2bTaxInvoiceRequestedIntegrationEvent(
    Guid OrganizationId,
    Guid LedgerEntryId,
    string InvoiceNumber,
    string GatewayTransactionId,
    decimal AmountExcludingTax,
    decimal TaxAmount,
    string Currency,
    string? CorrelationId = null) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
