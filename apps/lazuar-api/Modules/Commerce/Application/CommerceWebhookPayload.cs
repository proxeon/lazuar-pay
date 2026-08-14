using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Modules.Commerce.Domain.Aggregates;

namespace Modules.Commerce.Application;

/// <summary>
/// Shared outbound <c>data</c> builder for subscription.* webhooks (P09.03).
/// <c>current_period_end</c> is paid-through (<see cref="Subscription.NextBillingDate"/>).
/// </summary>
public static class CommerceWebhookPayload
{
    public static readonly JsonSerializerOptions Snake = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static JsonElement From(
        Subscription sub,
        Product? product,
        string? customerEmail,
        string status,
        bool? isFirstPayment = null)
    {
        var metadata = CommerceCheckoutMetadata.Deserialize(sub.MetadataJson);
        return Build(
            sub.Id,
            sub.ClientProfileId,
            sub.ProductId,
            status,
            sub.NextBillingDate,
            sub.CurrentPeriodEnd,
            customerEmail,
            product?.Price,
            product?.Currency,
            product?.Interval,
            metadata,
            isFirstPayment);
    }

    public static JsonElement Build(
        Guid subscriptionId,
        Guid clientProfileId,
        Guid productId,
        string status,
        DateTime? nextBillingDate,
        DateTime? currentPeriodEnd,
        string? customerEmail,
        decimal? amount,
        string? currency,
        string? interval,
        IReadOnlyDictionary<string, string>? metadata,
        bool? isFirstPayment = null)
    {
        var paidThrough = nextBillingDate ?? currentPeriodEnd;
        var payload = new Dictionary<string, object?>
        {
            ["subscription_id"] = subscriptionId.ToString(),
            ["client_profile_id"] = clientProfileId.ToString(),
            ["customer_id"] = clientProfileId.ToString(),
            ["product_id"] = productId.ToString(),
            ["status"] = status
        };

        if (paidThrough.HasValue)
        {
            payload["current_period_end"] = AsUtc(paidThrough.Value);
        }

        if (!string.IsNullOrWhiteSpace(customerEmail))
        {
            payload["customer_email"] = customerEmail;
        }

        if (amount.HasValue)
        {
            payload["amount"] = amount.Value;
        }

        if (!string.IsNullOrWhiteSpace(currency))
        {
            payload["currency"] = currency;
        }

        if (!string.IsNullOrWhiteSpace(interval))
        {
            payload["interval"] = interval;
        }

        if (isFirstPayment.HasValue)
        {
            payload["is_first_payment"] = isFirstPayment.Value;
        }

        if (metadata != null && metadata.Count > 0)
        {
            payload["metadata"] = new Dictionary<string, string>(metadata, StringComparer.Ordinal);
        }

        return JsonSerializer.SerializeToElement(payload, Snake);
    }

    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
