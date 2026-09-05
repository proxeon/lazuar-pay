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
using Lazuar.Pay.Rails.Solana;
using Lazuar.Pay.Rails.Stripe;
using Lazuar.Pay.Rails.Test;
using Lazuar.Pay.Rails.Xendit;
using Lazuar.Pay.Webhooks.Outbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Lazuar.Pay.PublicPay;

internal static class PublicPayEndpoints
{
    // Issue 007 (issues/001): double-click double-starts both passed the read-side resume
    // guard (PspRedirectUrl == null), both minted, and the second write overwrote the first
    // session — on Solana the tab holding the overwritten QR could pay on-chain into a
    // reference nobody would ever confirm. The mint is now serialized per checkout, and the
    // persist itself is a conditional update so a second replica cannot win either.
    static readonly KeyedGates StartGates = new();

    public static void MapPublicPay(this WebApplication app)
    {
        app.MapGet("/v1/pay/{token}", Get);
        app.MapPost("/v1/pay/{token}/start", Start);
        app.MapPost("/v1/pay/{token}/confirm", Confirm);
    }

    static async Task<IResult> Get(
        string token,
        string? slot_key,
        CheckoutStore store,
        PayDbContext db,
        IConfiguration config,
        ProcessorRemote remote,
        CancellationToken ct)
    {
        var link = await db.PaymentLinks.AsNoTracking().FirstOrDefaultAsync(x => x.PublicToken == token, ct);
        if (link is not null)
        {
            return await GetLink(link, slot_key, db, config, remote, ct);
        }

        var session = await store.GetByPublicTokenAsync(token, ct);
        if (session is null)
        {
            return PayErrors.Status(404, "Not Found", "Checkout not found");
        }

        var row = await db.Checkouts.AsNoTracking().FirstAsync(x => x.Id == session.Id, ct);
        if (!string.IsNullOrWhiteSpace(row.PaymentLinkId))
        {
            var parent = await db.PaymentLinks.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == row.PaymentLinkId, ct);
            if (parent is not null)
            {
                return await GetLink(parent, row.SlotKey ?? slot_key, db, config, remote, ct);
            }
        }

