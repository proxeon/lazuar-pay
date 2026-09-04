using System.Text.Json;
using Lazuar.Pay.Data;
using Lazuar.Pay.Hosting;
using Lazuar.Pay.Identity.Client;
using Lazuar.Pay.Secrets;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Identity.OneWebhooks;

internal static class OneWebhookEndpoints
{
    public static void MapOneWebhooks(this WebApplication app)
    {
        app.MapPost("/v1/one/webhooks", Handle);
        app.MapPut("/v1/orgs/{orgId}/one-webhook", Put);
        app.MapGet("/v1/orgs/{orgId}/one-webhook", Get);
    }

    static async Task<IResult> Handle(
        HttpRequest request,
        PayDbContext db,
        IConfiguration config,
        SecretBox box,
        OneWhoamiCache cache,
        CancellationToken ct)
    {
        using var reader = new StreamReader(request.Body);
        var json = await reader.ReadToEndAsync(ct);
        var secret = await ResolveSecretAsync(json, db, config, box, ct);
        var provided = request.Headers["X-Lazuar-Signature"].ToString().Trim();
        var timestamp = request.Headers["X-Lazuar-Timestamp"].ToString().Trim();
        if (string.IsNullOrWhiteSpace(secret)
            || !OneWebhookSignature.TryVerify(secret, json, provided, timestamp))
        {
            // Same 401 whether the secret is missing or the HMAC is wrong — do not
            // leak whether an org has a stored One webhook secret (002/012).
            return PayErrors.Status(401, "Unauthorized", "Invalid HMAC");
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return PayErrors.Status(400, "Bad Request", "invalid event");
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return PayErrors.Status(400, "Bad Request", "invalid event");
        }

        using (doc)
        {
            return await ApplyAsync(doc, request, db, cache, ct);
        }
    }

    static async Task<IResult> Put(
        string orgId,
        PutOneWebhookRequest? body,
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

        var webhookSecret = body?.WebhookSecret?.Trim();
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            return PayErrors.Status(400, "Bad Request", "webhook_secret is required");
        }

        string wrapped;
        try
        {
            wrapped = box.Protect(webhookSecret);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("WrapKey", StringComparison.Ordinal))
        {
            return PayErrors.Status(503, "Service Unavailable", ex.Message);
        }

        var settings = await db.OrgSettings.FindAsync([orgId], ct);
        if (settings is null)
        {
            settings = new OrgSettingsRow { OrgId = orgId, OneWebhookCiphertext = wrapped };
            db.OrgSettings.Add(settings);
        }
        else
        {
            settings.OneWebhookCiphertext = wrapped;
        }

        db.AuditEvents.Add(new AuditEventRow
        {
            Id = Guid.NewGuid().ToString("N"),
            OrgId = orgId,
            Action = "one.webhook_secret.upsert",
            At = DateTimeOffset.UtcNow
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
        }
        return Results.Json(View(orgId, configured: true), OneClient.Json);
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

        var settings = await db.OrgSettings.FindAsync([orgId], ct);
        var configured = !string.IsNullOrWhiteSpace(settings?.OneWebhookCiphertext);
        return Results.Json(View(orgId, configured), OneClient.Json);
    }

    static async Task<string?> ResolveSecretAsync(
        string json,
        PayDbContext db,
        IConfiguration config,
        SecretBox box,
        CancellationToken ct)
    {
        // Process secret wins when set so an org cannot replace One's control channel
        // with a self-chosen HMAC key (002/003).
        var process = config["Pay:OneWebhookSecret"];
        if (!string.IsNullOrWhiteSpace(process))
        {
            return process;
        }

        var orgId = PeekOrgId(json);
        if (string.IsNullOrWhiteSpace(orgId))
        {
            return null;
        }

        var settings = await db.OrgSettings.FindAsync([orgId], ct);
        if (string.IsNullOrWhiteSpace(settings?.OneWebhookCiphertext))
        {
            return null;
        }

        try
        {
            var stored = box.Unprotect(settings.OneWebhookCiphertext);
            return string.IsNullOrWhiteSpace(stored) ? null : stored;
        }
        catch (Exception)
        {
            return null;
        }
    }

    static string? PeekOrgId(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return ReadOrgId(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static async Task<IResult> ApplyAsync(
        JsonDocument doc,
        HttpRequest request,
        PayDbContext db,
        OneWhoamiCache cache,
        CancellationToken ct)
    {
        var type = doc.RootElement.TryGetProperty("type", out var t) ? t.GetString() : null;
        var bodyId = doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        var headerId = request.Headers["X-Lazuar-Event-Id"].ToString().Trim();
        var delivery = !string.IsNullOrWhiteSpace(bodyId) ? bodyId.Trim() : headerId;
        if (string.IsNullOrWhiteSpace(delivery))
        {
            return PayErrors.Status(400, "Bad Request", "event id required");
        }

        var orgId = ReadOrgId(doc.RootElement);
        if (await db.OneWebhookEvents.AnyAsync(x => x.DeliveryId == delivery, ct))
        {
            return Results.Ok(new { duplicate = true });
        }

        db.OneWebhookEvents.Add(new OneWebhookEventRow
        {
            Id = Guid.NewGuid().ToString("N"),
            DeliveryId = delivery,
            EventType = type ?? "unknown",
            ReceivedAt = DateTimeOffset.UtcNow
        });

        if (type == "tenant.suspended" && !string.IsNullOrWhiteSpace(orgId))
        {
            var settings = await db.OrgSettings.FindAsync([orgId], ct);
            if (settings is null)
            {
                db.OrgSettings.Add(new OrgSettingsRow { OrgId = orgId, ChargesPaused = true });
            }
            else
            {
                settings.ChargesPaused = true;
            }

            cache.InvalidateOrg(orgId);
        }

        if (type == "tenant.reactivated" && !string.IsNullOrWhiteSpace(orgId))
        {
            var settings = await db.OrgSettings.FindAsync([orgId], ct);
            if (settings is not null) settings.ChargesPaused = false;
        }

        if (type == "api_key.revoked")
        {
            var keyId = ReadKeyId(doc.RootElement);
            if (!string.IsNullOrWhiteSpace(keyId))
            {
                cache.InvalidateKey(keyId);
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return Results.Ok(new { duplicate = true });
        }
        return Results.Json(new { ok = true }, OneClient.Json);
    }

    static object View(string orgId, bool configured) => new
    {
        org_id = orgId,
        webhook_configured = configured
    };

    static string? ReadOrgId(JsonElement root)
    {
        if (root.TryGetProperty("org_id", out var o))
        {
            var orgId = o.GetString();
            if (!string.IsNullOrWhiteSpace(orgId))
            {
                return orgId;
            }
        }

        if (root.TryGetProperty("tenant_id", out var tenant))
        {
            var tenantId = tenant.GetString();
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                return tenantId;
            }
        }

        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
        {
            return ReadOrgId(data);
        }

        return null;
    }

    static string? ReadKeyId(JsonElement root)
    {
        if (root.TryGetProperty("key_id", out var k))
        {
            var keyId = k.GetString();
            if (!string.IsNullOrWhiteSpace(keyId))
            {
                return keyId.Trim();
            }
        }

        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
        {
            return ReadKeyId(data);
        }

        return null;
    }
}

public sealed class PutOneWebhookRequest
{
    public string? WebhookSecret { get; set; }
}
