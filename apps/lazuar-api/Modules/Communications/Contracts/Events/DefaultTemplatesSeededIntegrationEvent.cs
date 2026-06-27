using System;
using BuildingBlocks.Application;

namespace Modules.Communications.Contracts.Events;

public record DefaultTemplatesSeededIntegrationEvent(Guid TenantId) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
