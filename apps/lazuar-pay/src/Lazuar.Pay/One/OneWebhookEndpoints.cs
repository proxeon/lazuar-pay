using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Lazuar.Pay.One;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Webhooks;

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
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(json)));
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        if (providedBytes.Length != expectedBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes))
        {
            return PayErrors.Status(401, "Unauthorized", "Invalid HMAC");
        }

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        var type = doc.RootElement.TryGetProperty("type", out var t) ? t.GetString() : null;
        var delivery = doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetString() : Guid.NewGuid().ToString("N");
        var orgId = doc.RootElement.TryGetProperty("org_id", out var o) ? o.GetString() : null;
        if (await db.OneWebhookEvents.AnyAsync(x => x.DeliveryId == delivery, ct))
        {
            return Results.Ok(new { duplicate = true });
        }

        db.OneWebhookEvents.Add(new OneWebhookEventRow
        {
            Id = Guid.NewGuid().ToString("N"),
            DeliveryId = delivery ?? Guid.NewGuid().ToString("N"),
            EventType = type ?? "unknown",
            ReceivedAt = DateTimeOffset.UtcNow
        });

        if (type == "tenant.suspended" && !string.IsNullOrWhiteSpace(orgId))
        {
            var settings = await db.OrgSettings.FindAsync([orgId], ct);
            if (settings is null)
            {
                db.OrgSettings.Add(new OrgSettingsRow { OrgId = orgId, ChargesPaused = true, SstRegistered = false });
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
}
