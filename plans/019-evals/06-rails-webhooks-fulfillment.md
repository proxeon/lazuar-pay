# 06 — Rails, webhooks, same-handler fulfillment

**Date:** 26 August 2026  
**Type:** Uncondensed evaluation. Not an implementation.  
**Branch:** `feat/018-merchant-shell`  
**HEAD:** `9f04ad58` — `fix(pay-ui): match receipts table to pay-link chrome`  
**Authority:** live files under `apps/lazuar-pay/`. Papers in `plans/016-adapters-check/` are historical: they still say `Gateways/` and five names. Folders moved to `Rails/`. A sixth name (`test`) is on the allow-list. Do not treat 016 ticks as proof.

This paper answers: after 017/018, do the six hosted rails on the new host actually verify, join, and book money through one handler? Where is it still wrong, and how to fix it **without** a factory, `IEnumerable<IHostedRail>`, MediatR, outbox, registrar, or DNS folklore.

---

## Coordinates

| Item | Value |
|------|--------|
| Host | `apps/lazuar-pay` on **8081** |
| Dispatch | Concrete `AddScoped<*Hosted>()` + `switch` on `PayProviders.*` at Start and Handle |
| Interface | `IHostedRail` = `Provider` + `CreateHostedUrlAsync` only. Parse is `internal static` next to each folder |
| Plane B | `POST /v1/webhooks/{provider}/{orgId}` → verify → unique `(org, provider, eventId)` → `Fulfillment.FulfillPaidAsync` in one EF transaction |
| Plane A (asked as W10–W24) | `POST /v1/one/webhooks` HMAC + `ChargesPaused` |
| Names | `stripe`, `chip`, `billplz`, `xendit`, `razorpay`, `test` |
| Capability | always `"hosted_link"` |
| Money document | `Title = "Official Receipt"`, number `RCPT-{MalaysiaYear}-{n:00000}` |
| Hub judgment library | `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/` — HTTP only |
| Out of scope here | merchant chrome depth, TypeSpec paper, occupancy algorithm depth |

016 leftovers that **this SHA already closed** (do not re-open as live bugs):

- One HMAC dialect was body-only uppercase hex. Live is `t={unix},v1={lowercase hex}` over `{unix}.{body}`, 300s skew, 401 on the old dialect. Tests exist.
- Fulfill ignored `ChargesPaused`. Live checks pause in Handle **before** the unique insert **and** again inside `Fulfillment`. Paused paid webhook is 409, event id not consumed, retry after unsuspend pays.
- Process `Pay:StripeWebhookSecret` was a non-Production fallback. Live is **Testing-only**.
- Start always minted a second processor session. Live returns stored `PspRedirectUrl` when present.
- Webhook ignored `checkout.Provider`. Live 400 `provider mismatch`.
- PUT flipped `org_settings.ActiveProvider`. Live leaves it unused; mint/start/webhook use the row’s `Provider`.
- Razorpay join was `notes.checkout_id` only. Live also joins `payload.payment_link.entity.id` to `ProviderSessionId`.
- Xendit token compare was not hash-first. Live SHA256-then-`FixedTimeEquals`.
- Billplz localhost fail-closed was untested. Live has a named test and no PSP HTTP.
- `_ => stripe` dead arm. Live `_ => throw` after `TryNormalize`.
- SST throw on fulfill. Gone. Isolation bans LHDN tokens.

016 leftovers that **remain**: InMemory is still not a transaction; A99.2 paid+replay+not-paid is still uneven; FillTests/F00 still missing dozens of named methods; mismatch 400 still does not consume the event id; Stripe still does not read `payment_status`; Test rail Plane B is unsigned.

---

## Files opened

### Pay host (live authority)

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/README.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/.env.example`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/docker-compose.pay.yml`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Program.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Lazuar.Pay.csproj`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/appsettings.json`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/appsettings.Development.json`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Properties/launchSettings.json`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/IHostedRail.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/HostedSession.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Stripe/StripeHosted.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Stripe/StripeWebhook.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Chip/ChipHosted.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Chip/ChipWebhook.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Billplz/BillplzHosted.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Billplz/BillplzWebhook.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Xendit/XenditHosted.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Xendit/XenditWebhook.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Razorpay/RazorpayHosted.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Razorpay/RazorpayWebhook.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestHosted.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestWebhook.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Webhooks/PspParseResult.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Money/MoneyMath.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Money/Queries/PaymentQueryEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/PublicPay/BuyerEmail.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/PublicPay/CheckoutUrls.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutStore.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CreateCheckoutRequest.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkOccupancy.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Catalog/CatalogEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260821152601_Initial.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260824120000_FourAdaptersHostedRails.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Secrets/SecretBox.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Hosting/PayErrors.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookSignature.cs`

### Pay tests

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/FulfillmentProbe.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayTest.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/FakePspHandler.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Stripe/StripeRailTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Chip/ChipRailTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Billplz/BillplzRailTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Xendit/XenditRailTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Razorpay/RazorpayRailTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Test/TestRailTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Webhooks/WebhookTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Webhooks/FillTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Money/PaymentQueryTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPay/PublicPayTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Credentials/GatewayTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OneWebhookTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Secrets/SecretBoxTests.cs`

### Hub adapters (HTTP judgment only; not copied)

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzPublicBase.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/XenditGatewayAdapter.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/GatewayCommon.cs`

### Honesty surfaces (receipt / success URL ≠ paid; not a chrome paper)

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/pages/org/ReceiptsPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/lib/processors.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-checkout/src/App.tsx`

### Historical 016 (not authority)

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/016-adapters-check/00-evaluation.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/016-adapters-check/checklist/f00-fill-index.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/016-adapters-check/checklist/w10-steal-one-signer-judgment.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/016-adapters-check/checklist/w22-paused-does-not-consume-paid-id.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/016-adapters-check/checklist/y10-path-matches-checkout-provider.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/016-adapters-check/checklist/g10-inmemory-is-not-tx-proof.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/016-adapters-check/checklist/e10-stripe-fallback-testing-only.md`

---

## What exists (IHostedRail, six folders, webhook pipeline, fulfill TX, Official Receipt)

### Shape

`IHostedRail` is still one verb. Parse is not on the interface. A sixth rail is a folder plus two switch arms. That is the anti-factory. IsolationTests still fail `IEnumerable<IHostedRail>` and `namespace Lazuar.Pay.Gateways`.

```5:10:apps/lazuar-pay/src/Lazuar.Pay/Rails/IHostedRail.cs
public interface IHostedRail
{
    string Provider { get; }

    Task<HostedSession> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct);
}
```

```1:3:apps/lazuar-pay/src/Lazuar.Pay/Rails/HostedSession.cs
namespace Lazuar.Pay.Rails;

public readonly record struct HostedSession(string RedirectUrl, string? ProviderSessionId);
```

`Program.cs` registers **six concretes as themselves**, plus `Fulfillment` as `IFulfillPaid`. There is no `AddScoped<IHostedRail, T>`.

```39:46:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
builder.Services.AddScoped<StripeHosted>();
builder.Services.AddScoped<ChipHosted>();
builder.Services.AddScoped<BillplzHosted>();
builder.Services.AddScoped<XenditHosted>();
builder.Services.AddScoped<RazorpayHosted>();
builder.Services.AddScoped<TestHosted>();
builder.Services.AddScoped<Fulfillment>();
builder.Services.AddScoped<IFulfillPaid>(sp => sp.GetRequiredService<Fulfillment>());
```

HttpClient names exist for the four JSON rails (`chip`, `billplz`, `xendit`, `razorpay`). Stripe uses Stripe.net. Test uses no HTTP. csproj references Stripe.net only.

### Allow-list

```5:40:apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs
public static class PayProviders
{
    public const string Stripe = "stripe";
    public const string Chip = "chip";
    public const string Billplz = "billplz";
    public const string Xendit = "xendit";
    public const string Razorpay = "razorpay";
    public const string Test = "test";

    public const string Capability = "hosted_link";

    public static readonly string[] All = [Stripe, Chip, Billplz, Xendit, Razorpay];

