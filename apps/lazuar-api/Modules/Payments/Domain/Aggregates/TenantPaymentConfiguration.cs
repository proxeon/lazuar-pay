using System;
using BuildingBlocks.Domain;

namespace Modules.Payments.Domain.Aggregates;

public class TenantPaymentConfiguration : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string GatewayType { get; private set; }

    /// <summary>AES-encrypted gateway API/secret key (base64 IV+ciphertext). Never return raw to clients.</summary>
    public string? ApiKey { get; private set; }

    /// <summary>AES-encrypted webhook signing secret.</summary>
    public string? WebhookSecret { get; private set; }

    public string? MerchantId { get; private set; }

    /// <summary>When false, credentials are retained but the gateway is not used for new checkouts/charges.</summary>
    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private TenantPaymentConfiguration() { }
#pragma warning restore CS8618

    public TenantPaymentConfiguration(
        Guid organizationId,
        string gatewayType,
        string? encryptedApiKey,
        string? encryptedWebhookSecret,
        string? merchantId,
        bool isActive = true)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        GatewayType = gatewayType.ToUpperInvariant();
        ApiKey = encryptedApiKey;
        WebhookSecret = encryptedWebhookSecret;
        MerchantId = merchantId;
        IsActive = isActive;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateCredentials(
        string gatewayType,
        string? encryptedApiKey,
        string? encryptedWebhookSecret,
        string? merchantId,
        bool isActive)
    {
        GatewayType = gatewayType.ToUpperInvariant();
        ApiKey = encryptedApiKey;
        WebhookSecret = encryptedWebhookSecret;
        MerchantId = merchantId;
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Soft-disable / re-enable without rotating secrets.</summary>
    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }
}
