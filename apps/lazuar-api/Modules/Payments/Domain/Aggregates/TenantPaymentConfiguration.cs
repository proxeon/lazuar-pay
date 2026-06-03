using BuildingBlocks.Domain;

namespace Modules.Payments.Domain.Aggregates;

public class TenantPaymentConfiguration : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; } // Managed by PlatformDbContext

    public string GatewayType { get; private set; }
    public string? ApiKey { get; private set; }
    public string? WebhookSecret { get; private set; }
    public bool IsActive { get; private set; }
    
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
    private TenantPaymentConfiguration() { } // For EF Core
#pragma warning restore CS8618

    public TenantPaymentConfiguration(
        Guid organizationId, 
        string gatewayType, 
        string? apiKey, 
        string? webhookSecret, 
        bool isActive)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        GatewayType = gatewayType.ToUpperInvariant();
        ApiKey = apiKey;
        WebhookSecret = webhookSecret;
        IsActive = isActive;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateCredentials(string gatewayType, string? apiKey, string? webhookSecret, bool isActive)
    {
        GatewayType = gatewayType.ToUpperInvariant();
        ApiKey = apiKey;
        WebhookSecret = webhookSecret;
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }
}
