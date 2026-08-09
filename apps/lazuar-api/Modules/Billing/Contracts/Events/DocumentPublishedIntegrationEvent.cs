using System;
using BuildingBlocks.Application;

namespace Modules.Billing.Contracts.Events;

/// <summary>
/// Published after Billing stores a document PDF. Payload is denormalized so Communications
/// can send receipt/quotation email without cross-schema SQL (billing/one/commerce).
/// </summary>
public record DocumentPublishedIntegrationEvent(
    Guid OrganizationId,
    Guid LedgerEntryId,
    string DocumentType,
    string StoragePath,
    string TenantSlug,
    string BusinessName,
    string CustomerName,
    string CustomerEmail) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
