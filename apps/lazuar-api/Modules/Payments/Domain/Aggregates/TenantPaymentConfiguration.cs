using System;
using BuildingBlocks.Domain;

namespace Modules.Payments.Domain.Aggregates;

public class TenantPaymentConfiguration : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string GatewayType { get; private set; }
    public string? ApiKey { get; private set; }
    public string? WebhookSecret { get; private set; }
    public string? MerchantId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private TenantPaymentConfiguration() { }
#pragma warning restore CS8618

    public TenantPaymentConfiguration(
        Guid organizationId,
        string gatewayType,
        string? apiKey,
        string? webhookSecret,
        string? merchantId)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        GatewayType = gatewayType.ToUpperInvariant();
        ApiKey = apiKey;
        WebhookSecret = webhookSecret;
        MerchantId = merchantId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateCredentials(
        string gatewayType, 
        string? apiKey, 
        string? webhookSecret, 
        string? merchantId)
    {
        GatewayType = gatewayType.ToUpperInvariant();
        ApiKey = apiKey;
        WebhookSecret = webhookSecret;
        MerchantId = merchantId;
        UpdatedAt = DateTime.UtcNow;
    }
}
