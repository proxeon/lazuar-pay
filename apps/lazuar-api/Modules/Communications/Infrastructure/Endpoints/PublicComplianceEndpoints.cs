using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Communications.Contracts;
using Modules.Communications.Infrastructure.Security;

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
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("PublicComplianceEndpoints");
            var org = request.Query["org"].ToString();
            var email = request.Query["email"].ToString();
            var sig = request.Query["sig"].ToString();

            if (!Guid.TryParse(org, out var orgId) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(sig))
                return Results.BadRequest("Invalid unsubscribe link.");

            if (!TryJwtHmacSecret(config, out var secret))
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

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

        // RFC 8058 one-click POST to the same List-Unsubscribe URL.
        group.MapPost("/unsubscribe", async (
            HttpRequest request,
            IConfiguration config,
            ISuppressionService suppression,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("PublicComplianceEndpoints");
            var org = request.Query["org"].ToString();
            var email = request.Query["email"].ToString();
            var sig = request.Query["sig"].ToString();

            if (!Guid.TryParse(org, out var orgId) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(sig))
                return Results.BadRequest("Invalid unsubscribe link.");

            if (!TryJwtHmacSecret(config, out var secret))
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

            var expected = ComputeSig(secret, $"{orgId}:{email}");
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(sig.ToLowerInvariant())))
            {
                return Results.BadRequest("Invalid unsubscribe link.");
            }

            await suppression.SuppressAsync(orgId, email, "UNSUBSCRIBE", "list_unsubscribe_one_click");
            logger.LogInformation("Tenant {OrganizationId}: {Email} unsubscribed via one-click POST.", orgId, email);
            return Results.Ok();
        });

        // Resend (Svix-signed) webhook for bounce/complaint events.
        group.MapPost("/webhooks/resend", async (
            HttpRequest request,
            IConfiguration config,
            IWebHostEnvironment env,
            ISuppressionService suppression,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("PublicComplianceEndpoints");
            var secret = config["Resend:WebhookSecret"];
            request.EnableBuffering();
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();
            request.Body.Position = 0;

            // Fail closed outside Development when secret is not configured.
            // Only skip Svix verification in Development when secret is empty.
            if (string.IsNullOrWhiteSpace(secret))
            {
                if (!env.IsDevelopment())
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

                logger.LogWarning("Resend webhook received with no WebhookSecret configured; skipping signature verification (development only).");
            }
            else
            {
                if (!request.Headers.TryGetValue("svix-id", out var svixId)
                    || !request.Headers.TryGetValue("svix-timestamp", out var svixTimestamp)
                    || !request.Headers.TryGetValue("svix-signature", out var svixSignature))
                {
                    return Results.BadRequest("Missing Svix signature headers.");
                }

                if (!long.TryParse(svixTimestamp, out var ts) || Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts) > 300)
                    return Results.BadRequest("Stale webhook timestamp.");

                if (!SvixWebhookSignature.IsValid(
                        secret,
                        svixId.ToString(),
                        svixTimestamp.ToString(),
                        rawBody,
                        svixSignature.ToString()))
                {
                    return Results.BadRequest("Invalid webhook signature.");
                }
            }

            try
            {
                if (!ResendWebhookParser.TryParseSuppression(rawBody, out var type, out var recipient, out var orgId))
                {
                    logger.LogWarning("Failed to parse Resend webhook payload.");
                    return Results.Ok();
                }

                var reason = ResendWebhookParser.MapReason(type);
                if (reason == null) return Results.Ok();
                if (string.IsNullOrWhiteSpace(recipient)) return Results.Ok();

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

    /// <summary>
    /// Empty Jwt:Secret is not a working HMAC key. Do not fall back to a well-known string.
    /// </summary>
    public static bool TryJwtHmacSecret(IConfiguration config, out string secret)
    {
        secret = config["Jwt:Secret"] ?? "";
        return !string.IsNullOrWhiteSpace(secret);
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