        return CheckoutView(token, row, config);
    }

    static async Task<IResult> GetLink(
        PaymentLinkRow link,
        string? slotKey,
        PayDbContext db,
        IConfiguration config,
        ProcessorRemote remote,
        CancellationToken ct)
    {
        await PaymentLinkOccupancy.SerializeAsync(link.Id, async () =>
        {
            var settings = await db.OrgSettings.FindAsync([link.OrgId], ct);
            IReadOnlyList<CheckoutRow> expired;
            if (settings?.ChargesPaused == true)
            {
                expired = await PaymentLinkOccupancy.ExpireOpenAsync(db, link.Id, ct);
            }
            else
            {
                expired = await PaymentLinkOccupancy.ExpireStaleAsync(
                    db, link.Id, PaymentLinkOccupancy.ReservationTtl(config), ct);
            }

            await ExpireRemoteAsync(remote, expired, ct);
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
            return CheckoutView(link.PublicToken, mine, config, remaining, link.MaxPayers, paid, taken, mine: true);
        }

        if (PaymentLinkOccupancy.IsFull(link.MaxPayers, taken))
        {
            if (link.MaxPayers == 1 && paid >= 1)
            {
                return LinkView(link, "already_paid", remaining, paid, taken, started: false, redirectUrl: null, config);
            }

            return LinkView(link, "full", remaining, paid, taken, started: false, redirectUrl: null, config);
        }

        return LinkView(link, "open", remaining, paid, taken, started: false, redirectUrl: null, config);
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
        SolanaHosted solana,
        TestHosted test,
        IFulfillPaid fulfillment,
        ProcessorRemote remote,
        IConfiguration config,
        IHostEnvironment env,
        CancellationToken ct)
    {
        var maxStarts = config.GetValue("Pay:StartMaxPerMinute", 20);
        if (maxStarts > 0 && !PublicPayLimiter.TryAcquire(token, maxStarts, 60))
        {
            return PayErrors.Status(429, "Too Many Requests", "Too many start attempts");
        }

        var link = await db.PaymentLinks.FirstOrDefaultAsync(x => x.PublicToken == token, ct);
        CheckoutRow row;
        if (link is not null)
        {
            var minted = await MintOrResume(link, body, db, config, env, remote, ct);
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

            if (session.Status is "paid" or "expired" or "failed")
            {
                // Issue 004 (issues/001): a failed checkout tied to a past_due subscription is
                // the subscription's only recovery path — Start used to 409 on every non-open
                // status, and since no rail ever re-bills, past_due was a dead end the payer
                // could never leave. Failed ONE-OFF checkouts stay terminal (a fresh checkout
                // is the retry); expired stays terminal (late-pay refund logic depends on it).
                if (session.Status != "failed"
                    || await db.Subscriptions.AsNoTracking()
                        .FirstOrDefaultAsync(x => x.CheckoutId == session.Id, ct) is null)
                {
                    return PayErrors.Status(409, "Conflict", "Checkout is not open");
                }
            }

            var settings = await db.OrgSettings.FindAsync([session.OrgId], ct);
            if (settings?.ChargesPaused == true)
            {
                return PayErrors.Status(403, "Forbidden", "Org charges are paused");
            }

            row = await db.Checkouts.FirstAsync(x => x.Id == session.Id, ct);
            if (session.Status == "failed")
            {
                // CAS failed→open so exactly one retryer wins; drop the dead PSP session so a
                // fresh hosted URL mints instead of resuming the spent one.
                if (!await CheckoutTransitions.TryTransitionAsync(db, row, from: "failed", to: "open", ct))
                {
                    return PayErrors.Status(409, "Conflict", "Checkout is not open");
                }

                row.PspRedirectUrl = null;
                row.ProviderSessionId = null;
                await db.SaveChangesAsync(ct);
            }
        }

        return await StartGates.RunAsync(row.Id, async () =>
        {
            // The row was loaded before this request acquired the gate — a concurrent start
            // that minted while we waited may have persisted its session since. Reload under
            // the gate so the resume guard below decides on committed state, not a stale copy.
            // Payer fields are assigned AFTER the reload (reloading would otherwise wipe
            // unsaved modifications) and persist with the SaveChanges calls below.
            await db.Entry(row).ReloadAsync(ct);

            if (!string.IsNullOrWhiteSpace(body?.Name))
            {
                var payerName = body.Name.Trim();
                row.PayerName = payerName.Length > 200 ? payerName[..200] : payerName;
            }

            if (!string.IsNullOrWhiteSpace(body?.Email))
            {
                var email = body.Email.Trim();
                row.PayerEmail = email.Length > 254 ? email[..254] : email;
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
                return StartedPay(row.PspRedirectUrl);
            }

            IHostedRail rail = name switch
            {
                PayProviders.Stripe => stripe,
                PayProviders.Chip => chip,
                PayProviders.Billplz => billplz,
                PayProviders.Xendit => xendit,
                PayProviders.Razorpay => razorpay,
                PayProviders.Solana => solana,
                PayProviders.Test => test,
                _ => throw new InvalidOperationException("rail not configured")
            };

            try
            {
                // PSP HTTP then persist. A SaveChanges failure after the processor
                // already created a session may mint a second session on retry.
                var hosted = await rail.CreateHostedUrlAsync(row, ct);
                row.Provider = name;
                row.ProviderSessionId = hosted.ProviderSessionId;
                row.PspRedirectUrl = hosted.Url;
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
                else if (CheckoutTransitions.IsNpgsql(db))
                {
                    // Issue 007: conditional persist — exactly one minted session may land.
                    // The WHERE clause fails when another replica minted and persisted first;
                    // our duplicate session is discarded and the winner's URL is returned so
                    // the payer is never shown a redirect/QR that no confirmation will ever
                    // reference (the in-process gate above covers same-process double-clicks).
                    var claimed = await db.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        UPDATE public.checkouts
                        SET "PspRedirectUrl" = {hosted.Url}, "ProviderSessionId" = {hosted.ProviderSessionId}, "Provider" = {name}
                        WHERE "Id" = {row.Id} AND "PspRedirectUrl" IS NULL AND "ProviderSessionId" IS NULL
                        """,
                        ct);
                    if (claimed == 0)
                    {
                        db.Entry(row).State = EntityState.Detached;
                        var winner = await db.Checkouts.AsNoTracking().FirstAsync(x => x.Id == row.Id, ct);
                        return string.IsNullOrWhiteSpace(winner.PspRedirectUrl)
                            ? PayErrors.Status(409, "Conflict", "Checkout is not open")
                            : StartedPay(winner.PspRedirectUrl);
                    }

                    // Backdate the originals so SaveChanges cannot re-issue these writes and
                    // race the row again; only other pending changes (payer name/email) persist.
                    var entry = db.Entry(row);
                    entry.Property(x => x.Provider).OriginalValue = name;
                    entry.Property(x => x.ProviderSessionId).OriginalValue = hosted.ProviderSessionId;
                    entry.Property(x => x.PspRedirectUrl).OriginalValue = hosted.Url;
                    await db.SaveChangesAsync(ct);
                }
                else
                {
                    await db.SaveChangesAsync(ct);
                }

                return StartedPay(hosted.Url);
            }
            catch (InvalidOperationException ex)
            {
                await ExpireFailedReservation(db, row, remote, ct);
                var status = ex.Message.Contains("callback base", StringComparison.Ordinal) ? 400 : 503;
                return PayErrors.Status(status, status == 400 ? "Bad Request" : "Service Unavailable", ex.Message);
            }
            catch (Stripe.StripeException)
            {
                await ExpireFailedReservation(db, row, remote, ct);
                return PayErrors.Status(503, "Service Unavailable", "Stripe rejected the org key");
            }
        }, ct);
    }

    static async Task<IResult> Confirm(
        string token,
        ConfirmPayRequest? body,
        CheckoutStore store,
        PayDbContext db,
        SolanaConfirm confirm,
        IConfiguration config,
        CancellationToken ct)
    {
        var maxStarts = config.GetValue("Pay:StartMaxPerMinute", 20);
        if (maxStarts > 0 && !PublicPayLimiter.TryAcquire("confirm:" + token, maxStarts, 60))
        {
            return PayErrors.Status(429, "Too Many Requests", "Too many confirm attempts");
        }

        var session = await store.GetByPublicTokenAsync(token, ct);
        CheckoutRow? row = null;
        if (session is not null)
        {
            row = await db.Checkouts.FirstOrDefaultAsync(x => x.Id == session.Id, ct);
        }
        else
        {
            var link = await db.PaymentLinks.AsNoTracking().FirstOrDefaultAsync(x => x.PublicToken == token, ct);
            if (link is null)
            {
                return PayErrors.Status(404, "Not Found", "Checkout not found");
            }

            return PayErrors.Status(400, "Bad Request", "confirm a started checkout token");
        }

        if (row is null)
        {
            return PayErrors.Status(404, "Not Found", "Checkout not found");
        }

        try
        {
            return await confirm.ConfirmAsync(row, body?.Signature?.Trim() ?? "", ct);
        }
        catch (SolanaRpcThrottledException)
        {
            // Issue 011 (issues/003): ConfirmAsync rethrows the throttle for the poller's
            // backoff, but on this public endpoint that surfaced as an unhandled bare 500
            // (no exception handler is registered) and invited retry storms against the
            // same throttled RPC. Map it like every other upstream hiccup.
            return PayErrors.Status(503, "Service Unavailable", "solana rpc throttled, retry shortly");
        }
    }

    static async Task<(CheckoutRow? Row, IResult? Error)> MintOrResume(
        PaymentLinkRow link,
        StartPayRequest? body,
        PayDbContext db,
        IConfiguration config,
        IHostEnvironment env,
        ProcessorRemote remote,
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
                var expired = await PaymentLinkOccupancy.ExpireStaleAsync(
                    db, link.Id, PaymentLinkOccupancy.ReservationTtl(config), ct);
                await ExpireRemoteAsync(remote, expired, ct);

                var existing = await db.Checkouts.FirstOrDefaultAsync(
                    x => x.PaymentLinkId == link.Id && x.SlotKey == slot, ct);
                if (existing is not null)
                {
                    if (existing.Status == "paid")
                    {
                        return ((CheckoutRow?)null, PayErrors.Status(409, "Conflict", "Checkout is not open"));
                    }

                    if (existing.Status is "expired" or "failed")
                    {
                        existing.SlotKey = existing.SlotKey + ":burned:" + existing.Id;
                    }
                    else
                    {
                        if (tx is not null)
                        {
                            await tx.CommitAsync(ct);
                        }

                        return (existing, (IResult?)null);
                    }
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

                // Issue 011 (issues/001): the failed INSERT stays tracked after a
                // DbUpdateException — RollbackAsync does not clear the change tracker — so
                // every later SaveChanges on this scoped context re-attempted the doomed row
                // and threw again: the "recovered" raced checkout 503'd forever and the
                // payer's edits plus PspRedirectUrl never persisted. Clear before re-querying,
                // exactly as CheckoutStore.CreateAsync does for the same race.
                db.ChangeTracker.Clear();

                var raced = await db.Checkouts.AsNoTracking().FirstOrDefaultAsync(
                    x => x.PaymentLinkId == link.Id && x.SlotKey == slot, ct);
                if (raced is not null && raced.Status is not "paid" and not "expired" and not "failed")
                {
                    return (await db.Checkouts.FirstAsync(x => x.Id == raced.Id, ct), (IResult?)null);
                }

                return ((CheckoutRow?)null, PayErrors.Status(409, "Conflict", "This pay link is full"));
            }
        }, ct);
    }

    static async Task ExpireFailedReservation(PayDbContext db, CheckoutRow row, ProcessorRemote remote, CancellationToken ct)
    {
        if (row.PaymentLinkId is null
            || row.Status != "open"
            || !string.IsNullOrWhiteSpace(row.PspRedirectUrl))
        {
            return;
        }

        // Issue 002: compare-and-set off "open" — a concurrent webhook may have fulfilled this
        // row between the read and the write; the old blind write could erase a committed "paid".
        if (!await CheckoutTransitions.TryLeaveOpenAsync(db, row, "expired", ct))
        {
            return;
        }
        await OutboundWebhookEnqueue.TryAddAsync(
            db,
            row.OrgId,
            "expired:" + row.Id,
            PayWebhookEnvelope.Expired,
            new { checkout_id = row.Id, payment_link_id = row.PaymentLinkId, reason = "start_failed" },
            ct);
        await db.SaveChangesAsync(ct);
        await remote.ExpireAsync(row, ct);
    }

    static async Task ExpireRemoteAsync(ProcessorRemote remote, IReadOnlyList<CheckoutRow> rows, CancellationToken ct)
    {
        foreach (var row in rows)
        {
            await remote.ExpireAsync(row, ct);
        }
    }

    static IResult CheckoutView(
        string token,
        CheckoutRow row,
        IConfiguration config,
        int? remaining = null,
        int? maxPayers = null,
        int? paidCount = null,
        int? takenCount = null,
        bool mine = true)
    {
        var provider = row.Provider;
        var emailRequired = PayProviders.TryNormalize(provider, out var p) && PayProviders.RequiresEmail(p);
        var started = !string.IsNullOrWhiteSpace(row.PspRedirectUrl);
        var liveUrl = started && row.Status == "open" ? row.PspRedirectUrl : null;
        var onPage = PayProviders.IsOnPageUrl(liveUrl);
        var isSolana = PayProviders.TryNormalize(provider, out var rail) && PayProviders.IsSolana(rail);
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
            mine,
            provider,
            redirect_url = onPage ? null : liveUrl,
            solana_pay_url = onPage ? liveUrl : null,
            solana_cluster = isSolana ? SolanaCluster.FromConfig(config) : null,
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
        string? redirectUrl,
        IConfiguration config)
    {
        var emailRequired = PayProviders.TryNormalize(link.Provider, out var p) && PayProviders.RequiresEmail(p);
        var isSolana = PayProviders.TryNormalize(link.Provider, out var rail) && PayProviders.IsSolana(rail);
        return Results.Json(new
        {
            token = link.PublicToken,
            amount = link.Amount,
            currency = link.Currency,
            status,
            email_required = emailRequired,
            started,
            mine = false,
            provider = link.Provider,
            redirect_url = redirectUrl,
            solana_pay_url = (string?)null,
            solana_cluster = isSolana ? SolanaCluster.FromConfig(config) : null,
            remaining,
            max_payers = link.MaxPayers,
            paid_count = paid,
            taken_count = taken
        }, OneClient.Json);
    }

    static IResult StartedPay(string url) =>
        PayProviders.IsOnPageUrl(url)
            ? Results.Json(new { solana_pay_url = url }, OneClient.Json)
            : Results.Json(new { redirect_url = url }, OneClient.Json);

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

public sealed class ConfirmPayRequest
{
    public string? Signature { get; set; }
}
