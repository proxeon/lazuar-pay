using System;
using BuildingBlocks.Application;

namespace Modules.CRM.Contracts;

/// <summary>
/// Published after a client profile is anonymized. Includes pre-wipe contact details so
/// consumers can cancel subscriptions and suppress email without reading wiped CRM rows.
/// </summary>
public record ClientProfileAnonymizedIntegrationEvent(
    Guid OrganizationId,
    Guid ClientProfileId,
    string? Email,
    string? Phone) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
