using System;
using BuildingBlocks.Domain;

namespace Modules.One.Domain;

public class TenantAppEntitlement : Entity, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string AppId { get; private set; } = "";
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private TenantAppEntitlement() { }
#pragma warning restore CS8618

    public TenantAppEntitlement(Guid organizationId, string appId)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        AppId = appId.Trim().ToUpperInvariant();
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Toggle(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }
}
