using BuildingBlocks.Domain;

namespace Modules.One.Domain;

public class TenantMembership : Entity, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid GlobalUserId { get; private set; }
    public Guid OrganizationId { get; set; }
    public string Role { get; private set; } // e.g. "ADMIN", "CLIENT"
    public DateTime CreatedAt { get; private set; }

#pragma warning disable CS8618
    private TenantMembership() { }
#pragma warning restore CS8618

    public TenantMembership(Guid globalUserId, Guid organizationId, string role)
    {
        Id = Guid.CreateVersion7();
        GlobalUserId = globalUserId;
        OrganizationId = organizationId;
        Role = role.ToUpperInvariant();
        CreatedAt = DateTime.UtcNow;
    }
}
