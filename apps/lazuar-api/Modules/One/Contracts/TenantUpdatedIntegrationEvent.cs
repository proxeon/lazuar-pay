using BuildingBlocks.Application;

namespace Modules.One.Contracts;

public record TenantUpdatedIntegrationEvent(Guid TenantId, string Name, string Slug, bool IsActive) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
