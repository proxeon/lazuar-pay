using System;
using BuildingBlocks.Application;

namespace Modules.Billing.Contracts.Events;

public record DocumentPublishedIntegrationEvent(
    Guid OrganizationId,
    Guid LedgerEntryId,
    string DocumentType,
    string StoragePath) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
