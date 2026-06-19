using System;
using BuildingBlocks.Application;

namespace Modules.Lhdn.Contracts.Events;

/// <summary>
/// Published when an API key is manually revoked via the dashboard.
/// The API layer subscribes to this to instantly evict the hash from IMemoryCache.
/// </summary>
public record ApiKeyRevokedIntegrationEvent(
    Guid OrganizationId, 
    string KeyHash) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