    public static IReadOnlyList<string> Listed(IHostEnvironment env) =>
        AllowsTest(env) ? [..All, Test] : All;

    public static bool AllowsTest(IHostEnvironment env) =>
        !env.IsProduction();

    public static bool IsTest(string provider) => provider == Test;

    public static bool TryNormalize(string? raw, out string provider)
    {
        provider = (raw ?? "").Trim().ToLowerInvariant();
        return provider is Stripe or Chip or Billplz or Xendit or Razorpay or Test;
    }

    public static bool RequiresPublicMerchantId(string provider) =>
        provider is Chip or Billplz;

    public static bool RequiresEmail(string provider) =>
        provider is not Stripe and not Test;
```

Facts:

- `TryNormalize` accepts **six** lowercase names, including `test`, even in Production. Production refusal is a later `AllowsTest` / `IsTest` branch, not the allow-list.
- `All` is five names. `Listed` adds `test` when `!IsProduction()`. Gateway list tests in Testing expect **six** processors (`GatewayTests.List_returns_all_five_and_put_does_not_default_pay_links` still names “five” and asserts length 6).
- `ActiveProvider` is unused on the pay path (`Rows.cs` says so). Vault PUT does not pick a charge rail. Checkout/payment-link mint stores `Provider` on the row. Start and Handle read that column, not org settings.

### Start dispatch

`POST /v1/pay/{token}/start` injects six concretes and switches. Unknown after `TryNormalize` is **503** `rail not configured`, not 400. The `_ => throw` arm is dead given `TryNormalize` already returned a known name.

```140:166:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
        var provider = row.Provider ?? link?.Provider;
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
            PayProviders.Test => test,
            _ => throw new InvalidOperationException("rail not configured")
        };
```

Start is **idempotent on stored URL** (016 P0-A closed for the retry-after-redirect case). It is **not** idempotent if PSP HTTP succeeded and `SaveChanges` failed: the comment at 170–171 still admits a second processor session. Stripe has an Idempotency-Key belt (`lazuar-checkout:{id}`). CHIP / Billplz / Xendit / Razorpay do not.

Test start **fulfills in-process** (no Plane B). That is the local cashier. It writes a `psp_webhook_events` row with `EventId = hosted.ProviderSessionId ?? "test:" + row.Id` then calls the same `IFulfillPaid`.

```176:186:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
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
```

### Webhook pipeline (Plane B)

One route, one switch, six parsers.

```21:78:apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs
    public static void MapWebhooks(this WebApplication app)
    {
        app.MapPost("/v1/webhooks/{provider}/{orgId}", Handle);
    }
    // ...
        if (!PayProviders.TryNormalize(provider, out var name))
        {
            return PayErrors.Status(400, "Bad Request", "unknown provider");
        }
    // empty body → 400
    // test + !AllowsTest → 400 "rail not configured"
    // else load GatewayCredentials (orgId, name) or 400 "rail not configured"
            parsed = name switch
            {
                PayProviders.Stripe => StripeWebhook.Parse(raw, request.Headers, cred!, box, config, env),
                PayProviders.Chip => ChipWebhook.Parse(raw, request.Headers, cred!, box),
                PayProviders.Billplz => BillplzWebhook.Parse(raw, request.Query, cred!, box),
                PayProviders.Xendit => XenditWebhook.Parse(raw, request.Headers, cred!, box),
                PayProviders.Razorpay => RazorpayWebhook.Parse(raw, request.Headers, cred!, box),
                PayProviders.Test => TestWebhook.Parse(raw),
                _ => throw new InvalidOperationException("unknown provider")
            };
