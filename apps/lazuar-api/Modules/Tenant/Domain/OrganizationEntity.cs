using BuildingBlocks.Domain;

namespace Modules.Tenant.Domain;

public class OrganizationEntity : Entity, IAggregateRoot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
