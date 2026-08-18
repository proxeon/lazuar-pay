using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Modules.Payments.Infrastructure.Gateways;

/// <summary>
/// CHIP webhook subscribe is idempotent on callback URL (B04-P19).
/// Verify PEM is Webhook.public_key, not the company GET /public_key/ key.
/// </summary>
internal static class ChipWebhookRegistrar
{
    public const string WebhooksUrl = "https://gate.chip-in.asia/api/v1/webhooks/";
    public const string CompanyPublicKeyUrl = "https://gate.chip-in.asia/api/v1/public_key/";

    public static async Task<string> EnsureRegisteredAsync(
        HttpClient client,
        string callbackUrl,
        CancellationToken ct)
    {
        var existing = await TryFindExistingAsync(client, callbackUrl, ct);
        if (existing != null)
        {
            return existing;
        }

        var payload = new
        {
            title = "Lazuar Platform Webhook",
            events = new[] { "purchase.paid", "purchase.payment_failure", "payment.refunded", "purchase.preauthorized" },
            callback = callbackUrl
        };

        var created = await client.PostAsJsonAsync(WebhooksUrl, payload, ct);
        created.EnsureSuccessStatusCode();
        var createdJson = await created.Content.ReadAsStringAsync(ct);
        var fromCreate = ExtractPublicKey(createdJson);
        if (!string.IsNullOrWhiteSpace(fromCreate))
        {
            return NormalizePem(fromCreate);
        }

        return await FetchCompanyPublicKeyAsync(client, ct);
    }

    internal static async Task<string?> TryFindExistingAsync(HttpClient client, string callbackUrl, CancellationToken ct)
    {
        var list = await client.GetAsync(WebhooksUrl, ct);
        if (!list.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await list.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
        foreach (var item in EnumerateWebhookObjects(doc.RootElement))
        {
            if (!item.TryGetProperty("callback", out var cb))
            {
                continue;
            }

            if (!string.Equals(cb.GetString(), callbackUrl, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (item.TryGetProperty("public_key", out var key) && !string.IsNullOrWhiteSpace(key.GetString()))
            {
                return NormalizePem(key.GetString()!);
            }

            return await FetchCompanyPublicKeyAsync(client, ct);
        }

        return null;
    }

    internal static string? ExtractPublicKey(string json)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("public_key", out var key))
        {
            return key.GetString();
        }

        return null;
    }

    internal static string NormalizePem(string raw) =>
        raw.Trim().Trim('"').Replace("\\n", "\n", StringComparison.Ordinal);

    private static async Task<string> FetchCompanyPublicKeyAsync(HttpClient client, CancellationToken ct)
    {
        var pubKeyResponse = await client.GetAsync(CompanyPublicKeyUrl, ct);
        pubKeyResponse.EnsureSuccessStatusCode();
        return NormalizePem(await pubKeyResponse.Content.ReadAsStringAsync(ct));
    }

    private static System.Collections.Generic.IEnumerable<JsonElement> EnumerateWebhookObjects(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    yield return item;
                }
            }

            yield break;
        }

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("results", out var results)
            && results.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in results.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    yield return item;
                }
            }
        }
    }
}
