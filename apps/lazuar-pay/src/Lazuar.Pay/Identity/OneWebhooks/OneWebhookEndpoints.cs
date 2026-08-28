using System.Text.Json;
using Lazuar.Pay.Data;
using Lazuar.Pay.Hosting;
using Lazuar.Pay.Identity.Client;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Identity.OneWebhooks;

internal static class OneWebhookEndpoints
{
    public static void MapOneWebhooks(this WebApplication app)
    {
        app.MapPost("/v1/one/webhooks", Handle);
    }

    static async Task<IResult> Handle(
        HttpRequest request,
        PayDbContext db,
        IConfiguration config,
        CancellationToken ct)
    {
        using var reader = new StreamReader(request.Body);
        var json = await reader.ReadToEndAsync(ct);
        var secret = config["Pay:OneWebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            return PayErrors.Status(503, "Service Unavailable", "One webhook secret missing");
        }

        var provided = request.Headers["X-Lazuar-Signature"].ToString().Trim();
        var timestamp = request.Headers["X-Lazuar-Timestamp"].ToString().Trim();
        if (!OneWebhookSignature.TryVerify(secret, json, provided, timestamp))
        {
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
            return await ApplyAsync(doc, request, db, ct);
        }
    }

    static async Task<IResult> ApplyAsync(JsonDocument doc, HttpRequest request, PayDbContext db, CancellationToken ct)
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
        }

        if (type == "tenant.reactivated" && !string.IsNullOrWhiteSpace(orgId))
        {
            var settings = await db.OrgSettings.FindAsync([orgId], ct);
            if (settings is not null) settings.ChargesPaused = false;
        }

        await db.SaveChangesAsync(ct);
        return Results.Json(new { ok = true }, OneClient.Json);
    }

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

        return null;
    }
}
