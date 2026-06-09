using System;
using BuildingBlocks.Application;

namespace Modules.One.Contracts;

public record GlobalUserProfileUpdatedIntegrationEvent(Guid UserId, string Email, string Name) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
