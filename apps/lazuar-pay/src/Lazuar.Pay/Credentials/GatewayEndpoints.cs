using System.Security.Cryptography;
using Lazuar.Pay.Data;
using Lazuar.Pay.Hosting;
using Lazuar.Pay.Identity.Client;
using Lazuar.Pay.Rails;
using Lazuar.Pay.Rails.Razorpay;
using Lazuar.Pay.Rails.Solana;
using Lazuar.Pay.Secrets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

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
        IConfiguration config,
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

        if (PayProviders.IsTest(provider))
        {
            return PayErrors.Status(400, "Bad Request", "test processor does not take secrets");
        }

        string wrapped;
        string? wrappedWh;
        string last4;
        string? publicId;
        string? environment;
        if (PayProviders.UsesReceiveAddress(provider))
        {
            var receive = ReceiveAddressVault(body, config);
            if (receive.Error is not null)
            {
                return receive.Error;
            }

            wrapped = "";
            wrappedWh = null;
            last4 = receive.Last4!;
            publicId = receive.PublicId;
            environment = receive.Environment;
        }
        else
        {
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

            publicId = body?.PublicMerchantId?.Trim();
            if (PayProviders.RequiresPublicMerchantId(provider) && string.IsNullOrWhiteSpace(publicId))
            {
                return PayErrors.Status(400, "Bad Request", "public_merchant_id is required");
            }

            if (!PayProviders.AllowsPublicMerchantId(provider) && !string.IsNullOrWhiteSpace(publicId))
            {
                return PayErrors.Status(400, "Bad Request", "public_merchant_id is not used for this provider");
            }

            if (provider == PayProviders.Billplz && string.IsNullOrWhiteSpace(body?.Environment))
            {
                return PayErrors.Status(400, "Bad Request", "environment is required");
            }

            environment = null;
            if (!string.IsNullOrWhiteSpace(body?.Environment))
            {
                environment = body.Environment.Trim().ToLowerInvariant();
                if (environment is not ("test" or "live"))
                {
                    return PayErrors.Status(400, "Bad Request", "environment must be test or live");
                }
            }

            if (provider == PayProviders.Razorpay && !RazorpayHosted.TrySplit(secret, out _, out _))
            {
                return PayErrors.Status(400, "Bad Request", "secret must be key_id:key_secret");
            }

            if (provider == PayProviders.Chip && !TryChipPem(webhookSecret))
            {
                return PayErrors.Status(400, "Bad Request", "webhook_secret must be a CHIP PEM");
            }

            last4 = secret.Length >= 4 ? secret[^4..] : secret;
            if (provider == PayProviders.Razorpay && RazorpayHosted.TrySplit(secret, out var keyId, out _))
            {
                last4 = keyId.Length >= 4 ? keyId[^4..] : keyId;
            }

            try
            {
                wrapped = box.Protect(secret);
                wrappedWh = box.Protect(webhookSecret);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("WrapKey", StringComparison.Ordinal))
            {
                return PayErrors.Status(503, "Service Unavailable", ex.Message);
            }
        }

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
                Environment = environment ?? "test",
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
            if (environment is not null)
            {
                row.Environment = environment;
            }

            row.Last4 = last4;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await OrgSettingsStore.GetOrCreateAsync(db, orgId, ct);

        db.AuditEvents.Add(new AuditEventRow
        {
            Id = Guid.NewGuid().ToString("N"),
            OrgId = orgId,
            Action = "gateway.credentials.upsert",
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

        return Results.Json(GatewayJson(orgId, row, configured: true), OneClient.Json);
    }

    static async Task<IResult> Get(
        string orgId,
        string? provider,
        HttpRequest request,
        OneClient one,
        PayDbContext db,
        IHostEnvironment env,
        CancellationToken ct)
    {
        var denied = await MemberGate.RequireMemberAsync(request, one, orgId, ct);
        if (denied is not null)
        {
            return denied;
        }

        if (string.IsNullOrWhiteSpace(provider))
        {
            return PayErrors.Status(400, "Bad Request", "provider is required");
        }

        if (!PayProviders.TryNormalize(provider, out var name))
        {
            return PayErrors.Status(400, "Bad Request", "unknown provider");
        }

        if (PayProviders.IsTest(name) && PayProviders.AllowsTest(env))
        {
            return Results.Json(TestGatewayJson(orgId), OneClient.Json);
        }

        var row = await db.GatewayCredentials.AsNoTracking().FirstOrDefaultAsync(x => x.OrgId == orgId && x.Provider == name, ct);
        if (row is null)
        {
            return Results.Json(new { org_id = orgId, provider = name, configured = false }, OneClient.Json);
        }

        return Results.Json(GatewayJson(orgId, row, CredentialConfigured(name, row)), OneClient.Json);
    }

    static async Task<IResult> List(
        string orgId,
        HttpRequest request,
        OneClient one,
        PayDbContext db,
        IHostEnvironment env,
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
        var processors = PayProviders.Listed(env).Select(name =>
        {
            if (PayProviders.IsTest(name))
            {
                return TestGatewayJson(orgId);
            }

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

            return GatewayJson(orgId, row, CredentialConfigured(name, row));
        });

        return Results.Json(new { org_id = orgId, processors }, OneClient.Json);
    }

    static (IResult? Error, string? PublicId, string? Last4, string? Environment) ReceiveAddressVault(
        PutGatewayRequest? body,
        IConfiguration config)
    {
        if (!string.IsNullOrWhiteSpace(body?.Secret)
            || !string.IsNullOrWhiteSpace(body?.KeyId)
            || !string.IsNullOrWhiteSpace(body?.KeySecret))
        {
            return (PayErrors.Status(400, "Bad Request", "solana does not take an API secret"), null, null, null);
        }

        if (!string.IsNullOrWhiteSpace(body?.WebhookSecret))
        {
            return (PayErrors.Status(400, "Bad Request", "solana does not take a webhook secret"), null, null, null);
        }

        if (!SolanaAddress.TryNormalize(body?.PublicMerchantId, out var address))
        {
            return (PayErrors.Status(400, "Bad Request", "public_merchant_id must be a Solana wallet address"), null, null, null);
        }

        if (!PayProviders.TryNormalizeSolanaEnvironment(body?.Environment, out var environment))
        {
            return (PayErrors.Status(400, "Bad Request", "environment must be devnet or mainnet"), null, null, null);
        }

        if (!SolanaCluster.MatchesVault(SolanaCluster.FromConfig(config), environment))
        {
            return (PayErrors.Status(400, "Bad Request", "solana cluster mismatch"), null, null, null);
        }

        return (null, address, SolanaAddress.Last4(address), environment);
    }

    static bool CredentialConfigured(string name, GatewayCredentialRow row) =>
        !PayProviders.UsesReceiveAddress(name) || SolanaAddress.TryNormalize(row.PublicMerchantId, out _);

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

    static bool TryChipPem(string pem)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            return rsa.KeySize >= 2048;
        }
        catch (Exception)
        {
            return false;
        }
    }

    static object TestGatewayJson(string orgId) => new
    {
        org_id = orgId,
        provider = PayProviders.Test,
        last4 = (string?)null,
        configured = true,
        capability = PayProviders.Capability,
        public_merchant_id = (string?)null,
        environment = "test",
        webhook_configured = true
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