```

Order after parse (this is the money state machine):

1. `FindAsync([orgId, name, EventId])` → 200 `{ duplicate: true }` (no fulfill).
2. `parsed.Ignored` → insert event (swallow unique), 200 `{ ignored }`.
3. Resolve checkout: `CheckoutId`, else `HostedSessionId` → `checkouts.ProviderSessionId` (org + provider scoped).
4. Missing / wrong org → 400 `checkout not found`. **Event not inserted.**
5. `checkout.Provider` blank or ≠ path name → 400 `provider mismatch`. **Event not inserted.** Bound to **checkout.Provider**, not `OrgSettings.ActiveProvider`.
6. `ChargesPaused` → 409. **Event not inserted.**
7. Currency present and ≠ checkout → 400. **Event not inserted.**
8. `AmountMinor` present and ≠ `MoneyMath.ToMinor(checkout.Amount)` → 400. **Event not inserted.**
9. `BeginTransaction` → insert event row → `FulfillPaidAsync` → `Commit`. `DbUpdateException` → 200 duplicate. `ChargesPausedException` → rollback 409. other `InvalidOperationException` → rollback 500.

`PspParseResult` is the shared grain: `EventId`, `Ignored`/`IgnoreReason`, `CheckoutId`, `HostedSessionId`, `ProviderRef`, `AmountMinor`, `Currency`. Amounts on the webhook side are **minor units** except Xendit, which parses major and `ToMinor`s.

**Occupancy:** `Handle` never reads `PaymentLinkId` / `MaxPayers` / `PaymentLinkOccupancy`. Paying an already-minted open child is intended (the seat was taken at start). Over-capacity mint is a start-path race, not a webhook check. A forged or raced extra child that is `open` will still book if the PSP event verifies. Mentioned only; occupancy algorithm is out of scope.

### Fulfillment (same handler, Official Receipt)

One class for all six names. No SST. No tax invoice. No outbound `payment.completed`. No mail.

```11:130:apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs
public sealed class Fulfillment(PayDbContext db) : IFulfillPaid
{
    public async Task FulfillPaidAsync(string checkoutId, string provider, string? providerRef, CancellationToken ct)
    {
        var checkout = await db.Checkouts.FirstOrDefaultAsync(x => x.Id == checkoutId, ct);
        if (checkout is null) { return; }
        if (checkout.Amount <= 0) { return; }
        if (checkout.Status != "open") { return; }

        var settings = await db.OrgSettings.FindAsync([checkout.OrgId], ct);
        if (settings?.ChargesPaused == true)
        {
            throw new ChargesPausedException();
        }

        checkout.Status = "paid";
        db.Charges.Add(/* provider, providerRef, amount, currency, status paid */);
        // optional PayerRow from PayerEmail/PayerName
        // SubscriptionRow only if Interval is "mo" or "yr" — mint always writes "one_off"
        // JournalEntry + cash D + revenue C
        // DocumentSequence RCPT Malaysia year, LastN++
        // Document Title = "Official Receipt"
        // AuditEvent checkout.paid
        await db.SaveChangesAsync(ct);
    }
}
```

Silent `return` when not `open` is the in-handler idempotency for a **second event id** against an already-paid checkout: HTTP 200 `{ ok: true }`, extra `psp_webhook_events` row, no second journal. That is not the same as `{ duplicate: true }` for the same event id.

There is **no unique index** on `charges.CheckoutId` or `documents.Number` (Initial migration PK-only). Two concurrent paid grains for one checkout can both observe `status == open` and double-book. EF InMemory will not prove that.

`MailOutbox` exists as a table and is never written on fulfill.

### Official Receipt vs tax invoice

Host:

- `DocumentRow.Title = "Official Receipt"` (`Fulfillment.cs:118`).
- Number `RCPT-{year}-{n:00000}` with Malaysia (`Asia/Kuala_Lumpur` / Windows `Singapore Standard Time`) year.
- `OrgSettings.SstRegistered` is documented unused and not read on the pay path.
- List receipts returns `title`, `number`, `amount`, `payer_name`, `status` issued/pending (`PaymentQueryEndpoints.cs:99–117`).
- **GET by id does not return amount/currency/payer** (`PaymentQueryEndpoints.cs:136–143`) — list vs detail honesty hole, not a tax-invoice lie.
- WebhookTests asserts title, `SstRegistered` null, and the old `"SST registration unknown"` string is gone.

Merchant copy (out of chrome scope, honesty only): `ReceiptsPage.tsx` subtitle **“Official Receipt RCPT-…. Never a Tax Invoice.”** Processor blurb for Stripe: **“Official Receipt, not an e-invoice.”** That matches the host.

### Success URL ≠ paid

`CheckoutUrls.Success` defaults to `{Pay:CheckoutBaseUrl}/c/{token}?status=verifying`. Checkout SPA treats `status=verifying` as **not paid** and polls `GET /v1/pay/{token}` (`App.tsx:43–45`, `99–115`, `242–257`: “The processor success URL is not paid.”).

Test rail is the exception: `TestHosted` **returns that success URL as the “hosted” redirect** and Start fulfills immediately, so the verifying screen usually sees `paid` on first GET. Real rails wait for Plane B.

Merchant mint may pass an explicit `success_url` without `verifying`. Then the SPA will not poll. Payment-link children always set verifying URLs in `MintOrResume`.

### Plane A (W10–W24) — not still wrong

016 P0-4 / W10–W24 on this SHA:

| W | Live | Test |
|---|------|------|
| W10–W13 signer | `OneWebhookSignature.TryVerify`: header `t=,v1=` over `{unix}.{body}`, lowercase hex, `FixedTimeEquals` | `OneWebhookTests` |
| W14 skew | 300s default | `Stale_timestamp_is_401` |
| W15 missing header | 401 | `Missing_signature_is_401` |
| W16 old body-only hex | 401 | `Body_only_uppercase_hex_is_401` |
| W17 org_id or tenant_id | both | two suspend tests |
| W18–W19 pause flag | `tenant.suspended` / `tenant.reactivated` | both |
| W20 missing secret | 503 | `Missing_secret_is_503` |
| W21 fulfill reads paused | `Fulfillment` throws `ChargesPausedException` | belt of W24 |
| W22 paid id not consumed | Handle pause **before** TX | `Paused_org_does_not_mint_receipt` asserts events 0, then unsuspend retry pays |
| W23 vector | HMAC tests above | yes |
| W24 paused webhook | 409, 0 docs, retry pays | `WebhookTests.Paused_org_does_not_mint_receipt` |

Header name is still `X-Lazuar-Signature` only (W10 said keep unless live One sends another). Missing delivery `id` still falls back to `Guid.NewGuid()` (`OneWebhookEndpoints.cs:38`), so an id-less body is not replay-stable. That is Plane A residual, not Plane B.

Billplz still has **two HMAC dialects** (with extra fields, then without). That is Hub steal, not the One signer. Do not collapse it to “one dialect.”

---

## Per-rail status table (create URL, verify, event-id, paid vs setup, email refuse, tests)

Legend: **steal** = Hub HTTP judgment kept; **invert** = Hub mapped this as paid and Pay must not; **gap** = Hub HTTP Pay still lacks; **ok** = intended Pay behaviour.

| | stripe | chip | billplz | xendit | razorpay | test |
|---|---|---|---|---|---|---|
| **Create URL** | Stripe.net Checkout Session `mode=payment`, `ClientReferenceId=checkout.Id`, metadata `checkout_id`/`org_id`, Idempotency-Key `lazuar-checkout:{id}`, line `Name="Pay"`, `UnitAmount=ToMinor` (always ×100). Returns `session.Url` + `session.Id`. | `POST https://gate.chip-in.asia/api/v1/purchases/` Bearer, `brand_id`, client email/name, `purchase.products[0].price` sen, metadata, success/failure/cancel redirects. Returns `checkout_url` + `id`. **No** `force_recurring`. | `POST https://www.billplz[-sandbox].com/api/v3/bills` Basic `{key}:`, collection_id, amount sen, `callback_url={Pay:PublicBaseUrl}/v1/webhooks/billplz/{orgId}?checkout_id=`, `reference_1=checkout.Id`, redirect = success (verifying), not callback. Public https fail-closed (localhost / `lazuar-local-dev.com` / non-https → 400 `callback base not public`). | `POST https://api.xendit.co/v2/invoices` Basic `{secret}:`, `external_id=checkout.Id`, amount **major** via `FromMinor(ToMinor)`, `payer_email`, metadata, success/failure redirects. Returns `invoice_url` + `id`. No channel allow-list. | `POST https://api.razorpay.com/v1/payment_links` Basic `key_id:key_secret` (`TrySplit`), amount minor, notes `checkout_id`/`org_id`, `callback_url` = success GET. Returns `short_url` + `id`. No e-mandate. | No HTTP. Returns `CheckoutUrls.Success` + `"test:"+checkout.Id`. Refuses when `!AllowsTest`. |
| **Verify** | Stripe.net `EventUtility.ValidateSignature` + `ConstructEvent` (`throwOnApiVersionMismatch: false`). `Stripe-Signature`. Secret: row `WebhookCiphertext`, else **Testing-only** `Pay:StripeWebhookSecret`, else 503. | RSA PKCS1 SHA256 over raw body, `X-Signature` base64, PEM from `WebhookCiphertext`. Missing PEM → 503. | Form `x_signature`, HMAC-SHA256 of `key+value` sorted `\|`, extra fields then without (`paid_at`, `transaction_id`, `transaction_status`). Fixed-time hex. | `x-callback-token` vs Unprotect, **hash-first SHA256** then FixedTimeEquals (Hub 073). | HMAC-SHA256 raw body vs `X-Razorpay-Signature`, lowercase hex FixedTimeEquals. No `Razorpay.Api`. | **None.** `JsonDocument.Parse` only. `PspVerifyException` on bad JSON. |
| **Event-id grain** | Stripe `evt_` (`stripeEvent.Id`) | `paid:{purchaseId}` / `preauth:` / `failed:` / `{eventType}:` | `paid:{billId}` / `unpaid:{billId}` | `paid:{invoiceId}` / `settled:` / `{status}:` | Header `X-Razorpay-Event-Id` else `captured:{pay_}` / `failed:` / `{event}:` | Body `id` else **`test:` + new Guid** (unstable) |
| **Paid** | `checkout.session.completed` **and not** (`mode==setup` or `AmountTotal` null/0). Does **not** read `payment_status`. Does **not** handle `checkout.session.async_payment_succeeded`. Ignores `payment_intent.succeeded` (Hub maps it; Pay invert is safer against double grain). | `event_type == purchase.paid` only | `paid=true` or `state=paid` | `PAID` or `invoice.paid` | `payment.captured` only (`payment_link.paid` / `order.paid` ignored — Hub same) | Any JSON with optional amount/currency. Start also pays without webhook. |
| **Not paid / ignore** | setup / zero → `setup_or_zero`; other types ignored | `purchase.preauthorized` **invert Hub** (Hub `PAYMENT_COMPLETED` if token present); `purchase.payment_failure`; other types | unpaid form (Hub mapped `PAYMENT_FAILED` and still invented MYR) | **SETTLED invert Hub** (Hub `PAYMENT_COMPLETED`); EXPIRED/PENDING/other ignored | `payment.failed`; non-captured | **No not-paid path** |
| **Join checkout** | `client_reference_id` or metadata `checkout_id`. `HostedSessionId` unset. | purchase.metadata `checkout_id` only. `HostedSessionId` **unset** so Handle’s session join never runs. | query `checkout_id`, else form, else `reference_1` | metadata `checkout_id` else `external_id` | notes `checkout_id` **or** `payment_link.entity.id` → `HostedSessionId` | body `checkout_id` |
| **Amount units** | `AmountTotal` already minor; comment “Do not ToMinor again” | `purchase.total` sen; `(long)total` | form `paid_amount` sen | `paid_amount` else `amount` **major** then `ToMinor` | entity `amount` already minor; JSON `tax`/`fee` unread | optional `amount_total` minor; **omission skips mismatch** |
| **Currency** | refuse missing (`PspVerifyException`) | refuse missing | refuse missing (**not** Hub’s hardcoded MYR) | refuse missing | refuse missing | optional; omission skips mismatch |
| **Email refuse** | not required | required; placeholder `customer@example.com` refused (`BuyerEmail`) | same | same | same | not required |
| **Public merchant id** | refused on PUT | Brand ID required | Collection ID required; environment required (no silent default for Billplz) | n/a | n/a; secret must be `key_id:key_secret` | PUT 400 “does not take secrets” |
| **Tests paid** | `Completed_session_writes_receipt_and_replay_is_noop` | `Chip_start_and_paid_webhook` | `Billplz_paid_form_and_localhost_blocked` | `Xendit_paid_and_settled` | `Razorpay_captured` + `Razorpay_captured_without_notes_joins_plink` | `Mint_and_start_pays_without_keys` + `Webhook_pays_open_test_checkout` |
| **Tests replay** | same Stripe paid test | same CHIP paid test | same Billplz paid test | **no dedicated paid replay** (settled is a different id) | **missing** | **missing** |
| **Tests not-paid** | setup + zero-amount | preauthorized | unpaid | SETTLED (not EXPIRED/PENDING) | `payment.failed` | **missing** |
| **Hermetic create** | **untested** (Stripe.net, not FakePsp) | FakePsp `checkout_url` | FakePsp + sandbox host assert | FakePsp `invoice_url` | FakePsp `short_url` | no HTTP |

