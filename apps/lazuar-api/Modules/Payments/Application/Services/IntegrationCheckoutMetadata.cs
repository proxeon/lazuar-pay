using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Modules.Payments.Application.Exceptions;

namespace Modules.Payments.Application.Services;

public static class IntegrationCheckoutMetadata
{
    public const int MaxKeys = 20;
    public const int MaxKeyLength = 40;
    public const int MaxValueLength = 500;

    /// <summary>Reserved Aura / integrator keys that must not be stripped.</summary>
    public static readonly HashSet<string> ReservedAuraKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "integrator",
        "type",
        "booking_id",
        "gift_card_id",
        "subscription_id",
        "payment_type",
        "aura_org_id",
        "aura_branch_id"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public static Dictionary<string, string> NormalizeAndValidate(Dictionary<string, string>? input)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (input == null || input.Count == 0)
            return result;

        if (input.Count > MaxKeys)
        {
            throw PaymentIntegrationException.MetadataInvalid(
                $"Metadata may contain at most {MaxKeys} keys.");
        }

        foreach (var (rawKey, rawValue) in input)
        {
            if (string.IsNullOrWhiteSpace(rawKey))
            {
                throw PaymentIntegrationException.MetadataInvalid("Metadata keys must be non-empty.");
            }

            var key = rawKey.Trim();
            if (key.Length > MaxKeyLength)
            {
                throw PaymentIntegrationException.MetadataInvalid(
                    $"Metadata key '{key}' exceeds {MaxKeyLength} characters.");
            }

            var value = rawValue ?? string.Empty;
            if (value.Length > MaxValueLength)
            {
                throw PaymentIntegrationException.MetadataInvalid(
                    $"Metadata value for '{key}' exceeds {MaxValueLength} characters.");
            }

            result[key] = value;
        }

        return result;
    }

    /// <summary>
    /// Stamps hub_workspace_id, checkout_id, tenant_id, hub_checkout_kind (overwrite client values).
    /// Preserves all client keys including reserved Aura keys.
    /// </summary>
    public static Dictionary<string, string> Stamp(
        Dictionary<string, string> metadata,
        Guid organizationId,
        Guid checkoutId,
        string? customerName = null)
    {
        var stamped = new Dictionary<string, string>(metadata, StringComparer.Ordinal)
        {
            ["hub_workspace_id"] = organizationId.ToString(),
            ["checkout_id"] = checkoutId.ToString(),
            ["tenant_id"] = organizationId.ToString(),
            ["hub_checkout_kind"] = "integration"
        };

        if (!string.IsNullOrWhiteSpace(customerName) && !stamped.ContainsKey("customer_name"))
            stamped["customer_name"] = customerName.Trim();

        // Hub stamps count toward key budget only loosely; reject if total exceeds MaxKeys + stamps headroom.
        // Stamps are always applied even if client already filled reserved slots (overwrite).
        if (stamped.Count > MaxKeys + 4)
        {
            throw PaymentIntegrationException.MetadataInvalid(
                $"Metadata may contain at most {MaxKeys} client keys (hub stamps excluded).");
        }

        return stamped;
    }

    public static string Serialize(Dictionary<string, string> metadata) =>
        JsonSerializer.Serialize(metadata, JsonOptions);

    public static Dictionary<string, string> Deserialize(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
            return new Dictionary<string, string>(StringComparer.Ordinal);

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

    public static string ComputeFingerprint(
        decimal amount,
        string currency,
        string successUrl,
        string cancelUrl,
        string description,
        string customerEmail,
        string? customerName,
        string? gatewayName,
        bool setupFutureUsage,
        Dictionary<string, string> metadata)
    {
        var metaPart = string.Join("|", metadata.OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={kv.Value}"));
        var raw =
            $"{amount:F4}|{currency.ToUpperInvariant()}|{successUrl}|{cancelUrl}|{description}|{customerEmail}|{customerName ?? ""}|{gatewayName?.ToUpperInvariant() ?? ""}|{setupFutureUsage}|{metaPart}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }
}
