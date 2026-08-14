using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Modules.Commerce.Application;

/// <summary>
/// Checkout → Payments stamps and Subscription.MetadataJson persistence (P09 / P10.22).
/// </summary>
public static class CommerceCheckoutMetadata
{
    public const string TypeCommerce = "commerce_subscription";
    public const string TypeSaas = "saas_subscription";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    private static readonly HashSet<string> PersistenceExcludedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "subscription_id",
        "tenant_id",
        "dunning_campaign_id",
        "charge_attempt_id",
        "failure_reason",
        "gateway_name",
        "gateway_response_code",
        "receipt"
    };

    public static bool IsCommerceSubscriptionType(string? type) =>
        string.Equals(type, TypeCommerce, StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, TypeSaas, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Merge client metadata into the Payments dictionary.
    /// Client <c>saas_subscription</c> wins for <c>type</c>; otherwise <c>commerce_subscription</c>.
    /// Always stamps <c>tenant_id</c> and correlation <c>subscription_id</c> (checkout session id).
    /// </summary>
    public static Dictionary<string, string> MergeClientIntoGateway(
        IReadOnlyDictionary<string, string>? client,
        Guid tenantId,
        Guid sessionId)
    {
        var result = CopyClient(client);

        if (!result.TryGetValue("type", out var type)
            || !string.Equals(type, TypeSaas, StringComparison.OrdinalIgnoreCase))
        {
            result["type"] = TypeCommerce;
        }
        else
        {
            result["type"] = TypeSaas;
        }

        result["subscription_id"] = sessionId.ToString();
        result["tenant_id"] = tenantId.ToString();
        return result;
    }

    /// <summary>
    /// Map stored on CheckoutSession / Subscription. Drops payment-correlation stamps.
    /// Defaults <c>type</c> and <c>billing_interval</c> when the client omitted them.
    /// </summary>
    public static Dictionary<string, string> ForPersistence(
        IReadOnlyDictionary<string, string>? source,
        string? productInterval)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (source != null)
        {
            foreach (var kv in source)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                {
                    continue;
                }

                var key = kv.Key.Trim();
                if (PersistenceExcludedKeys.Contains(key))
                {
                    continue;
                }

                result[key] = kv.Value ?? string.Empty;
            }
        }

        if (!result.TryGetValue("type", out var type)
            || !string.Equals(type, TypeSaas, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(type, TypeCommerce, StringComparison.OrdinalIgnoreCase))
            {
                result["type"] = TypeCommerce;
            }
        }
        else
        {
            result["type"] = TypeSaas;
        }

        if (!result.ContainsKey("billing_interval") && !string.IsNullOrWhiteSpace(productInterval))
        {
            result["billing_interval"] = productInterval switch
            {
                "yr" => "yearly",
                "mo" => "monthly",
                _ => productInterval
            };
        }

        return result;
    }

    public static string Serialize(IReadOnlyDictionary<string, string> metadata) =>
        JsonSerializer.Serialize(metadata, JsonOptions);

    public static Dictionary<string, string> Deserialize(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson, JsonOptions);
            return dict ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static Dictionary<string, string> CopyClient(IReadOnlyDictionary<string, string>? client)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (client == null)
        {
            return result;
        }

        foreach (var kv in client)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
            {
                continue;
            }

            result[kv.Key.Trim()] = kv.Value ?? string.Empty;
        }

        return result;
    }
}