Hub steal/refuse one-liners (do not copy the type graph):

- Stripe: steal Session create + `EventUtility` + `evt_` grain + setup-not-paid invert. Do not copy `setupFutureUsage`, Connect fee, Billing SoT, PI expand for fees, `PAYMENT_COMPLETED` for setup. **Still missing** Hub `PaymentMethodTypes=["card"]`, `CustomerEmail`, `PaymentIntentData.Metadata`, `payment_status==paid`, zero-decimal table.
- CHIP: steal purchases HTTP + RSA PEM. **Invert** preauthorized-as-paid. Do not copy `ChipWebhookRegistrar`, `force_recurring`, off-session.
- Billplz: steal JSON bills + dual HMAC + public-https callback predicate. **Do not** copy `PublicDnsFallback`, `AllowInsecureBillplzCallback`, hardcoded MYR, unpaid-as-failed-that-still-looks-like-a-payment-result into fulfill. Pay unpaid is ignored (better).
- Xendit: steal `/v2/invoices` + callback token hash-first. **Invert SETTLED-as-paid.** Do not copy channel allow-lists, xenPlatform, refund payload.
- Razorpay: steal payment_links HTTP + HMAC + captured-only + header event id. Do not copy `Razorpay.Api`, `ChargeOffSession`, fee/tax booking. Pay’s plink join is **ahead** of Hub (Hub notes-only).

---

## Bugs

A **bug** here is live code that books money wrongly, drops a paid event forever, or accepts a spoof in an environment merchants will tunnel. Gaps that are missing tests or missing belts are in the next section even when they are dangerous.

### B1 — Test Plane B is unsigned in every non-Production environment

`TestWebhook.Parse` never verifies a signature. Handle skips credentials when `IsTest`. `AllowsTest` is `!env.IsProduction()`, so **Development (launchSettings), Testing, and Staging** all accept `POST /v1/webhooks/test/{orgId}` with a JSON body.

```9:57:apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestWebhook.cs
    public static PspParseResult Parse(string json)
    {
        // JsonDocument.Parse … id / checkout_id / optional amount_total / optional currency
            return new PspParseResult
            {
                EventId = eventId,
                CheckoutId = checkoutId,
                ProviderRef = eventId,
                AmountMinor = amountMinor,
                Currency = currency
            };
```

```50:55:apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs
        if (PayProviders.IsTest(name))
        {
            if (!PayProviders.AllowsTest(env))
            {
                return PayErrors.Status(400, "Bad Request", "rail not configured");
            }
        }
```

```21:22:apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs
    public static bool AllowsTest(IHostEnvironment env) =>
        !env.IsProduction();
```

Production compose sets `ASPNETCORE_ENVIRONMENT=Production` (`deploy/prod/docker-compose.yml`). Local `launchSettings` is **Development**, so a Cloudflare tunnel to 8081 (the same tunnel Billplz needs) makes Test webhooks world-writable. `TestRailTests.Webhook_pays_open_test_checkout` posts unsigned JSON and expects a receipt.

**Solve:** Treat Test as Testing-or-Development only (`env.IsEnvironment("Testing") || env.IsDevelopment()`), **or** delete the Test webhook route and keep start-to-pay as the only Test money door (Start already fulfills). If a webhook must exist, HMAC the body with `Pay:WrapKey` / a Testing secret and fail closed when missing. Do not invent a factory. Do not register Test as `IHostedRail` in Production DI — the switch arm can stay; `TestHosted.CreateHostedUrlAsync` already throws `rail not configured` when `!AllowsTest`.

### B2 — Test webhook omits amount and currency and still pays

Handle only mismatch-checks when the parser **set** the fields:

```132:141:apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs
        if (parsed.Currency is not null
            && !string.Equals(parsed.Currency, checkout.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return PayErrors.Status(400, "Bad Request", "currency mismatch");
        }

        if (parsed.AmountMinor is not null && parsed.AmountMinor.Value != MoneyMath.ToMinor(checkout.Amount))
        {
            return PayErrors.Status(400, "Bad Request", "amount mismatch");
        }
```

Test parser leaves both null when absent. Payload `{"id":"x","checkout_id":"<open test checkout>"}` books RM 10 (or whatever the row says). Combined with B1 this is unauthenticated arbitrary fulfill of any `provider=test` open checkout whose id leaked.

**Solve:** In `TestWebhook.Parse`, require `id`, `checkout_id`, `amount_total`, `currency`; throw `PspVerifyException` when missing. Same as Stripe/CHIP fail-closed currency. Keep Handle’s null-skip for parsers that truly have no amount (none of the real five should).

### B3 — Test webhook EventId is a new Guid when `id` is missing

```27:30:apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestWebhook.cs
            if (string.IsNullOrWhiteSpace(eventId))
            {
                eventId = "test:" + Guid.NewGuid().ToString("N");
            }
```

Every retry is a new grain. After first pay, Fulfillment no-ops because status ≠ open, but each retry inserts another event and returns `{ ok: true }`. Duplicate detection never fires.

**Solve:** Missing `id` → `PspVerifyException("missing event id")`, same as Razorpay failed-without-id. Do not mint a Guid.

### B4 — Stripe `checkout.session.completed` is treated as paid without `payment_status`

Live ignore is only `mode == "setup"` or `AmountTotal` null/0:

```46:75:apps/lazuar-pay/src/Lazuar.Pay/Rails/Stripe/StripeWebhook.cs
        if (stripeEvent.Type is not "checkout.session.completed")
        {
            return new PspParseResult { EventId = stripeEvent.Id, Ignored = true, IgnoreReason = stripeEvent.Type };
        }
        // ...
        if (session.Mode == "setup" || session.AmountTotal is null or 0)
        {
            return new PspParseResult { EventId = stripeEvent.Id, Ignored = true, IgnoreReason = "setup_or_zero" };
        }
```

Stripe can emit `checkout.session.completed` with `payment_status=unpaid` for delayed methods, then `checkout.session.async_payment_succeeded`. Pay would **fulfill the unpaid completed** (amount still matches) and **ignore** the later succeeded (type ≠ `checkout.session.completed`). Tests inject `"payment_status":"paid"` in fixtures but the parser never reads it. Hub also skipped `payment_status`; 016 listed it as steal-next. It is still next.

**Solve:** Ignore unless `session.PaymentStatus` is `paid` (or `no_payment_required` if you ever charge zero, which you already ignore). Add a second **paid** type arm: `checkout.session.async_payment_succeeded` with the same amount/currency/client_reference_id rules. Do **not** add `payment_intent.succeeded` (second grain for the same Checkout Session; Hub’s cathedral). Event id stays Stripe `evt_`.

### B5 — Two concurrent paid grains can double-book one checkout

Fulfillment’s only seat lock is `if (checkout.Status != "open") return;` in memory, then `SaveChanges`. `charges` PK is `Id` only (`Initial.cs:33–49`). `documents` PK is `Id` only. `document_sequences` PK is `(OrgId, Series, YearMyt)` with `LastN` mutated in place — two concurrent fulfills can mint the same `RCPT-…` number.

