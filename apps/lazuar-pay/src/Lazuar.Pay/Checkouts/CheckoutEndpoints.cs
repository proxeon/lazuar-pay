using Lazuar.Pay.Data;
using Lazuar.Pay.One;

namespace Lazuar.Pay.Checkouts;

internal static class CheckoutEndpoints
{
    public static void MapCheckouts(this WebApplication app)
    {
        app.MapPost("/v1/checkouts", Create);
        app.MapGet("/v1/checkouts/{id}", Get);
    }

    static async Task<IResult> Create(
        CreateCheckoutRequest? body,
        HttpRequest request,
        OneClient one,
        CheckoutStore store,
        PayDbContext db,
        CancellationToken cancellationToken)
    {
        var orgId = body?.OrgId?.Trim();
        var denied = await MemberGate.RequireMemberAsync(request, one, orgId ?? "", cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var settings = orgId is null ? null : await db.OrgSettings.FindAsync([orgId], cancellationToken);
        if (settings is null && orgId is not null)
        {
            settings = new OrgSettingsRow { OrgId = orgId, SstRegistered = false };
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
            PublicToken = Convert.ToHexString(Guid.NewGuid().ToByteArray()) + Convert.ToHexString(Guid.NewGuid().ToByteArray()),
            Amount = body.Amount.Value,
            Currency = currency,
            Status = "open",
            Interval = "one_off",
            SuccessUrl = body.SuccessUrl,
            CancelUrl = body.CancelUrl,
            CreatedAt = DateTimeOffset.UtcNow
        };

        session = await store.CreateAsync(session, idempotency, cancellationToken);
        return Results.Json(session, OneClient.Json, statusCode: 201);
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
}
