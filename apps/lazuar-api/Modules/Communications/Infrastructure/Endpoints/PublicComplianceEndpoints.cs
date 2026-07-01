using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Modules.Communications.Contracts;

namespace Modules.Communications.Infrastructure;

public static class PublicComplianceEndpoints
{
    private const string UnsubscribeHtml = """
        <!doctype html><html><head><meta charset="utf-8"><title>Unsubscribed</title>
        <style>body{{font-family:system-ui,sans-serif;display:flex;align-items:center;justify-content:center;height:100vh;margin:0;background:#fafafa;color:#18181b}}
        .card{{background:#fff;border:1px solid #e5e5e5;padding:48px;border-radius:8px;max-width:420px;text-align:center}}
        h1{{font-size:18px;margin:0 0 8px}} p{{color:#71717a;font-size:14px;margin:0}}</style></head>
        <body><div class="card"><h1>You're unsubscribed</h1>
        <p>You will no longer receive marketing emails from this sender.</p></div></body></html>
        """;

    public static IEndpointRouteBuilder MapPublicComplianceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/public/communications");

        // Tokenized, auth-free unsubscribe link. Token = HMAC-SHA256(Jwt secret, "orgId:email").
        group.MapGet("/unsubscribe", async (
            HttpRequest request,
            IConfiguration config,
            ISuppressionService suppression,
            ILogger logger) =>
        {
            var org = request.Query["org"].ToString();
            var email = request.Query["email"].ToString();
            var sig = request.Query["sig"].ToString();

            if (!Guid.TryParse(org, out var orgId) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(sig))
                return Results.BadRequest("Invalid unsubscribe link.");

            var secret = config["Jwt:Secret"] ?? "secure_development_key_minimum_32_characters_long";
            var expected = ComputeSig(secret, $"{orgId}:{email}");
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(sig.ToLowerInvariant())))
            {
                return Results.BadRequest("Invalid unsubscribe link.");
            }

            await suppression.SuppressAsync(orgId, email, "UNSUBSCRIBE", "unsubscribe_link");
            logger.LogInformation("Tenant {OrganizationId}: {Email} unsubscribed via link.", orgId, email);
            return Results.Content(UnsubscribeHtml, "text/html", Encoding.UTF8);
        });

        // Resend (Svix-signed) webhook for bounce/complaint events.
        group.MapPost("/webhooks/resend", async (
            HttpRequest request,
            IConfiguration config,
            ISuppressionService suppression,
            ILogger logger) =>
        {
            var secret = config["Resend:WebhookSecret"];
            request.EnableBuffering();
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();
            request.Body.Position = 0;

            // If a webhook secret is configured, verify the Svix signature; otherwise accept
            // (development). Set Resend:WebhookSecret in production to enforce verification.
            if (!string.IsNullOrWhiteSpace(secret))
            {
                if (!request.Headers.TryGetValue("svix-id", out var svixId)
                    || !request.Headers.TryGetValue("svix-timestamp", out var svixTimestamp)
                    || !request.Headers.TryGetValue("svix-signature", out var svixSignature))
                {
                    return Results.BadRequest("Missing Svix signature headers.");
                }

                if (!long.TryParse(svixTimestamp, out var ts) || Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts) > 300)
                    return Results.BadRequest("Stale webhook timestamp.");

                var signed = $"{svixId}.{svixTimestamp}.{rawBody}";
                var expected = Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signed)));
                var received = svixSignature.ToString()
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault(p => p.StartsWith("v1="))?["v1=".Length..];
                if (received == null || !CryptographicOperations.FixedTimeEquals(
                        Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(received)))
                {
                    return Results.BadRequest("Invalid webhook signature.");
                }
            }
            else
            {
                logger.LogWarning("Resend webhook received with no WebhookSecret configured; skipping signature verification (development only).");
            }

            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;
                var type = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "" : "";

                var reason = type switch
                {
                    "email.bounced" => "BOUNCE",
                    "email.complained" => "COMPLAINT",
                    _ => null
                };
                if (reason == null) return Results.Ok(); // not a suppression-worthy event

                var data = root.TryGetProperty("data", out var d) ? d : root;

                // Extract recipient email.
                string? recipient = null;
                if (data.TryGetProperty("email", out var emailEl) && emailEl.TryGetProperty("to", out var toEl) && toEl.GetArrayLength() > 0)
                    recipient = toEl[0].GetString();
                recipient ??= data.TryGetProperty("recipient", out var recipEl) ? recipEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(recipient)) return Results.Ok();

                // Extract org from the "org" tag (set on send by ResendEmailService).
                Guid? orgId = null;
                if (data.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tag in tagsEl.EnumerateArray())
                    {
                        var name = tag.TryGetProperty("name", out var n) ? n.GetString() : null;
                        var value = tag.TryGetProperty("value", out var v) ? v.GetString() : null;
                        if (name == "org" && Guid.TryParse(value, out var g)) { orgId = g; break; }
                    }
                }

                if (orgId.HasValue)
                {
                    await suppression.SuppressAsync(orgId.Value, recipient!, reason, "resend_webhook");
                    logger.LogInformation("Suppressed {Email} for tenant {OrganizationId} ({Reason}).", recipient, orgId.Value, reason);
                }
                else
                {
                    logger.LogWarning("Resend {Type} event for {Email} could not be attributed to a tenant (no org tag). Not suppressed.", type, recipient);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to parse Resend webhook payload.");
            }

            return Results.Ok();
        });

        return endpoints;
    }

    private static string ComputeSig(string secret, string payload)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var bytes = Encoding.UTF8.GetBytes(payload);
        return Convert.ToHexString(HMACSHA256.HashData(key, bytes)).ToLowerInvariant();
    }

    /// <summary>Builds a tokenized unsubscribe URL for the given tenant + recipient.</summary>
    public static string BuildUnsubscribeUrl(string baseUrl, Guid organizationId, string email, string secret)
    {
        var sig = ComputeSig(secret, $"{organizationId}:{email}");
        return $"{baseUrl.TrimEnd('/')}/public/communications/unsubscribe?org={organizationId}&email={Uri.EscapeDataString(email)}&sig={sig}";
    }
}