Webhook unique key is `(OrgId, Provider, EventId)`. That stops **the same** PSP delivery, not two different event ids (two CHIP purchases after a SaveChanges-after-PSP miss, or Stripe completed + a future second type if B4 is “fixed” badly).

The TX around insert+fulfill (`WebhookEndpoints.cs:143–154`) does **not** `SELECT … FOR UPDATE` the checkout row.

**Solve without cathedral:**

1. Unique index `charges (CheckoutId)`.
2. Unique index `documents (OrgId, Number)` (and optionally `(CheckoutId)`).
3. In `FulfillPaidAsync`, `UPDATE checkouts SET status='paid' WHERE id=@id AND status='open'` and proceed only if 1 row; else return. Keep that inside the existing `BeginTransaction`. Catch unique on charges as already-paid (HTTP 200 ok/duplicate).
4. Sequence: `LastN = LastN + 1` in the same TX is enough **if** the checkout CAS holds; add a unique on number as belt.

Do not add an outbox or MediatR notification to “fix” this.

### B6 — Start after PSP success + SaveChanges failure still mints a second CHIP/Billplz/Xendit/Razorpay session

```170:171:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
            // PSP HTTP then persist. A SaveChanges failure after the processor
            // already created a session may mint a second session on retry.
```

Stored-URL short-circuit only helps when the first SaveChanges **worked**. Stripe Idempotency-Key is the belt for Stripe only (`StripeHosted.cs:50`).

**Solve:** For CHIP/Xendit/Razorpay/Billplz, send an idempotency header the PSP documents (Xendit `Idempotency-key`, CHIP if any, Razorpay none — then persist `ProviderSessionId` **before** considering start complete by writing a pending row in the **same** SaveChanges as the URL, or 409 on a second start when `ProviderSessionId` is set even if URL save raced). Minimum: if `ProviderSessionId` is non-null, return stored URL even when `PspRedirectUrl` is empty (and log). Do not call create again. No factory.

### B7 — CHIP paid join is metadata-only; Handle’s session join is dead for CHIP/Xendit/Billplz/Stripe

Handle already has:

```101:107:apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs
        string? checkoutId = parsed.CheckoutId;
        if (string.IsNullOrWhiteSpace(checkoutId) && !string.IsNullOrWhiteSpace(parsed.HostedSessionId))
        {
            var bySession = await db.Checkouts.FirstOrDefaultAsync(
                x => x.OrgId == orgId && x.Provider == name && x.ProviderSessionId == parsed.HostedSessionId, ct);
            checkoutId = bySession?.Id;
        }
```

Only Razorpay sets `HostedSessionId`. CHIP paid with stripped metadata → 400 `checkout not found`, **event not inserted**, CHIP retries forever, buyer paid, no `RCPT-`. Same class as 016 P0-C, now on CHIP.

**Solve:** Set `HostedSessionId = purchaseId` on CHIP paid (and `ProviderRef` already is). Same one-liner for Xendit `invoiceId`, Billplz `billId`, Stripe `session.Id`. Parsers stay static. No interface method.

### B8 — Amount/currency mismatch 400 does not consume the event (lost cash if **we** are wrong)

Fail-closed against a hostile payload is correct. If our unit map is wrong, Plane B never inserts, PSP retries until they give up, buyer paid, no receipt. 016 P0-D. Live units are pinned in comments and **Stripe** has mismatch tests. CHIP/Billplz/Xendit/Razorpay **do not** have amount-mismatch tests (`fc18`, `fb22`, `fx20`, `fr22` still missing). A CHIP `total` that was actually major (10 not 1000) would 400 forever.

**Solve:** Keep 400 + no insert (do not “fix” by inserting a poison event). Add one mismatch fixture per name (F00). Add a lived JSON comment or FakePsp body assertion that CHIP total is 1000 for RM10, Xendit `paid_amount` is 10, Billplz `paid_amount` is 1000, Razorpay 1000, Stripe `amount_total` 1000. Do not divide CHIP by 100 because Hub did.

### B9 — `.env.example` still advertises a Dev process `whsec_` fallback

Live `StripeWebhook.ResolveSecret`:

```78:91:apps/lazuar-pay/src/Lazuar.Pay/Rails/Stripe/StripeWebhook.cs
        if (!string.IsNullOrWhiteSpace(cred.WebhookCiphertext))
        {
            return box.Unprotect(cred.WebhookCiphertext);
        }

        if (env.IsEnvironment("Testing"))
        {
            return config["Pay:StripeWebhookSecret"];
        }

        return null;
```

`.env.example` line 11–12: “Dev fallback only; Production uses per-org webhook_secret.” README is honest (“Testing-only”). Development with a NULL `WebhookCiphertext` is 503, not process env. E10 is **coded**. The example file lies.

**Solve:** Comment: Testing-only; PUT `webhook_secret` is required for every real rail. Do not read process env in Development.

### B10 — `ChargesPausedException` is an `InvalidOperationException`; catch order is correct today but brittle

Handle catches `ChargesPausedException` before `InvalidOperationException`. If someone reorders catches, pause becomes HTTP 500 `fulfill failed` and the event rolls back (still not consumed — money-safe, Stripe retries, worse ops). Not a live bug. Do not “fix” by making pause a 200.

---

## Gaps

Missing behaviour or missing proof. Not always a wrong book.

### G1 — Unknown provider: webhook 400, start 503, create 400

Task asked: switch on `PayProviders` in Start and Handle — unknown 400; is Test included?

- Handle unknown (`paypal`) → 400 `unknown provider`. Tested (`Unknown_provider_is_400`).
- Start unknown / missing provider → **503** `rail not configured` (`PublicPayEndpoints.cs:141–144`, `Start_without_rail_is_503`).
- Checkout create unknown → 400 (`CheckoutTests.Create_unknown_provider_is_400`).
- Test **is** included in `TryNormalize` and both switches. Production Test webhook → 400 `rail not configured` (AllowsTest). Production Test start → `TestHosted` throws → 503.

**Solve:** If you want HTTP honesty, map Start’s failed `TryNormalize` to 400 `unknown provider` and keep 503 for “known name, no keys / Production test”. Two strings, one switch. No factory.

### G2 — Webhook is bound to `checkout.Provider`, not org active provider (correct after 018; tests incomplete)

Y10 is implemented (`provider mismatch`, `Never_started_checkout_webhook_is_400` nulls `Provider`). `ActiveProvider` stays null after PUT (`GatewayTests`). There is **no** Y12 cross-rail test (CHIP-started checkout posted to `/v1/webhooks/stripe/{org}`).

**Solve:** One test: mint+start CHIP, POST a valid Stripe-shaped event with that checkout id to `/v1/webhooks/stripe/t1` (t1 has both keys) → 400 provider mismatch, 0 docs. Keep leftover credential rows (Y10 must-not).

### G3 — One DB transaction is coded, not proven on InMemory

```27:30:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs
    /// InMemory BeginTransaction is a no-op. H25/G12 proof uses FulfillmentProbe,
    /// which throws before Fulfillment.SaveChanges so the event row is not committed.
```

```53:54:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs
            services.AddDbContext<PayDbContext>(o => o.UseInMemoryDatabase(_dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
```

`FillTests.Fulfill_throw_returns_5xx_event_not_committed_retry_pays` is the G12/G13 seam: throw before inner `SaveChanges`, event count 0, retry pays. That is **not** proof that Npgsql rolls back a unique-constraint race, and not proof that `Fulfillment.SaveChanges` + later `Commit` is one snapshot. G10 remains true as a sentence: do not sell `task pay:test` as transaction proof.

**Solve:** Keep the probe test. Optionally one Npgsql-backed test behind a flag (not hermetic `pay:test`). Do not switch the suite to Postgres as a default. Do not remove `BeginTransaction`.

### G4 — FillTests / 015-lied cases: **still partly true**

`FillTests.cs` exists (016 F00 was “ticks with no method”). It covers fulfill-throw, Stripe amount mismatch, Stripe currency mismatch, rail-not-configured-with-body, never-started, empty body. That **closes** fs11/fs12/fs14/fs16/y11 and G12/G15 for **Stripe**.

