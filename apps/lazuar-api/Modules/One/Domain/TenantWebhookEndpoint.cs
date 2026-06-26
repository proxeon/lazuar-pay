// apps/lazuar-api/Modules/One/Domain/TenantWebhookEndpoint.cs
using System;
using BuildingBlocks.Domain;

namespace Modules.One.Domain;

public class TenantWebhookEndpoint : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string Url { get; private set; }
    public string SecretKey { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private TenantWebhookEndpoint() { }
#pragma warning restore CS8618

    public TenantWebhookEndpoint(Guid organizationId, string url, string secretKey, bool isActive = true)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Url = url;
        SecretKey = secretKey;
        IsActive = isActive;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string url, string secretKey, bool isActive)
    {
        Url = url;
        SecretKey = secretKey;
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }
}
