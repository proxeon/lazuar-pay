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
    public bool IsActive { get; private set; }
    public decimal EstimatedFeePercentage { get; private set; }
    public decimal FixedFee { get; private set; }
    public decimal TaxRate { get; private set; }
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
        string? merchantId,
        bool isActive,
        decimal estimatedFeePercentage = 0,
        decimal fixedFee = 0,
        decimal taxRate = 0)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        GatewayType = gatewayType.ToUpperInvariant();
        ApiKey = apiKey;
        WebhookSecret = webhookSecret;
        MerchantId = merchantId;
        IsActive = isActive;
        EstimatedFeePercentage = estimatedFeePercentage;
        FixedFee = fixedFee;
        TaxRate = taxRate;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateCredentials(
        string gatewayType, 
        string? apiKey, 
        string? webhookSecret, 
        string? merchantId, 
        bool isActive,
        decimal estimatedFeePercentage,
        decimal fixedFee,
        decimal taxRate)
    {
        GatewayType = gatewayType.ToUpperInvariant();
        ApiKey = apiKey;
        WebhookSecret = webhookSecret;
        MerchantId = merchantId;
        IsActive = isActive;
        EstimatedFeePercentage = estimatedFeePercentage;
        FixedFee = fixedFee;
        TaxRate = taxRate;
        UpdatedAt = DateTime.UtcNow;
    }
}
