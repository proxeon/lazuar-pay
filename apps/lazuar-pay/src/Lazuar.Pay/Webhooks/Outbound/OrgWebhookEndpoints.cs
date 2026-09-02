using System.Security.Cryptography;
using Lazuar.Pay.Data;
using Lazuar.Pay.Hosting;
using Lazuar.Pay.Identity.Client;
using Lazuar.Pay.Secrets;

namespace Lazuar.Pay.Webhooks.Outbound;

internal static class OrgWebhookEndpoints
{
    public static void MapOrgWebhooks(this WebApplication app)
    {
        app.MapPut("/v1/orgs/{orgId}/webhooks", Put);
        app.MapGet("/v1/orgs/{orgId}/webhooks", Get);
        app.MapPost("/v1/orgs/{orgId}/webhooks/rotate", Rotate);
        app.MapPost("/v1/orgs/{orgId}/webhooks/test", TestPing);
    }

    static async Task<IResult> Put(
        string orgId,
        PutOrgWebhookRequest? body,
        HttpRequest request,
        OneClient one,
        PayDbContext db,
        SecretBox box,
        IHostEnvironment env,
        CancellationToken ct)
    {
        var denied = await MemberGate.RequireWriterAsync(request, one, orgId, ct);
        if (denied is not null)
        {
            return denied;
        }

        var check = await OutboundUrl.ValidateResolvableAsync(body?.Url, env, ct);
        if (!check.Ok)
        {
            return PayErrors.Status(400, "Bad Request", check.Error);
        }

        var url = check.Url;

        string wrapped;
        string secret;
        try
        {
            secret = MintSecret();
            wrapped = box.Protect(secret);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("WrapKey", StringComparison.Ordinal))
        {
            return PayErrors.Status(503, "Service Unavailable", ex.Message);
        }

        var row = await db.OrgWebhookEndpoints.FindAsync([orgId], ct);
        if (row is null)
        {
            row = new OrgWebhookEndpointRow
            {
                OrgId = orgId,
                Url = url,
                SecretCiphertext = wrapped,
                SecretPrefix = secret[^4..],
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.OrgWebhookEndpoints.Add(row);
        }
        else
        {
            row.Url = url;
            row.SecretCiphertext = wrapped;
            row.SecretPrefix = secret[^4..];
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }

        db.AuditEvents.Add(new AuditEventRow
        {
            Id = Guid.NewGuid().ToString("N"),
            OrgId = orgId,
            Action = "org.webhook.upsert",
            At = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
        return Results.Json(new
        {
            org_id = orgId,
            url,
            webhook_configured = true,
            secret_prefix = row.SecretPrefix,
            webhook_secret = secret
        }, OneClient.Json);
    }

    static async Task<IResult> Get(
        string orgId,
        HttpRequest request,
        OneClient one,
        PayDbContext db,
        CancellationToken ct)
    {
        var denied = await MemberGate.RequireMemberAsync(request, one, orgId, ct);
        if (denied is not null)
        {
            return denied;
        }

        var row = await db.OrgWebhookEndpoints.FindAsync([orgId], ct);
        return Results.Json(new
        {
            org_id = orgId,
            url = row?.Url,
            webhook_configured = row is not null,
            secret_prefix = row?.SecretPrefix
        }, OneClient.Json);
    }

    static async Task<IResult> Rotate(
        string orgId,
        HttpRequest request,
        OneClient one,
        PayDbContext db,
        SecretBox box,
        CancellationToken ct)
    {
        var denied = await MemberGate.RequireWriterAsync(request, one, orgId, ct);
        if (denied is not null)
        {
            return denied;
        }

        var row = await db.OrgWebhookEndpoints.FindAsync([orgId], ct);
        if (row is null)
        {
            return PayErrors.Status(404, "Not Found", "webhook endpoint not found");
        }

        string secret;
        try
        {
            secret = MintSecret();
            row.SecretCiphertext = box.Protect(secret);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("WrapKey", StringComparison.Ordinal))
        {
            return PayErrors.Status(503, "Service Unavailable", ex.Message);
        }

        row.SecretPrefix = secret[^4..];
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Json(new
        {
            org_id = orgId,
            url = row.Url,
            webhook_configured = true,
            secret_prefix = row.SecretPrefix,
            webhook_secret = secret
        }, OneClient.Json);
    }

    static async Task<IResult> TestPing(
        string orgId,
        HttpRequest request,
        OneClient one,
        PayDbContext db,
        CancellationToken ct)
    {
        var denied = await MemberGate.RequireWriterAsync(request, one, orgId, ct);
        if (denied is not null)
        {
            return denied;
        }

        var row = await db.OrgWebhookEndpoints.FindAsync([orgId], ct);
        if (row is null)
        {
            return PayErrors.Status(404, "Not Found", "webhook endpoint not found");
        }

        var eventId = "test-" + Guid.NewGuid().ToString("N");
        var payload = PayWebhookEnvelope.Serialize("webhook.test", eventId, orgId, new { ok = true });
        db.OrgWebhookDeliveries.Add(new OrgWebhookDeliveryRow
        {
            Id = Guid.NewGuid().ToString("N"),
            OrgId = orgId,
            EventId = eventId,
            EventType = "webhook.test",
            PayloadJson = payload,
            Status = "pending",
            NextAttemptAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
        return Results.Json(new { ok = true, event_id = eventId }, OneClient.Json);
    }

    static string MintSecret() => "whsec_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}

public sealed class PutOrgWebhookRequest
{
    public string? Url { get; set; }
}
