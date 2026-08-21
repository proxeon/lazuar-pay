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
        CancellationToken cancellationToken)
    {
        var orgId = body?.OrgId?.Trim();
        var denied = await MemberGate.RequireMemberAsync(request, one, orgId ?? "", cancellationToken);
        if (denied is not null)
        {
            return denied;
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
            Amount = body.Amount.Value,
            Currency = currency,
            Status = "open",
            SuccessUrl = body.SuccessUrl,
            CancelUrl = body.CancelUrl,
            CreatedAt = DateTimeOffset.UtcNow
        };

        session = store.Create(session, idempotency);
        return Results.Json(session, OneClient.Json, statusCode: 201);
    }

    static async Task<IResult> Get(
        string id,
        HttpRequest request,
        OneClient one,
        CheckoutStore store,
        CancellationToken cancellationToken)
    {
        var session = store.Get(id);
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
