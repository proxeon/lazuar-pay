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

    static async Task<IResult> Get(string token, CheckoutStore store, PayDbContext db, CancellationToken ct)
    {
        var session = await store.GetByPublicTokenAsync(token, ct);
        if (session is null)
        {
            return PayErrors.Status(404, "Not Found", "Checkout not found");
        }

        var row = await db.Checkouts.AsNoTracking().FirstAsync(x => x.Id == session.Id, ct);
        var settings = await db.OrgSettings.AsNoTracking().FirstOrDefaultAsync(x => x.OrgId == session.OrgId, ct);
        var provider = row.Provider ?? settings?.ActiveProvider;
        var emailRequired = PayProviders.TryNormalize(provider, out var p) && PayProviders.RequiresEmail(p);
        var started = !string.IsNullOrWhiteSpace(row.PspRedirectUrl);
        return Results.Json(new
        {
            token,
            amount = session.Amount,
            currency = session.Currency,
            status = session.Status,
            payer_name = session.PayerName,
            payer_email = session.PayerEmail,
            email_required = emailRequired,
            started,
            redirect_url = started && session.Status == "open" ? row.PspRedirectUrl : null
        }, OneClient.Json);
    }

    static async Task<IResult> Start(
        string token,
        StartPayRequest? body,
        CheckoutStore store,
        PayDbContext db,
        StripeHosted stripe,
        ChipHosted chip,
        BillplzHosted billplz,
        XenditHosted xendit,
        RazorpayHosted razorpay,
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
        if (!string.IsNullOrWhiteSpace(body?.Name))
        {
            row.PayerName = body.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(body?.Email))
        {
            row.PayerEmail = body.Email.Trim();
        }

        var provider = row.Provider ?? settings?.ActiveProvider;
        if (!PayProviders.TryNormalize(provider, out var name))
        {
            return PayErrors.Status(503, "Service Unavailable", "rail not configured");
        }

        if (PayProviders.RequiresEmail(name) && !BuyerEmail.IsUsable(row.PayerEmail))
        {
            return PayErrors.Status(400, "Bad Request", "email is required");
        }

        if (!string.IsNullOrWhiteSpace(row.PspRedirectUrl))
        {
            await db.SaveChangesAsync(ct);
            return Results.Json(new { redirect_url = row.PspRedirectUrl }, OneClient.Json);
        }

        IHostedRail rail = name switch
        {
            PayProviders.Stripe => stripe,
            PayProviders.Chip => chip,
            PayProviders.Billplz => billplz,
            PayProviders.Xendit => xendit,
            PayProviders.Razorpay => razorpay,
            _ => throw new InvalidOperationException("rail not configured")
        };

        try
        {
            // PSP HTTP then persist. A SaveChanges failure after the processor
            // already created a session may mint a second session on retry.
            var hosted = await rail.CreateHostedUrlAsync(row, ct);
            row.Provider = name;
            row.PspRedirectUrl = hosted.RedirectUrl;
            row.ProviderSessionId = hosted.ProviderSessionId;
            await db.SaveChangesAsync(ct);
            return Results.Json(new { redirect_url = hosted.RedirectUrl }, OneClient.Json);
        }
        catch (InvalidOperationException ex)
        {
            var status = ex.Message.Contains("callback base", StringComparison.Ordinal) ? 400 : 503;
            return PayErrors.Status(status, status == 400 ? "Bad Request" : "Service Unavailable", ex.Message);
        }
        catch (Stripe.StripeException)
        {
            return PayErrors.Status(503, "Service Unavailable", "Stripe rejected the org key");
        }
    }
}

public sealed class StartPayRequest
{
    public string? Name { get; set; }
    public string? Email { get; set; }
}
