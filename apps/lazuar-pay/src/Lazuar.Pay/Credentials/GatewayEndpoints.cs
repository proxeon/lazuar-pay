using Lazuar.Pay.Data;
using Lazuar.Pay.Hosting;
using Lazuar.Pay.Identity.Client;
using Lazuar.Pay.Rails;
using Lazuar.Pay.Rails.Razorpay;
using Lazuar.Pay.Secrets;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Credentials;

internal static class GatewayEndpoints
{
    public static void MapGateways(this WebApplication app)
    {
        app.MapPut("/v1/orgs/{orgId}/gateway", Put);
        app.MapGet("/v1/orgs/{orgId}/gateway", Get);
        app.MapGet("/v1/orgs/{orgId}/gateways", List);
    }

    static async Task<IResult> Put(
        string orgId,
        PutGatewayRequest? body,
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

        if (!PayProviders.TryNormalize(body?.Provider, out var provider))
        {
            return PayErrors.Status(400, "Bad Request", "unknown provider");
        }

        var secret = body?.Secret?.Trim();
        if (string.IsNullOrWhiteSpace(secret)
            && !string.IsNullOrWhiteSpace(body?.KeyId)
            && !string.IsNullOrWhiteSpace(body?.KeySecret))
        {
            secret = body.KeyId.Trim() + ":" + body.KeySecret.Trim();
        }

        var webhookSecret = body?.WebhookSecret?.Trim();
        if (string.IsNullOrWhiteSpace(secret))
        {
            return PayErrors.Status(400, "Bad Request", "secret is required");
        }

        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            return PayErrors.Status(400, "Bad Request", "webhook_secret is required");
        }

        var publicId = body?.PublicMerchantId?.Trim();
        if (PayProviders.RequiresPublicMerchantId(provider) && string.IsNullOrWhiteSpace(publicId))
        {
            return PayErrors.Status(400, "Bad Request", "public_merchant_id is required");
        }

        if (!PayProviders.AllowsPublicMerchantId(provider) && !string.IsNullOrWhiteSpace(publicId))
        {
            return PayErrors.Status(400, "Bad Request", "public_merchant_id is not used for this provider");
        }

        var environment = string.IsNullOrWhiteSpace(body?.Environment) ? "test" : body.Environment.Trim().ToLowerInvariant();
        if (environment is not ("test" or "live"))
        {
            return PayErrors.Status(400, "Bad Request", "environment must be test or live");
        }

        if (provider == PayProviders.Billplz && string.IsNullOrWhiteSpace(body?.Environment))
        {
            return PayErrors.Status(400, "Bad Request", "environment is required");
        }

        if (provider == PayProviders.Razorpay && !RazorpayHosted.TrySplit(secret, out _, out _))
        {
            return PayErrors.Status(400, "Bad Request", "secret must be key_id:key_secret");
        }

        var last4 = secret.Length >= 4 ? secret[^4..] : secret;
        if (provider == PayProviders.Razorpay && RazorpayHosted.TrySplit(secret, out var keyId, out _))
        {
            last4 = keyId.Length >= 4 ? keyId[^4..] : keyId;
        }

        var wrapped = box.Protect(secret);
        var wrappedWh = box.Protect(webhookSecret);
        var row = await db.GatewayCredentials.FindAsync([orgId, provider], ct);
        if (row is null)
        {
            row = new GatewayCredentialRow
            {
                OrgId = orgId,
                Provider = provider,
                Ciphertext = wrapped,
                WebhookCiphertext = wrappedWh,
                PublicMerchantId = publicId,
                Environment = environment,
                Last4 = last4,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.GatewayCredentials.Add(row);
        }
        else
        {
            row.Ciphertext = wrapped;
            row.WebhookCiphertext = wrappedWh;
            row.PublicMerchantId = publicId;
            row.Environment = environment;
            row.Last4 = last4;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }

        if (await db.OrgSettings.FindAsync([orgId], ct) is null)
        {
            db.OrgSettings.Add(new OrgSettingsRow { OrgId = orgId });
        }

        db.AuditEvents.Add(new AuditEventRow
        {
            Id = Guid.NewGuid().ToString("N"),
            OrgId = orgId,
            Action = "gateway.credentials.upsert",
            At = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return Results.Json(GatewayJson(orgId, row, configured: true), OneClient.Json);
    }

    static async Task<IResult> Get(
        string orgId,
        string? provider,
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

        if (string.IsNullOrWhiteSpace(provider))
        {
            return await List(orgId, request, one, db, ct);
        }

        if (!PayProviders.TryNormalize(provider, out var name))
        {
            return PayErrors.Status(400, "Bad Request", "unknown provider");
        }

        var row = await db.GatewayCredentials.AsNoTracking().FirstOrDefaultAsync(x => x.OrgId == orgId && x.Provider == name, ct);
        if (row is null)
        {
            return Results.Json(new { org_id = orgId, provider = name, configured = false }, OneClient.Json);
        }

        return Results.Json(GatewayJson(orgId, row, configured: true), OneClient.Json);
    }

    static async Task<IResult> List(
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

        var rows = await db.GatewayCredentials.AsNoTracking()
            .Where(x => x.OrgId == orgId)
            .ToListAsync(ct);
        var processors = PayProviders.All.Select(name =>
        {
            var row = rows.FirstOrDefault(x => x.Provider == name);
            if (row is null)
            {
                return (object)new
                {
                    org_id = orgId,
                    provider = name,
                    configured = false,
                    last4 = (string?)null,
                    capability = PayProviders.Capability,
                    public_merchant_id = (string?)null,
                    environment = (string?)null,
                    webhook_configured = false
                };
            }

            return GatewayJson(orgId, row, configured: true);
        });

        return Results.Json(new { org_id = orgId, processors }, OneClient.Json);
    }

    static object GatewayJson(string orgId, GatewayCredentialRow row, bool configured) => new
    {
        org_id = orgId,
        provider = row.Provider,
        last4 = row.Last4,
        configured,
        capability = PayProviders.Capability,
        public_merchant_id = row.PublicMerchantId,
        environment = row.Environment,
        webhook_configured = !string.IsNullOrWhiteSpace(row.WebhookCiphertext)
    };
}

public sealed class PutGatewayRequest
{
    public string? Provider { get; set; }
    public string? Secret { get; set; }
    public string? WebhookSecret { get; set; }
    public string? PublicMerchantId { get; set; }
    public string? Environment { get; set; }
    public string? KeyId { get; set; }
    public string? KeySecret { get; set; }
}
