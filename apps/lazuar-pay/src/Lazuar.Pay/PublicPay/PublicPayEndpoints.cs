using Lazuar.Pay.Checkouts;
using Lazuar.Pay.Data;
using Lazuar.Pay.Gateways;
using Lazuar.Pay.One;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.PublicPay;

internal static class PublicPayEndpoints
{
    public static void MapPublicPay(this WebApplication app)
    {
        app.MapGet("/v1/pay/{token}", Get);
        app.MapPost("/v1/pay/{token}/start", Start);
    }

    static async Task<IResult> Get(string token, CheckoutStore store, CancellationToken ct)
    {
        var session = await store.GetByPublicTokenAsync(token, ct);
        if (session is null)
        {
            return PayErrors.Status(404, "Not Found", "Checkout not found");
        }

        return Results.Json(new
        {
            token,
            amount = session.Amount,
            currency = session.Currency,
            status = session.Status,
            payer_name = session.PayerName,
            payer_email = session.PayerEmail
        }, OneClient.Json);
    }

    static async Task<IResult> Start(
        string token,
        StartPayRequest? body,
        CheckoutStore store,
        PayDbContext db,
        StripeHosted stripe,
        CancellationToken ct)
    {
        var session = await store.GetByPublicTokenAsync(token, ct);
        if (session is null)
        {
            return PayErrors.Status(404, "Not Found", "Checkout not found");
        }

        if (session.Status is "paid" or "expired")
        {
            return PayErrors.Status(409, "Conflict", "Checkout is not open");
        }

        var settings = await db.OrgSettings.FindAsync([session.OrgId], ct);
        if (settings?.ChargesPaused == true)
        {
            return PayErrors.Status(403, "Forbidden", "Org charges are paused");
        }

        var row = await db.Checkouts.FirstAsync(x => x.Id == session.Id, ct);
        if (!string.IsNullOrWhiteSpace(body?.Name)) row.PayerName = body.Name.Trim();
        if (!string.IsNullOrWhiteSpace(body?.Email)) row.PayerEmail = body.Email.Trim();

        try
        {
            var url = await stripe.CreateHostedUrlAsync(row, ct);
            row.PspRedirectUrl = url;
            await db.SaveChangesAsync(ct);
            return Results.Json(new { redirect_url = url }, OneClient.Json);
        }
        catch (InvalidOperationException ex)
        {
            return PayErrors.Status(503, "Service Unavailable", ex.Message);
        }
    }
}

public sealed class StartPayRequest
{
    public string? Name { get; set; }
    public string? Email { get; set; }
}
