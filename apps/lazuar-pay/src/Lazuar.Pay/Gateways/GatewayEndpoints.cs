using Lazuar.Pay.Data;
using Lazuar.Pay.One;
using Lazuar.Pay.Secrets;

namespace Lazuar.Pay.Gateways;

internal static class GatewayEndpoints
{
    public static void MapGateways(this WebApplication app)
    {
        app.MapPut("/v1/orgs/{orgId}/gateway", Put);
        app.MapGet("/v1/orgs/{orgId}/gateway", Get);
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
        if (denied is not null) return denied;
        var provider = body?.Provider?.Trim().ToLowerInvariant();
        var secret = body?.Secret?.Trim();
        if (provider != StripeHosted.Provider)
        {
            return PayErrors.Status(400, "Bad Request", "Bar B first rail is stripe");
        }

        if (string.IsNullOrWhiteSpace(secret))
        {
            return PayErrors.Status(400, "Bad Request", "secret is required");
        }

        var last4 = secret.Length >= 4 ? secret[^4..] : secret;
        var wrapped = box.Protect(secret);
        var row = await db.GatewayCredentials.FindAsync([orgId, provider], ct);
        if (row is null)
        {
            db.GatewayCredentials.Add(new GatewayCredentialRow
            {
                OrgId = orgId,
                Provider = provider,
                Ciphertext = wrapped,
                Last4 = last4,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            row.Ciphertext = wrapped;
            row.Last4 = last4;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return Results.Json(new { org_id = orgId, provider, last4, capability = "hosted_link" }, OneClient.Json);
    }

    static async Task<IResult> Get(
        string orgId,
        HttpRequest request,
        OneClient one,
        PayDbContext db,
        CancellationToken ct)
    {
        var denied = await MemberGate.RequireMemberAsync(request, one, orgId, ct);
        if (denied is not null) return denied;
        var row = await db.GatewayCredentials.FindAsync([orgId, StripeHosted.Provider], ct);
        if (row is null)
        {
            return Results.Json(new { org_id = orgId, provider = StripeHosted.Provider, configured = false }, OneClient.Json);
        }

        return Results.Json(new { org_id = orgId, provider = row.Provider, last4 = row.Last4, configured = true, capability = "hosted_link" }, OneClient.Json);
    }
}

public sealed class PutGatewayRequest
{
    public string? Provider { get; set; }
    public string? Secret { get; set; }
}
