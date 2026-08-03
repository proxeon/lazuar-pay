using System;
using BuildingBlocks.Application;

namespace Modules.One.Contracts.Events;

/// <summary>
/// Published when a platform API credential is revoked.
/// Host cache layer subscribes to instantly evict the hash from IMemoryCache.
/// </summary>
public record ApiKeyRevokedIntegrationEvent(
    Guid OrganizationId,
    string KeyHash) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
