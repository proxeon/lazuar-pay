using System;
using BuildingBlocks.Application;

namespace Modules.Lhdn.Contracts.Events;

public record LhdnDocumentSubmittedIntegrationEvent(
    Guid OrganizationId,
    string InternalReferenceId,
    bool IsTestMode) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
