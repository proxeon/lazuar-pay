using System;
using BuildingBlocks.Application;

namespace Modules.Lhdn.Contracts.Events;

/// <summary>
/// Published when LHDN successfully validates a document.
/// Consumed by billing/ledger systems to attach the official Tax Invoice UUID.
/// </summary>
public record LhdnDocumentValidatedIntegrationEvent(
    Guid OrganizationId,
    string InternalReferenceId,
    string LhdnUuid,
    string LongId,
    string Status) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
