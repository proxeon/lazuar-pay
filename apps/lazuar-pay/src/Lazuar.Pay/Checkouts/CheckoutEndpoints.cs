using Lazuar.Pay.Data;
using Lazuar.Pay.Hosting;
using Lazuar.Pay.Identity.Client;
using Lazuar.Pay.Rails;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Lazuar.Pay.Checkouts;

internal static class CheckoutEndpoints
{
    public static void MapCheckouts(this WebApplication app)
    {
        app.MapPost("/v1/checkouts", Create);
        app.MapGet("/v1/checkouts/{id}", Get);
        app.MapGet("/v1/orgs/{orgId}/checkouts", List);
    }

    static async Task<IResult> Create(
        CreateCheckoutRequest? body,
        HttpRequest request,
        OneClient one,
        CheckoutStore store,
        PayDbContext db,
        IHostEnvironment env,
        CancellationToken cancellationToken)
    {
        var orgId = body?.OrgId?.Trim();
        var denied = await MemberGate.RequireWriterAsync(request, one, orgId ?? "", cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var settings = orgId is null ? null : await db.OrgSettings.FindAsync([orgId], cancellationToken);
        if (settings is null && orgId is not null)
        {
            settings = new OrgSettingsRow { OrgId = orgId };
            db.OrgSettings.Add(settings);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (settings?.ChargesPaused == true)
        {
            return PayErrors.Status(403, "Forbidden", "Org charges are paused");
        }

        if (body?.Amount is null || body.Amount <= 0)
        {
            return PayErrors.Status(400, "Bad Request", "amount must be greater than 0");
        }

        if (!PayProviders.TryNormalize(body.Provider, out var provider))
        {
            return PayErrors.Status(400, "Bad Request", "unknown provider");
        }

        if (PayProviders.IsTest(provider))
        {
            if (!PayProviders.AllowsTest(env))
            {
                return PayErrors.Status(400, "Bad Request", "test processor is not enabled");
            }
        }
        else
        {
            var cred = await db.GatewayCredentials.AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrgId == orgId && x.Provider == provider, cancellationToken);
            if (cred is null)
            {
                return PayErrors.Status(400, "Bad Request", "rail not configured");
            }
        }

        var currency = string.IsNullOrWhiteSpace(body.Currency) ? "MYR" : body.Currency.Trim().ToUpperInvariant();
        var idempotency = request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotency))
        {
            idempotency = body.IdempotencyKey;
        }

        var session = new CheckoutSession
        {
            Id = Guid.NewGuid().ToString("N"),
            OrgId = orgId!,
            Provider = provider,
            ProductId = string.IsNullOrWhiteSpace(body.ProductId) ? null : body.ProductId.Trim(),
            PublicToken = Convert.ToHexString(Guid.NewGuid().ToByteArray()) + Convert.ToHexString(Guid.NewGuid().ToByteArray()),
            Amount = body.Amount.Value,
            Currency = currency,
            Status = "open",
            Interval = "one_off",
            SuccessUrl = body.SuccessUrl,
            CancelUrl = body.CancelUrl,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var mintedId = session.Id;
        try
        {
            session = await store.CreateAsync(session, idempotency, cancellationToken);
        }
        catch (IdempotencyConflictException)
        {
            return PayErrors.Status(409, "Conflict", "idempotency key reused with a different body");
        }

        var created = session.Id == mintedId;
        return Results.Json(session, OneClient.Json, statusCode: created ? 201 : 200);
    }

    static async Task<IResult> Get(
        string id,
        HttpRequest request,
        OneClient one,
        CheckoutStore store,
        CancellationToken cancellationToken)
    {
        var session = await store.GetAsync(id, cancellationToken);
        if (session is null)
        {
            return PayErrors.Status(404, "Not Found", "Checkout not found");
        }

        var denied = await MemberGate.RequireMemberAsync(request, one, session.OrgId, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        return Results.Json(session, OneClient.Json);
    }

    static async Task<IResult> List(
        string orgId,
        HttpRequest request,
        OneClient one,
        PayDbContext db,
        CancellationToken cancellationToken)
    {
        var denied = await MemberGate.RequireMemberAsync(request, one, orgId, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var rows = await db.Checkouts.AsNoTracking()
            .Where(x => x.OrgId == orgId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var productIds = rows
            .Select(x => x.ProductId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();
        var names = productIds.Count == 0
            ? new Dictionary<string, string>()
            : await db.Products.AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        return Results.Json(rows.Select(r => new
        {
            id = r.Id,
            org_id = r.OrgId,
            provider = r.Provider,
            amount = r.Amount,
            currency = r.Currency,
            status = r.Status,
            public_token = r.PublicToken,
            created_at = r.CreatedAt,
            label = r.ProductId is not null && names.TryGetValue(r.ProductId, out var name) ? name : null
        }), OneClient.Json);
    }
}
