using Lazuar.Pay.Checkouts;
using Lazuar.Pay.Data;
using Lazuar.Pay.Hosting;
using Lazuar.Pay.Identity.Client;
using Lazuar.Pay.PaymentLinks;
using Lazuar.Pay.Rails;
using Lazuar.Pay.Rails.Billplz;
using Lazuar.Pay.Rails.Chip;
using Lazuar.Pay.Money;
using Lazuar.Pay.Rails.Razorpay;
using Lazuar.Pay.Rails.Stripe;
using Lazuar.Pay.Rails.Test;
using Lazuar.Pay.Rails.Xendit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Lazuar.Pay.PublicPay;

internal static class PublicPayEndpoints
{
    public static void MapPublicPay(this WebApplication app)
    {
        app.MapGet("/v1/pay/{token}", Get);
        app.MapPost("/v1/pay/{token}/start", Start);
    }

    static async Task<IResult> Get(
        string token,
        string? slot_key,
        CheckoutStore store,
        PayDbContext db,
        IConfiguration config,
        CancellationToken ct)
    {
        var link = await db.PaymentLinks.AsNoTracking().FirstOrDefaultAsync(x => x.PublicToken == token, ct);
        if (link is not null)
        {
            return await GetLink(link, slot_key, db, config, ct);
        }

        var session = await store.GetByPublicTokenAsync(token, ct);
        if (session is null)
        {
            return PayErrors.Status(404, "Not Found", "Checkout not found");
        }

        var row = await db.Checkouts.AsNoTracking().FirstAsync(x => x.Id == session.Id, ct);
        return CheckoutView(token, row);
    }

    static async Task<IResult> GetLink(
        PaymentLinkRow link,
        string? slotKey,
        PayDbContext db,
        IConfiguration config,
        CancellationToken ct)
    {
        await PaymentLinkOccupancy.SerializeAsync(link.Id, async () =>
        {
            await PaymentLinkOccupancy.ExpireStaleAsync(
                db, link.Id, PaymentLinkOccupancy.ReservationTtl(config), ct);
            return 0;
        }, ct);

        var children = await db.Checkouts.AsNoTracking()
            .Where(x => x.PaymentLinkId == link.Id)
            .ToListAsync(ct);
        var taken = children.Count(c => PaymentLinkOccupancy.CountsTowardCapacity(c.Status));
        var paid = children.Count(c => c.Status == "paid");
        var remaining = PaymentLinkOccupancy.Remaining(link.MaxPayers, taken);
        var slot = NormalizeSlotKey(slotKey);
        var mine = slot is null ? null : children.FirstOrDefault(c => c.SlotKey == slot);

        if (mine is not null)
        {
            return CheckoutView(link.PublicToken, mine, remaining, link.MaxPayers, paid, taken);
        }

        if (PaymentLinkOccupancy.IsFull(link.MaxPayers, taken))
        {
            if (link.MaxPayers == 1 && paid >= 1)
            {
                var paidRow = children.First(c => c.Status == "paid");
                return CheckoutView(link.PublicToken, paidRow, remaining, link.MaxPayers, paid, taken);
            }

            return LinkView(link, "full", remaining, paid, taken, started: false, redirectUrl: null);
        }

        return LinkView(link, "open", remaining, paid, taken, started: false, redirectUrl: null);
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
        TestHosted test,
        IFulfillPaid fulfillment,
        IConfiguration config,
        IHostEnvironment env,
        CancellationToken ct)
    {
        var link = await db.PaymentLinks.FirstOrDefaultAsync(x => x.PublicToken == token, ct);
        CheckoutRow row;
        if (link is not null)
        {
            var minted = await MintOrResume(link, body, db, config, env, ct);
            if (minted.Error is not null)
            {
                return minted.Error;
            }

            row = minted.Row!;
        }
        else
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

            row = await db.Checkouts.FirstAsync(x => x.Id == session.Id, ct);
        }

        if (!string.IsNullOrWhiteSpace(body?.Name))
        {
            row.PayerName = body.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(body?.Email))
        {
            row.PayerEmail = body.Email.Trim();
        }

        var provider = row.Provider ?? link?.Provider;
        if (!PayProviders.TryNormalize(provider, out var name))
        {
            return PayErrors.Status(503, "Service Unavailable", "rail not configured");
        }

