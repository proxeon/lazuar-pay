using BuildingBlocks.Domain;

namespace Modules.Tenant.Domain;

public class BranchEntity : Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
