using System;
using BuildingBlocks.Domain;

namespace Modules.One.Domain.Events;

public record WorkspaceProvisionedDomainEvent(
    Guid OrganizationId,
    string WorkspaceName,
    string OwnerName,
    string OwnerEmail,
    string? GeneratedPassword) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
