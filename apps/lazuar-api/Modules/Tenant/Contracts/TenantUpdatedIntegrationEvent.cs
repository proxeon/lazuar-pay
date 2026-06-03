using BuildingBlocks.Application;

namespace Modules.Tenant.Contracts;

public record TenantUpdatedIntegrationEvent(Guid TenantId, string Name, string Slug, bool IsActive) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
