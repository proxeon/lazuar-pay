using System;
using BuildingBlocks.Domain;

namespace Modules.Payments.Domain.Aggregates;

/// <summary>
/// Server-side M2M / integrator checkout session (Payments schema).
/// Full metadata safety net (especially Billplz); not Commerce CheckoutSession.
/// </summary>
public class IntegrationCheckoutSession : Entity, IAggregateRoot, IMustHaveTenant
{
    public const string StatusOpen = "open";
    public const string StatusCompleted = "completed";
    public const string StatusExpired = "expired";
    public const string StatusFailed = "failed";

    public static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }

    public string? IdempotencyKey { get; private set; }
    public string? RequestFingerprint { get; private set; }

    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public string Description { get; private set; }
    public string CustomerEmail { get; private set; }
    public string? CustomerName { get; private set; }
    public string SuccessUrl { get; private set; }
    public string CancelUrl { get; private set; }

    public string GatewayName { get; private set; }
    public string? ProviderSessionId { get; private set; }
    public string? GatewayTransactionId { get; private set; }
    public string? CheckoutUrl { get; private set; }

    public string Status { get; private set; }
    public string MetadataJson { get; private set; }
    public bool SetupFutureUsage { get; private set; }

    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private IntegrationCheckoutSession() { }
#pragma warning restore CS8618

    public IntegrationCheckoutSession(
        Guid organizationId,
        decimal amount,
        string currency,
        string description,
        string customerEmail,
        string successUrl,
        string cancelUrl,
        string gatewayName,
        string metadataJson,
        bool setupFutureUsage,
        string? customerName = null,
        string? idempotencyKey = null,
        string? requestFingerprint = null,
        DateTime? expiresAt = null,
        Guid? id = null)
    {
        Id = id ?? Guid.CreateVersion7();
        OrganizationId = organizationId;
        Amount = amount;
        Currency = currency.ToUpperInvariant();
        Description = description;
        CustomerEmail = customerEmail;
        CustomerName = customerName;
        SuccessUrl = successUrl;
        CancelUrl = cancelUrl;
        GatewayName = gatewayName.ToUpperInvariant();
        MetadataJson = metadataJson;
        SetupFutureUsage = setupFutureUsage;
        IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        RequestFingerprint = requestFingerprint;
        Status = StatusOpen;
        var now = DateTime.UtcNow;
        CreatedAt = now;
        UpdatedAt = now;
        ExpiresAt = expiresAt ?? now.Add(DefaultTtl);
    }

    public void ReplaceMetadataJson(string metadataJson)
    {
        MetadataJson = metadataJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkProviderIssued(string checkoutUrl, string? providerSessionId, string? metadataJson = null)
    {
        CheckoutUrl = checkoutUrl;
        ProviderSessionId = providerSessionId;
        if (metadataJson != null)
            MetadataJson = metadataJson;
        Status = StatusOpen;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFailed()
    {
        Status = StatusFailed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkCompleted(string? gatewayTransactionId = null)
    {
        Status = StatusCompleted;
        if (!string.IsNullOrWhiteSpace(gatewayTransactionId))
            GatewayTransactionId = gatewayTransactionId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkExpired()
    {
        Status = StatusExpired;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Lazy expire when still open and past TTL.</summary>
    public bool TryExpireIfPast(DateTime utcNow)
    {
        if (Status == StatusOpen && utcNow >= ExpiresAt)
        {
            MarkExpired();
            return true;
        }

        return false;
    }
}
