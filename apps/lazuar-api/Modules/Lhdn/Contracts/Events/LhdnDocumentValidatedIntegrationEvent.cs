using System;
using BuildingBlocks.Application;

namespace Modules.Lhdn.Contracts.Events;

public record LhdnDocumentValidatedIntegrationEvent(
    Guid OrganizationId,
    string InternalReferenceId,
    string LhdnUuid,
    string Status,
    string? QrLink = null) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
