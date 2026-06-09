using System;
using BuildingBlocks.Application;

namespace Modules.One.Contracts;

public record AppEntitlementGrantedIntegrationEvent(Guid TenantId, string AppId) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