F00 methods that **still have no `[Test]`** (names from `plans/016-adapters-check/checklist/f00-fill-index.md`):

| ID | Method | Status |
|----|--------|--------|
| fs13 | `Unknown_event_type_is_ignored` | missing (setup/zero are different types) |
| fs17 | `Stripe_whitespace_webhook_is_400` | missing (`""` is tested; `"  "` is Chip/Billplz/Xendit/Razorpay) |
| fs18 | Stripe missing currency | missing (code throws) |
| fs15 / e12 | Production empty ciphertext 503 | missing (Testing empty is 503 via process `""`) |
| fc10–fc12, fc13–fc15, fc17–fc18 | CHIP bad/missing sig, missing currency, payment_failure, failure-then-paid, cross-org, start without brand, amount mismatch | **all missing** except placeholder/email/empty/preauth/paid |
| fb11–fb13, fb16, fb19–fb25 | Billplz bad HMAC, extra/without extra as dedicated tests, start without email, PUT environment, start without collection, cross-org, amount mismatch, join `reference_1`, live host, missing currency | **missing** (unpaid, localhost, placeholder, empty, paid+replay exist; PUT collection exists in GatewayTests) |
| fx11–fx16, fx18–fx20 | Xendit bad/missing token, expired, pending, paid replay, missing email, missing currency, cross-org, amount mismatch | **missing** (PAID+SETTLED, placeholder, empty exist) |
| fr11–fr12, fr14–fr17, fr20–fr22 | Razorpay bad/missing sig, failed-then-captured, replay, event-id header, missing email, missing currency, cross-org, amount mismatch | **missing** (captured, without notes, failed, placeholder, empty exist) |
| fg14 | `Put_chip_get_active_is_chip_not_stripe` | **obsolete** — PUT does not set ActiveProvider; do not write this test |

015-lied **SST** is **false** now (throw gone, tests assert Official Receipt). 015-lied **factory** is **false** (IsolationTests). 015-lied **A99.2 five names paid+replay+not-paid** is **still true** as a coverage claim (see next section; six names now).

**Solve:** Write the missing methods in the existing `*RailTests` / `FillTests` files. Do not create a `HostedRailFactoryTests`. Do not call live PSP from `task pay:test`.

### G5 — Stripe hosted create is untested in the hermetic suite

`StripeRailTests` is one method: missing `Stripe-Signature` → 400. Start never hits `SessionService.CreateAsync` because Stripe.net does not go through `IHttpClientFactory` / `FakePsp`. A broken Success URL or dropped `ClientReferenceId` would not fail CI.

**Solve:** Optional: wrap `SessionService` behind a one-method `IStripeSessions` in `Rails/Stripe/` (not `IHostedRail`, not a multi-rail factory) for Fake. Or accept Stripe create as a sandbox soak. Do not add Stripe Billing.

### G6 — Product line is always `"Pay"`; catalog name unused

CHIP/Billplz/Xendit/Razorpay/Stripe hosted payloads use `"Pay"` / description `"Pay"`. Hub used `ProductDescription`. Checkout can carry `ProductId`. Fulfillment does not need the name; the **hosted page** the buyer sees does.

**Solve:** If `ProductId` is set, look up `Products.Name` in the hosted class (four extra lines each) or pass it on `CheckoutRow` at mint. Still one `CreateHostedUrlAsync`. Do not add `IProductRail`.

### G7 — Always ×100 minor units (JPY footgun)

`MoneyMath.ToMinor` is `Round(amount * 100m, AwayFromZero)` with no zero-decimal table. Hub `GatewayCommon.ToMinorUnits` has ISO exceptions. Checkout create accepts any trimmed uppercase currency, not even length-3 (`CheckoutEndpoints.cs:75`). Catalog create refuses non-MYR. Payment links do not.

**Solve:** Refuse non-MYR on checkout/payment-link create **or** copy the zero-decimal set into `MoneyMath` (judgment, not the Hub class). Bar B copy already says MYR. Do not invent SST.

### G8 — Stripe create missing Hub HTTP belts

No `CustomerEmail` even when `PayerEmail` is set. No `PaymentMethodTypes = ["card"]`. No copy of metadata onto `PaymentIntentData`. Idempotency-Key **is** present. Email is optional on Stripe (correct vs CHIP).

**Solve:** Steal those three Session fields in `StripeHosted` only. Do not steal `setupFutureUsage` / setup mode.

### G9 — PUT does not validate CHIP PEM / Billplz X-Signature shape

Garbage `webhook_secret` stores. First webhook 400 `invalid signature`. Ops-only.

**Solve:** Optional PUT syntax check (PEM header, non-empty). Not a factory.

### G10 — Receipt GET is thinner than list

List has amount/payer; GET `{id}` has `id/org_id/number/title/checkout_id` only. Merchant table uses list. Fine until a detail page.

**Solve:** Same anonymous object as list. Keep title Official Receipt.

### G11 — Dead subscription branch

`Fulfillment` writes `SubscriptionRow` when `Interval is "mo" or "yr"`. `CheckoutEndpoints.Create` and `MintOrResume` always set `one_off`. Catalog prices can have other intervals; checkout mint does not copy them.

**Solve:** Leave it. Do not grow `IHostedRail` with billing. Do not copy Stripe `mode=subscription`.

### G12 — No receipt email / outbound payment.completed

`MailOutbox` unused. Kernel story in 018-evals is still ahead of the door. Out of this paper’s fix list except: do not hang `SendReceipt` on `IHostedRail`.

### G13 — Occupancy ignored on webhook (mention)

See pipeline. Start path uses `PaymentLinkOccupancy`. Webhook pays any open child. Do not add occupancy math to Handle in the same PR as B4/B5.

### G14 — Razorpay `payment_link.paid` ignored by design

```60:66:apps/lazuar-pay/src/Lazuar.Pay/Rails/Razorpay/RazorpayWebhook.cs
        if (eventType is "payment_link.paid" or "payment_link.expired" or "order.paid"
            || eventType != "payment.captured")
        {
            var otherId = headerEventId
                          ?? (string.IsNullOrWhiteSpace(paymentId) ? (eventType ?? "razorpay") + ":none" : eventType + ":" + paymentId);
            return new PspParseResult { EventId = otherId, Ignored = true, IgnoreReason = eventType };
        }
```

The `or eventType != "payment.captured"` makes the first three names dead documentation; behaviour equals Hub `if (eventType != "payment.captured")`. If a merchant dashboard only enables `payment_link.paid`, Pay never fulfills. Ops: enable `payment.captured`. Do not map both types as paid (double grain: header id vs `plink_`).

### G15 — Isolation from Hub Payments holds

IsolationTests ban `IPaymentGatewayAdapter`, `PaymentGatewayFactory`, `AddPaymentsModule`, `GatewayPaymentCompletedIntegrationEvent`, `Modules.Payments`, `ChipWebhookRegistrar`, `PublicDnsFallback`, `IEnumerable<IHostedRail>`, `namespace Lazuar.Pay.Gateways`, `Razorpay.Api`, Connect fee tokens, LHDN tokens. Host csproj has no `apps/lazuar-api` reference. Keep it.

---

## Tests vs missing (paid+replay+not-paid per name; InMemory TX)

Hermetic NUnit (`[Test]` count across the test project ≈ 123). Rails+webhooks+money slice:

### Exists