        if (PayProviders.RequiresEmail(name) && !BuyerEmail.IsUsable(row.PayerEmail))
        {
            return PayErrors.Status(400, "Bad Request", "email is required");
        }

        if (!string.IsNullOrWhiteSpace(row.PspRedirectUrl)
            || !string.IsNullOrWhiteSpace(row.ProviderSessionId))
        {
            if (string.IsNullOrWhiteSpace(row.PspRedirectUrl))
            {
                return PayErrors.Status(409, "Conflict", "Checkout is not open");
            }

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
            PayProviders.Test => test,
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
            if (PayProviders.IsTest(name))
            {
                db.PspWebhookEvents.Add(new PspWebhookEventRow
                {
                    OrgId = row.OrgId,
                    Provider = name,
                    EventId = hosted.ProviderSessionId ?? "test:" + row.Id,
                    ReceivedAt = DateTimeOffset.UtcNow
                });
                await fulfillment.FulfillPaidAsync(row.Id, name, hosted.ProviderSessionId, ct);
            }
            else
            {
                await db.SaveChangesAsync(ct);
            }

            return Results.Json(new { redirect_url = hosted.RedirectUrl }, OneClient.Json);
        }
        catch (InvalidOperationException ex)
        {
            await ExpireFailedReservation(db, row, ct);
            var status = ex.Message.Contains("callback base", StringComparison.Ordinal) ? 400 : 503;
            return PayErrors.Status(status, status == 400 ? "Bad Request" : "Service Unavailable", ex.Message);
        }
        catch (Stripe.StripeException)
        {
            await ExpireFailedReservation(db, row, ct);
            return PayErrors.Status(503, "Service Unavailable", "Stripe rejected the org key");
        }
    }

    static async Task<(CheckoutRow? Row, IResult? Error)> MintOrResume(
        PaymentLinkRow link,
        StartPayRequest? body,
        PayDbContext db,
        IConfiguration config,
        IHostEnvironment env,
        CancellationToken ct)
    {
        var settings = await db.OrgSettings.FindAsync([link.OrgId], ct);
        if (settings?.ChargesPaused == true)
        {
            return (null, PayErrors.Status(403, "Forbidden", "Org charges are paused"));
        }

        var slot = NormalizeSlotKey(body?.SlotKey);
        if (slot is null)
        {
            return (null, PayErrors.Status(400, "Bad Request", "slot_key is required"));
        }

        if (!PayProviders.TryNormalize(link.Provider, out var providerName))
        {
            return (null, PayErrors.Status(503, "Service Unavailable", "rail not configured"));
        }

        if (PayProviders.RequiresEmail(providerName) && !BuyerEmail.IsUsable(body?.Email))
        {
            return (null, PayErrors.Status(400, "Bad Request", "email is required"));
        }

        try
        {
            CheckoutUrls.Base(config, env);
        }
        catch (InvalidOperationException ex)
        {
            return (null, PayErrors.Status(503, "Service Unavailable", ex.Message));
        }

        return await PaymentLinkOccupancy.SerializeAsync(link.Id, async () =>
        {
            await using var tx = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(ct)
                : null;
            try
            {
                await PaymentLinkOccupancy.LockParentAsync(db, link.Id, ct);
                await PaymentLinkOccupancy.ExpireStaleAsync(
                    db, link.Id, PaymentLinkOccupancy.ReservationTtl(config), ct);

                var existing = await db.Checkouts.FirstOrDefaultAsync(
                    x => x.PaymentLinkId == link.Id && x.SlotKey == slot, ct);
                if (existing is not null)
                {
                    if (existing.Status is "paid" or "expired")
                    {
                        return ((CheckoutRow?)null, PayErrors.Status(409, "Conflict", "Checkout is not open"));
                    }

                    if (tx is not null)
                    {
                        await tx.CommitAsync(ct);
                    }

                    return (existing, (IResult?)null);
                }

                var taken = await db.Checkouts.CountAsync(
                    x => x.PaymentLinkId == link.Id && (x.Status == "open" || x.Status == "paid"),
                    ct);
                if (PaymentLinkOccupancy.IsFull(link.MaxPayers, taken))
                {
                    return ((CheckoutRow?)null, PayErrors.Status(409, "Conflict", "This pay link is full"));
                }

                var baseUrl = CheckoutUrls.Base(config, env);
                var row = new CheckoutRow
                {
                    Id = Guid.NewGuid().ToString("N"),
                    OrgId = link.OrgId,
                    Provider = link.Provider,
                    ProductId = link.ProductId,
                    PaymentLinkId = link.Id,
                    SlotKey = slot,
                    PublicToken = Convert.ToHexString(Guid.NewGuid().ToByteArray()) + Convert.ToHexString(Guid.NewGuid().ToByteArray()),
                    Amount = link.Amount,
                    Currency = link.Currency,
                    Status = "open",
                    Interval = "one_off",
                    PayerName = string.IsNullOrWhiteSpace(body?.Name) ? null : body.Name.Trim(),
                    PayerEmail = string.IsNullOrWhiteSpace(body?.Email) ? null : body.Email.Trim(),
                    SuccessUrl = baseUrl + "/c/" + link.PublicToken + "?status=verifying",
                    CancelUrl = baseUrl + "/c/" + link.PublicToken,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.Checkouts.Add(row);
                await db.SaveChangesAsync(ct);
                if (tx is not null)
                {
                    await tx.CommitAsync(ct);
                }

                return (row, (IResult?)null);
            }
            catch (DbUpdateException)
            {
                if (tx is not null)
                {
                    await tx.RollbackAsync(ct);
                }

                var raced = await db.Checkouts.AsNoTracking().FirstOrDefaultAsync(
                    x => x.PaymentLinkId == link.Id && x.SlotKey == slot, ct);
                if (raced is not null && raced.Status is not "paid" and not "expired")
                {
                    return (await db.Checkouts.FirstAsync(x => x.Id == raced.Id, ct), (IResult?)null);
                }

                return ((CheckoutRow?)null, PayErrors.Status(409, "Conflict", "This pay link is full"));
            }
        }, ct);
    }

    static async Task ExpireFailedReservation(PayDbContext db, CheckoutRow row, CancellationToken ct)
    {
        if (row.PaymentLinkId is null
            || row.Status != "open"
            || !string.IsNullOrWhiteSpace(row.PspRedirectUrl))
        {
            return;
        }

        row.Status = "expired";
        await db.SaveChangesAsync(ct);
    }

    static IResult CheckoutView(
        string token,
        CheckoutRow row,
        int? remaining = null,
        int? maxPayers = null,
        int? paidCount = null,
        int? takenCount = null)
    {
        var provider = row.Provider;
        var emailRequired = PayProviders.TryNormalize(provider, out var p) && PayProviders.RequiresEmail(p);
        var started = !string.IsNullOrWhiteSpace(row.PspRedirectUrl);
        return Results.Json(new
        {
            token,
            amount = row.Amount,
            currency = row.Currency,
            status = row.Status,
            payer_name = row.PayerName,
            payer_email = row.PayerEmail,
            email_required = emailRequired,
            started,
            provider,
            redirect_url = started && row.Status == "open" ? row.PspRedirectUrl : null,
            remaining,
            max_payers = maxPayers,
            paid_count = paidCount,
            taken_count = takenCount
        }, OneClient.Json);
    }

    static IResult LinkView(
        PaymentLinkRow link,
        string status,
        int? remaining,
        int paid,
        int taken,
        bool started,
        string? redirectUrl)
    {
        var emailRequired = PayProviders.TryNormalize(link.Provider, out var p) && PayProviders.RequiresEmail(p);
        return Results.Json(new
        {
            token = link.PublicToken,
            amount = link.Amount,
            currency = link.Currency,
            status,
            email_required = emailRequired,
            started,
            provider = link.Provider,
            redirect_url = redirectUrl,
            remaining,
            max_payers = link.MaxPayers,
            paid_count = paid,
            taken_count = taken
        }, OneClient.Json);
    }

    static string? NormalizeSlotKey(string? raw)
    {
        var slot = raw?.Trim();
        if (string.IsNullOrWhiteSpace(slot) || slot.Length is < 8 or > 128)
        {
            return null;
        }

        return slot;
    }
}

public sealed class StartPayRequest
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? SlotKey { get; set; }
}