| Name | Paid | Replay same event | Not-paid / ignore | Extra that matters |
|------|------|-------------------|-------------------|--------------------|
| **stripe** | `Completed_session_writes_receipt_and_replay_is_noop` → `RCPT-`, title Official Receipt, journal D=C, SST string absent | same method `{ duplicate }` | `Setup_mode_is_ignored`, `Zero_amount_session_is_ignored` | cross-org 400; pause 409 + retry; fulfill throw 5xx no event; amount/currency mismatch; missing sig; missing secret 503; empty body; never-started; unknown provider |
| **chip** | `Chip_start_and_paid_webhook` start+paid+`RCPT-`+no `force_recurring` | same method | `Chip_preauthorized_is_ignored` | missing email; placeholder; empty body |
| **billplz** | `Billplz_paid_form_and_localhost_blocked` | same method | `Billplz_unpaid_is_ignored` | localhost start 400 no HTTP; placeholder; empty body |
| **xendit** | `Xendit_paid_and_settled` PAID → receipt | **no** second PAID | SETTLED ignored | placeholder; empty body |
| **razorpay** | `Razorpay_captured` + journal D=C; `Razorpay_captured_without_notes_joins_plink` | **no** | `Razorpay_payment_failed_is_ignored` | placeholder; empty body |
| **test** | start-to-pay + unsigned webhook pay | **no** | **no** | `PaymentQueryTests` uses test start for list payments/receipts |

InMemory TX: **not proven**. Probe throw **is** proven (`FillTests.Fulfill_throw_returns_5xx_event_not_committed_retry_pays`).

Public start twice one PSP HTTP: `PublicPayTests.Start_twice_returns_same_url_without_second_psp_http` (CHIP). Pause on start 403: `Start_paused_is_403_even_with_stored_url`. Email required flags: CHIP true, Stripe false.

One HMAC: 8 methods in `OneWebhookTests` (W10–W24 closed as tests).

### Missing (must exist before calling six rails “production BYOK”)

Per name, still needed:

1. **xendit** — POST the **same** PAID body twice → `{ duplicate }`, still one `RCPT-`. Today SETTLED is a different `EventId` (`settled:` vs `paid:`).
2. **razorpay** — replay captured; prefer `X-Razorpay-Event-Id`; failed-then-captured still one receipt.
3. **test** — replay; missing amount 400; Production (or Staging) webhook 400; signed-or-disabled.
4. **All five real rails** — amount mismatch 400, 0 events, checkout still `open` (only Stripe today).
5. **chip / billplz / xendit / razorpay** — bad signature 400; missing signature/token 400; missing currency 400; cross-org 400.
6. **chip** — `purchase.payment_failure` ignored then `purchase.paid` still mints one receipt.
7. **xendit** — EXPIRED / PENDING ignored (code has a generic non-PAID ignore; untested).
8. **billplz** — HMAC without extra fields; join via `reference_1` when query missing; live host `www.billplz.com`; PUT environment required (code exists, no named test).
9. **stripe** — unknown event type ignored; missing currency; `payment_status=unpaid` must **not** pay (once B4 is coded); whitespace body.
10. **Production** Stripe empty ciphertext 503 **without** Testing fallback (host `UseEnvironment("Production")` in one factory).

Do not implement Hub factory tests, registrar tests, DNS fallback tests, SST math tests, e-mandate tests.

---

## Ranked findings

Cash blast radius first. Closed 016 items are **not** re-ranked as open.

| Rank | ID | Finding | Class |
|------|----|---------|--------|
| 1 | B1+B2 | Test webhook is unsigned **and** amount-optional in Development/Staging. Tunnel to 8081 = forge `RCPT-`. Production is closed if env name is really Production. | **P0 bug** |
| 2 | B4 | Stripe completed-without-`payment_status`; async succeeded ignored. Delayed methods can book unpaid or never book the paid follow-up. | **P0 bug** (method-mix dependent) |
| 3 | B5 | No unique charge-per-checkout; in-memory `status==open` is not a lock. Double `RCPT-` under concurrency / two grains. | **P0 bug** (race) |
| 4 | B6 | SaveChanges-after-PSP second hosted session on CHIP/Billplz/Xendit/Razorpay. Two processor charges, one or two receipts depending on B5. | **P0 residual** of 016 P0-A |
| 5 | B7+B8 | Weak join (CHIP metadata-only) + mismatch 400 without unique insert. Buyer paid, no receipt, retries forever. | **P0** if join/units wrong; **P1** if metadata always echoes |
| 6 | G4 | F00 still mostly unwritten. Parsers can rot without CI. 015-lied **coverage** claim still true. | **P1 tests** |
| 7 | G3 | InMemory TX still a no-op. Probe is the only seam. | **P1 honesty** |
| 8 | B3 | Test EventId Guid. Replay never duplicates. | **P1** (with B1) |
| 9 | G1 | Start unknown is 503 not 400. Test is on the switch. | **P2 HTTP** |
| 10 | G6 G7 G8 | `"Pay"` line, ×100, Stripe Session belts. | **P2 steal-next** |
| 11 | G10 G11 G12 | Sparse GET receipt, dead subscriptions, no mail/outbound event. | **P2 product** |
| 12 | Occupancy | Webhook ignores cap. Intended for minted seats. | mention |
| — | W10–W24 | One HMAC + pause-on-fulfill **not still wrong**. | closed |
| — | E10 | Stripe process `whsec_` Testing-only **not still wrong** in code; `.env.example` stale (B9). | closed + doc lie |
| — | Y10 | Bound to `checkout.Provider`. ActiveProvider unused. | closed |
| — | Receipt honesty | Title Official Receipt; merchant subtitle matches; not a tax invoice. GET detail thin (G10). | mostly honest |

---

## How to solve without the cathedral

Keep: six folders, two switch arms, static `*Webhook.Parse`, `IHostedRail` = create URL, `WebhookEndpoints` = one pipeline, `Fulfillment` = one book, IsolationTests bans.

Do **not** add `IEnumerable<IHostedRail>`, keyed DI, `PaymentGatewayFactory`, MediatR, outbox, `ChipWebhookRegistrar`, `PublicDnsFallback`, Hub `IPaymentGatewayAdapter`, or a seventh “generic” parser.

Concrete order:

1. **Close Test Plane B (B1–B3).** Narrow `AllowsTest` to Testing+Development **or** drop `TestWebhook` and keep start-to-pay. Require `id` + `checkout_id` + `amount_total` + `currency` if the route stays. Add `Test_webhook_in_production_is_400` with `UseEnvironment("Production")`.
2. **Stripe `payment_status` + `async_payment_succeeded` (B4).** Ignore unpaid completed. Do not add PI succeeded. Test unpaid completed → ignored, checkout open; async succeeded → one `RCPT-`.
3. **CAS + unique charge (B5).** Unique `charges.CheckoutId`. Update checkout `open→paid` only if 1 row. Unique `documents.Number`. Still the same `BeginTransaction` in Handle.
4. **Session-id join on CHIP/Xendit/Billplz/Stripe parsers (B7).** Set `HostedSessionId`. One test: CHIP paid **without** metadata checkout_id still pays via `purch_1` == `ProviderSessionId`.
5. **Idempotency on create (B6).** Stripe already has a key. Xendit header. Others: do not call PSP if `ProviderSessionId` set. Comment stays until each rail has a belt.
6. **F00 methods in existing test files (G4).** One `[Test]` per remaining row. FakePsp only. No live network. Strengthen Xendit/Razorpay/Test replay.
7. **Mismatch fixtures per name (B8)** without changing the 400-no-insert policy.
8. **Start unknown 400 (G1)** if you want the allow-list error to be one string.
9. **MYR-only or zero-decimal table (G7)** on mint, not inside fulfill.
10. **Fix `.env.example` (B9).**

Pause-on-fulfill / One HMAC: **leave them.** They match W10–W24 on this SHA.

Billplz dual HMAC: **leave both dialects.** That is not W10.

SETTLED / preauthorized / setup-not-paid: **leave inverted vs Hub.**

---

## Refuse

Do not copy from Hub, and do not “complete” Pay by growing `IHostedRail`:

1. `IPaymentGatewayAdapter` / `PaymentGatewayFactory` / `IPaymentGatewayFactory` / `IEnumerable<IHostedRail>` lookup / keyed `IHostedRail`.
2. ProjectReference `apps/lazuar-api`. `MediatR`. Outbox. `GatewayPaymentCompletedIntegrationEvent`. `AddPaymentsModule`. `Modules.Payments`.
3. `ChipWebhookRegistrar` silent register on PUT/boot.
4. `PublicDnsFallback` / rewrite to `lazuar-local-dev.com`. `App:AllowInsecureBillplzCallback`.
5. Hub `PAYMENT_COMPLETED` for Stripe setup, CHIP `purchase.preauthorized`, Xendit `SETTLED`.
6. Stripe Billing as source of truth (`mode=subscription`, `invoice.paid`, `customer.subscription.updated`).
7. Connect `application_fee` / `TransferData` / `Stripe-Account`.
8. `Razorpay.Api`. Razorpay e-mandate / `ChargeOffSession`. Billplz Payment Orders as refunds.
9. SST / LHDN / Tax Invoice / `SstTaxMath` / VALID.
10. `DecryptOrPlaintext`. Process-wide CHIP PEM or Billplz X-Signature.
11. Treat 015 / 016 `[x]` as evidence a test exists.
12. Occupancy algorithm inside the webhook handler as a way to skip B5.
13. A seventh rail registered “for later” and resolved from DI.

---

## Appendix: quoted evidence

### Allow-list includes test; Production is a later gate

```16:29:apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs
    public static readonly string[] All = [Stripe, Chip, Billplz, Xendit, Razorpay];

    public static IReadOnlyList<string> Listed(IHostEnvironment env) =>
        AllowsTest(env) ? [..All, Test] : All;

    public static bool AllowsTest(IHostEnvironment env) =>
        !env.IsProduction();
    // ...
        return provider is Stripe or Chip or Billplz or Xendit or Razorpay or Test;
```

### Isolation fence (cathedral strings)

```5:16:apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs
        "IPaymentGatewayAdapter", "PaymentGatewayFactory",
        "IPaymentGatewayFactory", "AddPaymentsModule", "GatewayPaymentCompletedIntegrationEvent", "Modules.Payments",
        "ApplicationFeeAmount", "Razorpay.Api",
        "application_fee", "TransferData", "transfer_data",
        "ChipWebhookRegistrar", "PublicDnsFallback",
        "Lhdn", "MyInvois", "UBL", "XAdES", "Irbm",
        "IEnumerable<IHostedRail>",
        "namespace Lazuar.Pay.Gateways",
```

### Pause does not consume paid event id (W22 live)

```126:154:apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs
        var orgSettings = await db.OrgSettings.FindAsync([orgId], ct);
        if (orgSettings?.ChargesPaused == true)
        {
            return PayErrors.Status(409, "Conflict", "Org charges are paused");
        }
        // currency / amount mismatch …
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.PspWebhookEvents.Add(new PspWebhookEventRow { /* … */ });
            await fulfillment.FulfillPaidAsync(checkout.Id, name, parsed.ProviderRef, ct);
            await tx.CommitAsync(ct);
        }
```

### Stripe Testing-only process fallback (E10 live)

```85:90:apps/lazuar-pay/src/Lazuar.Pay/Rails/Stripe/StripeWebhook.cs
        if (env.IsEnvironment("Testing"))
        {
            return config["Pay:StripeWebhookSecret"];
        }

        return null;
```

### Billplz public https; localhost 400

```80:103:apps/lazuar-pay/src/Lazuar.Pay/Rails/Billplz/BillplzHosted.cs
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            error = "callback base not public";
            return false;
        }
        // loopback / localhost / 127.0.0.1 / ::1 / lazuar-local-dev.com → same error
```

### CHIP preauthorized invert (not paid)

```65:68:apps/lazuar-pay/src/Lazuar.Pay/Rails/Chip/ChipWebhook.cs
        if (eventType == "purchase.preauthorized")
        {
            return new PspParseResult { EventId = "preauth:" + purchaseId, Ignored = true, IgnoreReason = "preauthorized" };
        }
```

Hub still maps preauthorized+token to `PAYMENT_COMPLETED` (`ChipCollectGatewayAdapter.cs:157–163`). Do not copy.

### Xendit SETTLED invert (not paid)

```57:68:apps/lazuar-pay/src/Lazuar.Pay/Rails/Xendit/XenditWebhook.cs
        if (status.Equals("SETTLED", StringComparison.OrdinalIgnoreCase)
            || status.Equals("invoice.settled", StringComparison.OrdinalIgnoreCase))
        {
            return new PspParseResult { EventId = "settled:" + invoiceId, Ignored = true, IgnoreReason = "settled" };
        }
        var paid = status.Equals("PAID", StringComparison.OrdinalIgnoreCase)
                   || status.Equals("invoice.paid", StringComparison.OrdinalIgnoreCase);
```

Hub `MapStatus` returns `PAYMENT_COMPLETED` for SETTLED (`XenditGatewayAdapter.cs:425–429`). Do not copy.

### Xendit hash-first token (Hub 073 stolen)

```34:40:apps/lazuar-pay/src/Lazuar.Pay/Rails/Xendit/XenditWebhook.cs
        var left = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        var right = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        if (!CryptographicOperations.FixedTimeEquals(left, right))
        {
            throw new PspVerifyException("invalid signature");
        }
```

### Official Receipt, not tax invoice

```110:120:apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs
        seq.LastN += 1;
        var number = $"RCPT-{year}-{seq.LastN:00000}";
        db.Documents.Add(new DocumentRow
        {
            Id = Guid.NewGuid().ToString("N"),
            OrgId = checkout.OrgId,
            CheckoutId = checkout.Id,
            Number = number,
            Title = "Official Receipt",
            CreatedAt = DateTimeOffset.UtcNow
        });
```

```7:9:apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs
    /// <summary>Unused. Tax is out of this program. Column kept; do not read on the pay path.</summary>
    public bool? SstRegistered { get; set; }
    /// <summary>Unused. Vault save does not pick a default rail. Column kept; do not read on the pay path.</summary>
    public string? ActiveProvider { get; set; }
```

### One HMAC (W10 live; was 016 P0-4)

```7:43:apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookSignature.cs
/// Standard Webhooks–style verify: header t={unix},v1={lowercase hex} over {unix}.{body}.
/// Judgment stolen from One's signer. Do not import the Hub worker type.
        var signedPayload = $"{timestamp}.{body}";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signedPayload));
        var expectedHex = Convert.ToHexString(hash).ToLowerInvariant();
        return FixedTimeEqualsHex(v1Hex, expectedHex);
```

### Buyer email refuse (Hub `GatewayCommon.PlaceholderEmail` stolen as judgment)

```5:9:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/BuyerEmail.cs
    public const string Placeholder = "customer@example.com";

    public static bool IsUsable(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && !string.Equals(email.Trim(), Placeholder, StringComparison.OrdinalIgnoreCase);
```

### Success URL is not paid (default)

```8:11:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/CheckoutUrls.cs
        string.IsNullOrWhiteSpace(checkout.SuccessUrl)
            ? Base(config, env) + "/c/" + checkout.PublicToken + "?status=verifying"
            : checkout.SuccessUrl;
```

### Hub Billplz still invents MYR (Pay must not)

```237:249:apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs
            return Task.FromResult(new GatewayWebhookParsedResult(
                Verified: true,
                EventType: isPaid ? "PAYMENT_COMPLETED" : "PAYMENT_FAILED",
                EventId: $"{(isPaid ? "PAYMENT_COMPLETED" : "PAYMENT_FAILED")}:{billId}",
                AmountPaid: paidAmountMyr,
                Currency: "MYR",
```

Pay `BillplzWebhook` throws `missing currency` instead (`BillplzWebhook.cs:77–79`).

### No unique charge per checkout (B5 schema)

```33:49:apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260821152601_Initial.cs
            migrationBuilder.CreateTable(
                name: "charges",
                // …
                    table.PrimaryKey("PK_charges", x => x.Id);
```

`PayDbContext` adds no unique index on `ChargeRow.CheckoutId`.

### README (host honesty; Test/Billplz/Stripe fallback)

From `apps/lazuar-pay/README.md`: capability `hosted_link`; process `Pay__StripeWebhookSecret` is Testing-only; Billplz `Pay__PublicBaseUrl` public https (localhost 400); success URL is not paid; `:5179` polls `?status=verifying`; a sixth hosted rail is `Rails/Foo/` plus two switch arms; do not add `IEnumerable<IHostedRail>`.
