# 05 — Payment links and payer capacity

**Date:** 26 August 2026  
**Branch:** `feat/018-merchant-shell`  
**HEAD:** `9f04ad58` — `fix(pay-ui): match receipts table to pay-link chrome`  
**Type:** Uncondensed evaluation. **Not** an implementation. Live files on this SHA are authority.

**Question:** How many people can pay a pay link, how is that number enforced, and where does the live code over-admit, under-admit, or lie?

---

## Coordinates

| Item | Value |
|------|--------|
| Host | `apps/lazuar-pay` — focused C# host, public schema, Postgres 5435 in local compose |
| Mint door (capacity) | `POST /v1/payment-links` → `PaymentLinkRow` with `MaxPayers` |
| Mint door (no capacity) | `POST /v1/checkouts` → standalone `CheckoutRow`, `PaymentLinkId` null |
| Occupancy | Derived. `COUNT` of child checkouts whose status is `open` or `paid`. No stored counter. |
| Public GET | `GET /v1/pay/{token}` — payment-link token first, else checkout token |
| Public start | `POST /v1/pay/{token}/start` — for a link, mints or resumes a child checkout then talks to the rail |
| Slot identity | Buyer `slot_key` (SPA: `localStorage` UUID per link token). Not email. Not One account. |
| Merchant UI | `apps/lazuar-pay-merchant` route `/o/:orgId/checkouts` labeled **Pay links** |
| Buyer UI | `apps/lazuar-pay-checkout` route `/c/:token` |
| Schema | Migration `20260825120000_PaymentLinkPayers` |
| Tests | `PaymentLinkTests` holds occupancy. `PublicPayTests` has **zero** occupancy cases. Fulfillment never mentions a pay link. |

JSON is snake_case (`Program.cs` `JsonNamingPolicy.SnakeCaseLower`; same on `OneClient.Json`). Field names below are C# in source and snake_case on the wire.

---

## Files opened

Host / model / occupancy:

- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkOccupancy.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/CreatePaymentLinkRequest.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/CheckoutUrls.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/BuyerEmail.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260825120000_PaymentLinkPayers.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260824120000_FourAdaptersHostedRails.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260821152601_Initial.cs` (checkout table before link columns)
- `apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/PayDbContextModelSnapshot.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutStore.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutSession.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CreateCheckoutRequest.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Catalog/CatalogEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Money/MoneyMath.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Money/Queries/PaymentQueryEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestHosted.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestWebhook.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Stripe/StripeHosted.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Chip/ChipHosted.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Hosting/PayErrors.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Lazuar.Pay.csproj`
- `apps/lazuar-pay/docker-compose.pay.yml`

Tests:

- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPay/PublicPayTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayTest.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Test/TestRailTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Webhooks/FillTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Catalog/CatalogTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Checkouts/CheckoutTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs`

Merchant / checkout SPAs:

- `apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx`
- `apps/lazuar-pay-merchant/src/locks.test.ts`
- `apps/lazuar-pay-merchant/src/App.tsx`
- `apps/lazuar-pay-merchant/src/layout/nav.ts`
- `apps/lazuar-pay-merchant/src/layout/OrgLayout.tsx`
- `apps/lazuar-pay-checkout/src/App.tsx`
- `apps/lazuar-pay-checkout/src/locks.test.ts`

Glance (not the occupancy algorithm; used to confirm absence):

- `packages/pay-spec/main.tsp` — no `payment-links`, no `slot_key`, no `max_payers`. Contracts paper owns the spec. Named here only as a hole against live doors.
- `plans/019-evals/README.md` — this file is slice 05.
- `deploy/prod/docker-compose.yml` — Hub stack; no Pay migrate story.

---

## What exists (model, APIs, UI, occupancy algorithm)

### Model

`PaymentLinkRow` is a shared URL. Comment on the row: “Shared pay-link URL. MaxPayers null is unlimited. Each payer is a child checkout.” (`Rows.cs:36`).

```36:49:apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs
/// <summary>Shared pay-link URL. MaxPayers null is unlimited. Each payer is a child checkout.</summary>
public sealed class PaymentLinkRow
{
    public required string Id { get; set; }
    public required string OrgId { get; set; }
    public required string PublicToken { get; set; }
    public required string Provider { get; set; }
    public string? ProductId { get; set; }
    public required decimal Amount { get; set; }
    public required string Currency { get; set; }
    /// <summary>Null means unlimited payers. 1 is one person. N is a cap.</summary>
    public int? MaxPayers { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

Child checkouts carry the parent id and the buyer slot:

```31:32:apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs
    public string? PaymentLinkId { get; set; }
    public string? SlotKey { get; set; }
```

There is **no** `TakenCount`, `PaidCount`, `Remaining`, or `Status` column on `payment_links`. Occupancy is always a query. The link never expires, never cancels, never changes `MaxPayers` after insert. There is no FK from `checkouts.PaymentLinkId` to `payment_links.Id`, and none from `payment_links.ProductId` to `products.Id`.

Postgres unique (only on Npgsql, not InMemory tests):

```43:48:apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs
            if (Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
            {
                e.HasIndex(x => new { x.PaymentLinkId, x.SlotKey })
                    .IsUnique()
                    .HasFilter("\"SlotKey\" IS NOT NULL");
            }
```

That unique is **per slot**, not per capacity. Two different `slot_key`s on the same link are allowed even when `taken > MaxPayers`. Standalone checkouts (`SlotKey` null) are excluded by the filter.

### Create request field names

```3:13:apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/CreatePaymentLinkRequest.cs
public sealed class CreatePaymentLinkRequest
{
    public string? OrgId { get; set; }
    public string? Provider { get; set; }
    public string? ProductId { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    /// <summary>1 is one person. N is a cap. Ignored when Unlimited is true. Default 1.</summary>
    public int? MaxPayers { get; set; }
    public bool Unlimited { get; set; }
}
```

Wire names: `max_payers`, `unlimited`. There is no field called `capacity`. Capacity is a merchant-UI union (`'one' | 'limited' | 'unlimited'`) mapped onto those two JSON fields.

Create rules (`PaymentLinkEndpoints.Create`):

| Body | Stored `MaxPayers` | Meaning |
|------|--------------------|---------|
| `unlimited: true` (any `max_payers`) | `null` | Unlimited. `max_payers` is ignored, including `0`. |
| `unlimited` omitted/false, `max_payers` omitted/null | `1` | Default one person. |
| `unlimited` false, `max_payers` ≥ 1 | that int | Cap N. |
| `unlimited` false, `max_payers` < 1 | 400 | `"max_payers must be at least 1"` |

```73:97:apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs
        int? maxPayers;
        if (body.Unlimited)
        {
            maxPayers = null;
        }
        else
        {
            maxPayers = body.MaxPayers ?? 1;
            if (maxPayers < 1)
            {
                return PayErrors.Status(400, "Bad Request", "max_payers must be at least 1");
            }
        }

        var currency = string.IsNullOrWhiteSpace(body.Currency) ? "MYR" : body.Currency.Trim().ToUpperInvariant();
        var row = new PaymentLinkRow
        {
            ...
            Amount = body.Amount.Value,
            Currency = currency,
            MaxPayers = maxPayers,
```

No upper bound on N. No MYR-only check (catalog create **does** reject non-MYR; payment links do not). Amount is taken from the body, not from `products`/`prices`. `ProductId` is optional and not loaded for amount/currency.

Create returns 201 with `Map(row, taken: 0, paid: 0)` — remaining equals max, status `open`.

### Occupancy algorithm (the whole class)

```1:13:apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkOccupancy.cs
namespace Lazuar.Pay.PaymentLinks;

internal static class PaymentLinkOccupancy
{
    public static bool CountsTowardCapacity(string status) =>
        status is "open" or "paid";

    public static bool IsFull(int? maxPayers, int taken) =>
        maxPayers is int max && taken >= max;

    public static int? Remaining(int? maxPayers, int taken) =>
        maxPayers is int max ? Math.Max(0, max - taken) : null;
}
```

Read that three times:

1. **Started (`open`) counts.** A child that has not paid still occupies a seat.
2. **Paid counts.** Converting `open` → `paid` does **not** change `taken`. Fulfillment is occupancy-neutral.
3. **Unlimited is `MaxPayers == null`.** `IsFull` is false. `Remaining` is null. Any number of children is allowed.
4. **No other status is defined as counting.** `expired` would not count — but nothing writes `expired`. There is no `canceled` / `cancelled` / `abandoned` string in the pay host except Razorpay *PSP event names* and checkout `CancelUrl`.
5. **Identity is not unique payer.** Occupancy is “number of child checkouts in {open, paid}”, not distinct emails, not distinct cards, not One subjects.

Merchant list and public GET both use this class. Start uses a copy of the same predicate inline (`Status == "open" || Status == "paid"`) instead of calling `CountsTowardCapacity`. Same meaning today; two spellings.

### When occupancy increments

Not at create of the link (`taken: 0`).

Not at GET.

Not at webhook / `FulfillPaidAsync` (status stays in the counting set).

**At `POST /v1/pay/{token}/start` for a payment-link token, inside `MintOrResume`, when no child exists for that `(PaymentLinkId, SlotKey)` and the link is not already full.** The insert sets `Status = "open"` and `SaveChanges` **before** PSP HTTP and **before** email/rail validation on the outer `Start` method.

Resume of the same slot does not increment.

### APIs

| Door | Auth | Occupancy role |
|------|------|----------------|
| `POST /v1/payment-links` | writer | Stores cap. Does not take a seat. |
| `GET /v1/orgs/{orgId}/payment-links` | member | Reports `taken_count`, `paid_count`, `remaining`, `status` `open`\|`full`. |
| `POST /v1/checkouts` | writer | **No** `MaxPayers`. Not a shared link. Merchant UI no longer calls this. |
| `GET /v1/orgs/{orgId}/checkouts` | member | Lists **all** checkouts including children of links. Merchant Pay-links page does not use it. |
| `GET /v1/pay/{token}` | public | If token is a link: occupancy view, or the caller’s child, or (max=1 paid) someone else’s paid child. |
| `POST /v1/pay/{token}/start` | public | Link: require `slot_key`, mint/resume, then rail. Full → **409** `"This pay link is full"`. Missing/short `slot_key` → **400**. |
| `POST /v1/webhooks/{provider}/{orgId}` | PSP signature | Fulfills the child if `open`. Does not read `MaxPayers`. |
| `GET /v1/orgs/{orgId}/payments` / receipts | member | Charges/documents. No `payment_link_id`. Cannot group seats. |

There is no `GET/PATCH/DELETE /v1/payment-links/{id}`. No deactivate. No change cap. No expire.

### GET public pay when full

HTTP **200**, never 400/409. Body `status` is either `"full"` (link-level view) or the **child checkout status** (`"paid"` / `"open"`) if the caller has a matching `slot_key`, or if `MaxPayers == 1` and at least one child is paid (even without a matching slot).

```50:78:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
    static async Task<IResult> GetLink(PaymentLinkRow link, string? slotKey, PayDbContext db, CancellationToken ct)
    {
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
```

So:

- GET full, max N>1, no matching slot → `{ status: "full", remaining: 0, ... }` 200.
- GET full, max 1, paid ≥ 1, no matching slot → `{ status: "paid", ... }` 200 of **a** paid child (`.First`, not “yours”).
- GET full, max 1, paid = 0, taken = 1 (someone started, did not pay) → `{ status: "full" }` 200 for strangers; the slot owner gets `{ status: "open" }` of their child.

### POST start when full

**409**, not 400. Detail `"This pay link is full"`.

```236:242:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
        var taken = await db.Checkouts.CountAsync(
            x => x.PaymentLinkId == link.Id && (x.Status == "open" || x.Status == "paid"),
            ct);
        if (PaymentLinkOccupancy.IsFull(link.MaxPayers, taken))
        {
            return (null, PayErrors.Status(409, "Conflict", "This pay link is full"));
        }
```

400 is reserved for `slot_key is required` (missing / blank / length not in 8–128) and for later email/callback-base problems. Paid/expired **existing child** is also 409, but the detail is `"Checkout is not open"`, not `"full"`.

### Merchant UI

Route still `/checkouts`; chrome says Pay links (`nav.ts:12`, `OrgLayout.tsx:22`).

Mint dialog:

- Capacity select: `1 person only` | `Limited number` | `Unlimited`.
- Limited requires integer ≥ 2 (UI only; API allows 1).
- Body: `max_payers: 1 | limited | undefined`, `unlimited: boolean`.
- Always `POST /v1/orgs/{orgId}/products` first (new product every time, MYR, amount from the same form), then `POST /v1/payment-links` with `product_id`, `amount`, `currency: 'MYR'`, chosen `provider`.

Table:

- Payers column: unlimited → `"Unlimited"` or `"{paid} paid · unlimited"`; limited/one → `"{taken} / {max_payers}"`.
- Status: API `full` remapped to `paid` only when `max_payers === 1 && paid_count >= 1`. Otherwise raw `open` / `full`.
- Copy URL + Open always, including when full. URL is `{VITE_CHECKOUT_ORIGIN}/c/{public_token}`.
- `remaining` from the API is typed on `PayLink` and **never rendered**.

Dialog copy (the product claim):

```397:401:apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx
                <p className="text-xs text-slate-500">
                  {capacity === 'one'
                    ? 'The link closes after one successful payment.'
                    : 'Anyone with the URL can pay. It does not close on its own.'}
                </p>
```

That sentence is **false** against `CountsTowardCapacity`. The link closes after one **start** (`open` child), not after one **successful payment**.

### Checkout SPA

`PayView` type only has `token, amount, currency, status, email_required, started, provider, redirect_url`. API also returns `remaining`, `max_payers`, `paid_count`, `taken_count`. The SPA **drops them**. No “2 of 5 left”.

Slot:

```21:36:apps/lazuar-pay-checkout/src/App.tsx
function slotKey(token: string): string {
  const key = `lazuar-pay-slot:${token}`
  try {
    const existing = localStorage.getItem(key)
    if (existing) return existing
    const next = crypto.randomUUID()
    localStorage.setItem(key, next)
    return next
  } catch {
    return crypto.randomUUID()
  }
}

function payPath(token: string): string {
  return `${payApi}/v1/pay/${token}?slot_key=${encodeURIComponent(slotKey(token))}`
}
```

Start POST sends the same `slot_key`. 409 → refetch GET; if GET is `full`, the `full` card renders. `paid` / `expired` / `full` are mutually exclusive cards. Copy for full: “Link is full” / “This pay link has no remaining payments.” No distinction between “sold out because paid” and “someone clicked Pay and walked away”.

Honesty lock `locks.test.ts` only asserts the strings `lazuar-pay-slot:`, `slot_key`, `pay.status === 'full'`, `Link is full` exist. It does not hit the host.

### Checkouts spawned from a link

`MintOrResume` copies **link** snapshot, not live catalog:

```244:264:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
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
            SuccessUrl = baseUrl + "/c/" + link.PublicToken + "?status=verifying",
            CancelUrl = baseUrl + "/c/" + link.PublicToken,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Checkouts.Add(row);
        await db.SaveChangesAsync(ct);
```

Notes:

- Child gets its **own** `PublicToken`. Success/cancel URLs use the **link** token. The child token is a dead URL unless someone GETs it. GET of the child token skips occupancy (`Get` looks up payment links first; miss → standalone checkout view).
- `Interval` is always `one_off` even if the catalog price was `mo`/`yr`. Fulfillment will not mint a subscription for link children.
- Amount/currency/provider/product id frozen at **link create**, copied at **child mint**. Changing the product’s `PriceRow` later does nothing.

### Money / fulfillment as it consumes a slot

It does not consume a slot. It only moves `open` → `paid` if still `open`:

```26:37:apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs
        if (checkout.Status != "open")
        {
            return;
        }
        ...
        checkout.Status = "paid";
```

No `PaymentLinkId` read. No `IsFull`. No reject of the (N+1)th payment. Webhook path (`WebhookEndpoints.Handle`) matches amount/currency to **the checkout snapshot**, then `FulfillPaidAsync`. Same: occupancy-blind.

Test rail Start does not wait for a webhook. After mint, `CreateHostedUrlAsync` returns the success URL, then:

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

So on Test, the `open` window is **inside one Start request** after mint `SaveChanges` and before fulfill `SaveChanges`. Sequential tests therefore see `paid` immediately (`Two_people_can_pay_a_link_of_two`, `Unlimited_accepts_three_payers`, `One_person_link_shows_paid_without_slot_after_pay`). They **do not** exercise abandoned `open` seats. CHIP in `Same_slot_start_twice_does_not_take_two_seats` does: first Start stays `open`, `taken_count == 1`.

### Migration on existing DBs

`20260825120000_PaymentLinkPayers` is additive:

- Creates `payment_links` (including nullable `MaxPayers`).
- Adds nullable `PaymentLinkId`, `SlotKey` on `checkouts`.
- Indexes: `IX_payment_links_OrgId`, unique `IX_payment_links_PublicToken`, `IX_checkouts_PaymentLinkId`, unique filtered `IX_checkouts_PaymentLinkId_SlotKey`.

Existing checkout rows from `POST /v1/checkouts` get null link/slot. They are **not** backfilled into `payment_links`. They keep working as standalone public tokens. They **do not** appear on the new Pay-links table (that lists `payment_links` only).

Apply path:

```73:78:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<PayDbContext>().Database.MigrateAsync();
}
```

**Development only.** Tests use InMemory `EnsureCreated()` (`PayApiFactory`), which builds the current model and **skips** the Npgsql-only unique index. Production (`IsDevelopment() == false`) does **not** migrate on boot. There is no Pay service in `deploy/prod/docker-compose.yml`. Local compose `docker-compose.pay.yml` is Postgres only — it does not run EF. A non-Development host against an old DB will 500 on `POST /v1/payment-links` because `payment_links` does not exist.

No Designer.cs for this migration (same pattern as `FourAdaptersHostedRails`). Snapshot includes `MaxPayers` and the unique filter. Hand-written migration matches snapshot.

---

## Capacity semantics (started vs paid vs unique payer; re-read live code)

### The live rule, in one sentence

**A seat is a child checkout in `open` or `paid`, keyed by `slot_key` (8–128 chars), capped by `PaymentLinkRow.MaxPayers` (null = unlimited), checked with a non-transactional `COUNT` at mint time.**

It is not:

- unique human
- unique email (`PayerRow` is a new Guid every fulfill)
- unique card
- unique browser except insofar as `localStorage` sticks
- unique paid charge
- a stored integer that can be updated atomically

### Started vs paid

| Event | `taken` | `paid` | Seat released? |
|-------|---------|--------|----------------|
| Link created | 0 | 0 | — |
| Start, new slot, not full | +1 (`open`) | 0 | No |
| Same slot Start again (still `open`) | 0 | 0 | Resume. If `PspRedirectUrl` set, no second PSP HTTP. |
| Test rail Start | +1 then same row `paid` | +1 | `taken` unchanged by fulfill |
| Live rail webhook fulfill | 0 | +1 | `taken` unchanged |
| Buyer hits cancel URL | 0 | 0 | Child stays `open`. Seat held. |
| PSP session abandoned | 0 | 0 | Seat held forever |
| Start, existing child `paid`/`expired` | 0 | 0 | 409 `"Checkout is not open"` |
| Charges paused after `open` mint | 0 | 0 | Seat held; webhook 409; Start 403 |

Merchant dialog describes **paid**. Algorithm implements **started**. Table for limited links displays **taken / max** (algorithm-honest). Table for unlimited displays **paid** (algorithm-dishonest for in-flight CHIP starts). Status remap `full`→`paid` only for max=1 with `paid_count >= 1`.

### Unique payer

There is none.

- SPA identity = `localStorage['lazuar-pay-slot:'+token]` → UUID.
- Incognito, second phone, cleared storage, or `localStorage` throw → new UUID → new seat.
- Same person, two tabs, same origin: same key, resume. Tested (`Same_slot_start_twice_does_not_take_two_seats`).
- Same person, two emails, one browser: still one seat.
- Two people sharing a laptop: one seat.

`NormalizeSlotKey` rejects `< 8` or `> 128`. `crypto.randomUUID()` is 36 chars. Tests use keys like `slot-aaa-1` (10 chars).

### Unlimited vs 1 vs N

| Cap | Create | `IsFull` | GET after fill | Merchant status |
|-----|--------|----------|----------------|-----------------|
| 1 (default) | `max_payers` omitted or 1 | `taken >= 1` | If paid: strangers see **paid**. If only `open`: strangers see **full**. | `paid` if paid else `full`/`open` |
| N ≥ 2 | `max_payers: N` | `taken >= N` | Strangers see **full**, never another payer’s receipt | `full` when taken≥N even if unpaid |
| Unlimited | `unlimited: true` → `MaxPayers` null | never | always `open` (link view) | `open`; payers label uses paid count |

Merchant UI refuses limited N=1 (forces the “one” preset). API does not.

`unlimited: true` plus `max_payers: 0` is **not** 400 — unlimited wins first.

### Idempotent start vs new occupancy

1. Same `(link, slot)` existing `open` → return that row. Occupancy unchanged. If `PspRedirectUrl` already set, Start returns it and skips rail HTTP (`PublicPayEndpoints.cs:151-155`). CHIP test locks this.
2. Same `(link, slot)` existing `paid`/`expired` → 409 checkout not open. Occupancy unchanged. SPA 409 refetches GET; matching slot + paid → “Payment received”.
3. New slot, not full → insert `open` → occupancy +1.
4. New slot, full → 409 full. Occupancy unchanged.
5. Concurrent new slots: see Bugs. Unique index does not save you.
6. Concurrent same slot on Postgres: unique index should fail one insert. `MintOrResume` does **not** catch `DbUpdateException` to resume. That caller 500s. Tests cannot see this (InMemory, no unique).

### Does start reserve a seat before PSP redirect?

**Yes.** Order in `Start` for a link token:

1. `MintOrResume` — paused check, slot_key 400, resume or COUNT+insert `open` + **SaveChanges**.
2. Copy name/email onto the row (not saved yet).
3. Normalize provider; 503 if unknown.
4. Email required? **400 after the seat exists.**
5. Existing `PspRedirectUrl`? save name/email, return URL.
6. `rail.CreateHostedUrlAsync` (HTTP to PSP except Test).
7. Persist redirect; Test also fulfills.

An abandoned Start, a 400 email, a 503 rail reject, a CHIP HTTP failure, a buyer who never returns from the PSP — all leave an `open` child if mint already committed.

### Webhook fulfill after capacity already full

If occupancy is open+paid, “full” means N children already exist. A webhook for one of those children **should** pay it: that is converting the reserved seat, not adding one.

The dangerous case is **over-admitted children** (race minted N+1 `open` rows). Each can fulfill independently. You get N+1 Official Receipts, N+1 journal entries, N+1 charges, merchant table `taken_count` possibly `3 / 1`. Fulfillment will not stop it.

If product intent were “paid only”, this section would be the entire cap: two buyers pay, two webhooks, two receipts, COUNT paid was 0 at both Starts. Live code is not that model — unless the race punches a hole in the start-time COUNT.

### Same buyer paying twice

- Same slot, first still `open`: resume, one seat. Cannot pay twice through Start while `open`; second Start reuses the same PSP session if URL stored.
- Same slot, first `paid`: 409, GET shows paid. No second charge from Start.
- New slot (new device): treated as a new person. Unlimited: second charge. Cap 1: 409 full (or GET paid for strangers on max=1).
- Direct webhook on a second child: if that child exists and is `open`, it pays. No “this email already paid this link”.

### Catalog / currency / amount snapshot

Merchant always mints a **new** product then a link with the **same form amount**. Catalog create requires MYR (`CatalogEndpoints.cs:32-36`, `"Bar B currency is MYR"`). Payment-link create defaults to MYR and **accepts** other currencies. SPA hardcodes `'MYR'` on both posts.

Live `PriceRow` is never read on Start. Rails (`StripeHosted`, `ChipHosted`, …) use `checkout.Amount` / `checkout.Currency`. Webhook amount-mismatch compares PSP to **checkout**, not to the product.

If an API client sets `product_id` of org A’s product on org B’s link, list lookup of labels is `Products.Where(id in …)` **without org filter** (`PaymentLinkEndpoints.cs:144-146`) — name leak, not an occupancy bug, catalog-adjacent.

Product interval is ignored; children are `one_off`.

---

## Bugs (races, over-admit, under-admit, UI lies)

### B1. TOCTOU over-admit (P0)

`MintOrResume` is:

```
existing = SELECT by (link, slot)
if none:
  taken = COUNT open|paid
  if taken >= max: 409
  INSERT open
  SaveChanges
```

No `BEGIN`; no `SELECT … FOR UPDATE` on `payment_links`; no `UPDATE … WHERE taken < max`; no serializable retry; no occupancy table with a unique seat number. `WebhookEndpoints` is the only place that opens a transaction, and it is for event-id uniqueness, not seats.

Two buyers, max=1, two slot keys, same millisecond:

- Both COUNT 0.
- Both INSERT.
- Unique `(PaymentLinkId, SlotKey)` does not conflict.
- Two `open` children. `taken = 2`, `MaxPayers = 1`.
- Test rail: both fulfill in-request. Two receipts.
- CHIP/Stripe: both get hosted URLs. Both webhooks pay.

`IsFull` / `Remaining` then clamp remaining at 0 (`Math.Max(0, max - taken)`), so GET says full, but money already moved. Merchant can show `2 / 1`.

This is the occupancy bug. Sequential tests never see it. InMemory would not serialize it even if tests were parallel.

### B2. Same-slot race is a 500, not a resume (P1)

The unique index is the right tool for “one browser, two double-clicks”. `MintOrResume` does not catch unique violation and `SELECT` the winner. On Postgres one of the two requests 500s. On InMemory both inserts succeed (no unique) — tests would **green** a double seat if they ever ran concurrent same-slot Starts.

### B3. Seat reserved before the Start can succeed (P0)

Mint commits `open` before:

- email required (CHIP / Billplz / Xendit / Razorpay: `PayProviders.RequiresEmail` is true for all except Stripe and Test)
- provider usable
- PSP `CreateHostedUrlAsync`

SPA blocks email client-side for `email_required`. Raw `POST /v1/pay/{token}/start` with a valid `slot_key` and empty email on CHIP: **400 after occupying the only seat**. Other buyers GET `full`. The 400 caller can retry **the same slot** and proceed; a new browser cannot.

PSP 503 after mint: same. Retry same slot is the only recovery. There is no expire.

### B4. Abandoned starts never release (P0 under-admit for everyone else)

Nothing writes `expired`. `Start` and `MintOrResume` **read** `"paid" or "expired"` as terminal. Cancel URL returns the buyer to `/c/{linkToken}` without changing status. No worker, no TTL, no merchant “release seat”.

Product copy: “closes after one successful payment.” Live: one **click Pay** on a live rail (or one 400-after-mint) fills a one-person link with RM 0 collected.

Under-admit: genuine buyers are blocked by a ghost `open` child.

`CountsTowardCapacity` would treat `expired` as free if anything set it. Nothing does.

### B5. Merchant / API status lies (P0 product)

| Surface | Claim | Live |
|---------|-------|------|
| Dialog, capacity `one` | “The link closes after one successful payment.” | Closes after one `open` child. |
| Dialog, unlimited | “Anyone with the URL can pay. It does not close on its own.” | True for cap (never full). Does not mention seats held by abandoned starts (unlimited so they do not block). |
| API `PaymentLinkView.Status` | `"full"` or `"open"` never `"paid"` | Merchant remaps max=1 full+paid → `paid`. Public GET for that case returns checkout `"paid"`. Three spellings of one fact. |
| Unlimited payers column | uses `paid_count` | CHIP in-flight starts invisible. Limited column uses `taken_count`. Mixed definition of “how many people”. |
| Buyer full card | “no remaining payments” | May mean “no remaining **starts**”. |
| GET max=1 paid, no slot | `status: paid` | A stranger sees **Payment received** for someone else’s money (`GetLink` special case + SPA `pay.status === 'paid'` first). |

The max=1 GET special case is tested and therefore intentional-looking (`One_person_link_shows_paid_without_slot_after_pay`). It is still a UX lie for a second person: they did not pay; they see a receipt. For N>1 the code does **not** leak another payer’s checkout. Inconsistent privacy.

### B6. localStorage failure mints a new payer every call (P1)

`slotKey` catch path returns a **fresh UUID and does not remember it**. `payPath` (GET, poll) and `startPay` (POST) each call `slotKey`. If storage throws:

- GET uses UUID-A
- POST uses UUID-B → occupies a seat
- poll uses UUID-C → `mine` miss → if that was the last seat, SPA shows **Link is full** to the person who just paid

Under-admit of the buyer’s own receipt; possible extra seats if GET somehow minted (GET does not mint — only Start does). Still: they can fill the cap and never see paid.

### B7. Fulfillment will pay over-capacity children (P0 given B1)

`checkout.Status != "open"` → return. That is idempotency for **that checkout**, not a cap. After B1, every extra `open` child is a live payable object. Webhook amount check uses the child’s snapshot (same as the link), so it will **match** and mint RCPT.

No refund path, no “ignore paid because link full”, no auto-expire of extras.

### B8. Test rail hides the reservation model in the tests that claim capacity (P1 test lie)

`Two_people_can_pay_a_link_of_two` and `One_person_link_shows_paid_without_slot_after_pay` seed `provider: test` (PayTest default). Start pays instantly. The third Start 409 is “two paid seats”, which coincides with “two started seats”. Replace Test with CHIP and the same test would 409 the third Start after two **unpaid** hosted sessions — which is the real product, and is **not** what the merchant dialog says.

`Same_slot_start_twice` is the only payment-link test that uses CHIP and therefore the only one that observes `open` occupancy.

### B9. Charges-paused after mint stuck-occupies (P2)

Mint checks paused **before** insert. If the org is paused later: Start 403, webhook 409 `ChargesPausedException`, child remains `open`, seat held. GET still shows the pay form for the slot owner (`GetLink` does not read `ChargesPaused`). They cannot pay; nobody else can take the seat.

### B10. Child public tokens bypass the link GET occupancy view (P2)

`Get` prefers `PaymentLinks` by token, else `CheckoutStore.GetByPublicTokenAsync`. A leaked child token returns `CheckoutView` **without** `remaining` / `max_payers` (defaults null) and Start on that token uses the standalone branch (no `MintOrResume`, no full check). The child already exists, so Start will try the rail even if the merchant conceptually “full’d” — the seat is already taken, so this is not a new over-admit, but it is a second door onto the same checkout with none of the link metadata.

### B11. Over-admit display vs clamp (honesty leftover)

If B1 happens, merchant `payersLabel` shows `taken / max` as `2 / 1`. That is accidentally honest. `Remaining` is 0. Status `full`. Money 2×. The UI does not warn “over capacity”.

---

## Gaps

G1. **No product rule written down in code except the occupancy class and a false dialog string.** Start-reservation vs paid-only is undecided in the UI, decided in one 13-line class.

G2. **No expire / cancel / deactivate of a pay link or of a child.** `expired` is dead code. Cancel URL is a bounce, not a release.

G3. **No TTL on `open` children.** HitPay-style “link valid 24h” / Stripe session expire does not exist on the link or the child.

G4. **No merchant view of seats.** Payments/receipts have no `payment_link_id`. Cannot see Ada occupying seat 1 unpaid vs paid. Table is a fraction.

G5. **No remaining on the buyer page.** API already returns it.

G6. **Copy/Open still offered when full.** Maybe wanted (slot owner returning). Not explained.

G7. **No PATCH cap, no close-the-link.** Unlimited “does not close on its own” is absolute.

G8. **Catalog is a label factory.** New product per dialog. Price is duplicated onto the link. Interval discarded. Non-MYR allowed on the link API.

G9. **Standalone `POST /v1/checkouts` still exists** with public tokens and no cap. Two shapes of “pay link” in the host. Merchant only mints the new one.

G10. **No FK, no delete cascade, no org check on `product_id`.**

G11. **Production / non-Development does not apply `PaymentLinkPayers`.** Existing DBs on Development are fine (additive). Staging/prod Pay is undefined.

G12. **InMemory tests omit the only physical uniqueness** that exists (`PaymentLinkId, SlotKey`).

G13. **`pay-spec` does not know payment links, `slot_key`, `max_payers`, `unlimited`, `taken_count`.** Out of scope for this paper beyond naming the hole.

G14. **Same human, two devices, one-person link:** second device sees another person’s “Payment received” (max=1 paid) or “Link is full” (started unpaid). No “this is your payment” vs “someone else paid”.

G15. **Payer uniqueness / one-email-one-seat** not specified. Email is a CHIP field, not a key.

G16. **No upper bound on `max_payers`.** Unlimited plus no rate limit on Start = unbounded child checkouts and PSP sessions.

G17. **Merchant create is not transactional.** Product 201 + link 400 leaves an orphan product. Occupancy unaffected; catalog noise.

G18. **List `status: full` vs public `status: paid` for max=1.** Clients that are not the merchant SPA will disagree.

G19. **Start 409 details are overloaded.** SPA treats every 409 as “maybe full, refetch GET”. That happens to work if GET is truthful for that slot. A paused org is 403, not 409, on Start (link mint path). Standalone paid checkout Start is 409 `"Checkout is not open"`.

G20. **GET does not require `slot_key`.** SPA always sends one. API clients that omit it hit the max=1 paid leak and cannot resume an `open` child.

---

## Tests vs missing (name methods that should exist)

### What exists (and what it actually locks)

`PaymentLinkTests`:

| Method | What it proves | What it does not |
|--------|----------------|------------------|
| `Create_defaults_to_one_payer` | omit max → `max_payers=1`, `unlimited=false`, `remaining=1`, `status=open` | catalog, MYR, product |
| `Create_unlimited_has_null_max` | public GET `max_payers`/`remaining` null, status open | that Start does not 409 at large N |
| `Create_max_zero_is_400` | `max_payers:0` 400 contains `max_payers` | `unlimited:true` + `max_payers:0`; negatives already covered by `< 1` |
| `Create_without_bearer_is_401` | auth | occupancy |
| `List_returns_newest_first_with_capacity` | two links, remaining=max at rest, newest first | taken after Start; paid vs taken |
| `List_other_org_is_403` | authz | occupancy |
| `Two_people_can_pay_a_link_of_two` | sequential Start A,B OK, C 409 `"full"`; GET slot A `paid`; GET unused slot `full` remaining 0 | concurrency; CHIP abandoned `open`; Test rail instant pay |
| `Same_slot_start_twice_does_not_take_two_seats` | CHIP, same slot, one PSP HTTP, `taken_count=1`, `remaining=1` | concurrent same slot; unique violation mapping |
| `Unlimited_accepts_three_payers` | three Starts OK; GET `paid_count=3`, remaining null, status open | 4th, 100th; in-flight not paid |
| `One_person_link_shows_paid_without_slot_after_pay` | GET **without** `slot_key` after Test pay is `paid` | stranger vs owner; privacy |
| `Start_link_without_slot_key_is_400` | missing slot 400 contains `slot_key` | short/long keys; 409 vs 400 when full |
| `Public_get_does_not_need_bearer` | GET public, One not re-called | occupancy |

`PublicPayTests`: **no occupancy cases.** All standalone checkout tokens (`SeedCheckout`). Methods: `Public_get_does_not_need_bearer`, `Public_missing_is_404`, `Start_twice_returns_same_url_without_second_psp_http`, `Public_get_exposes_started_and_redirect_after_start`, `Start_paid_is_409`, `Start_paused_is_403_even_with_stored_url`, `Email_required_true_when_active_chip`, `Email_required_false_when_active_stripe`, `Start_without_rail_is_503`.

`TestRailTests.Mint_and_start_pays_without_keys` — standalone checkout, not a link.

`FillTests` / `WebhookTests` — standalone checkouts. No `PaymentLinkId`. No “fulfill when link already full”.

`CheckoutTests` — `POST /v1/checkouts` only.

`CatalogTests` — product create/member. No join to payment links.

Checkout SPA `locks.test.ts` — string presence.

Merchant `locks.test.ts` — `'test'`, `/payment-links`, `max_payers`, `1 person only`, `unlimited`. Does not lock dialog copy vs algorithm. Does not lock `taken / max` vs `paid`.

No unit tests of `PaymentLinkOccupancy` (class is `internal`; `InternalsVisibleTo` exists and is unused for this).

Factory: InMemory, `TransactionIgnoredWarning` swallowed. Unique filter not applied.

### Methods that should exist (names)

Host / occupancy (Postgres, not InMemory, for any race):

1. `Concurrent_two_slots_on_max_one_does_not_mint_two_open_children`
2. `Concurrent_same_slot_resumes_instead_of_500_or_two_rows`
3. `Chip_start_on_max_one_without_paying_409s_a_second_slot` — **the product-defining test**; today would **pass** against live code and **fail** the merchant sentence
4. `Chip_start_missing_email_does_not_take_a_seat` — today would **fail** (B3)
5. `Chip_start_rail_503_after_mint_still_holds_or_releases_the_seat` — decide, then lock
6. `Abandoned_open_child_expires_and_releases_capacity` — after a TTL exists; today would fail
7. `Cancel_url_does_not_release_a_seat` (characterization) **or** `Cancel_releases_if_unpaid` after a product choice
8. `Fulfill_webhook_does_not_pay_an_over_capacity_child` — after B7 is closed; today would fail
9. `Fulfill_webhook_on_reserved_open_child_does_not_change_taken_count`
10. `Test_rail_start_on_max_one_second_slot_is_409_full_not_second_receipt` — sequential already implied by `Two_people` with max=1; add explicit max=1 Test + second slot
11. `Get_full_is_200_not_409` — characterization of `Two_people` other-slot GET
12. `Start_full_is_409_not_400`
13. `Start_paid_child_is_409_checkout_not_open_and_get_returns_paid_for_that_slot`
14. `Unlimited_true_ignores_max_payers_zero`
15. `Create_max_payers_negative_is_400`
16. `Create_does_not_read_catalog_price_when_body_amount_differs` — snapshot honesty
17. `Link_amount_not_product_amount_is_what_stripe_charges`
18. `Paused_after_open_mint_keeps_or_releases_the_seat` — decide
19. `Expired_status_does_not_count_toward_capacity` — once expired is written
20. `CountsTowardCapacity_rejects_unknown_status` — unit
21. `IsFull_null_max_is_never_full` — unit
22. `Remaining_over_admit_is_zero_not_negative` — unit; `Math.Max` already
23. `Slot_key_length_7_is_400` / `Slot_key_length_8_mints`
24. `Payment_link_token_is_preferred_over_child_public_token`
25. `Child_token_start_does_not_mint_a_second_child`
26. `List_taken_counts_open_not_only_paid` — CHIP Start then list
27. `Merchant_max_one_started_unpaid_list_status_is_full_not_paid`
28. `Get_max_one_paid_without_slot_returns_another_payers_checkout` — characterization of the leak, or flip it
29. `Same_email_two_slots_takes_two_seats` — characterization of “not unique payer”
30. `Migrate_PaymentLinkPayers_is_applied_outside_Development` — once that is true

SPA (beyond string locks):

31. `Checkout_maps_409_full_to_Link_is_full_card`
32. `Checkout_maps_409_own_paid_slot_to_Payment_received`
33. `Checkout_does_not_show_Payment_received_for_a_stranger_on_a_one_person_link` — today would fail
34. `slotKey_is_stable_when_localStorage_throws` — today would fail
35. `Buyer_sees_remaining_when_api_sends_it` — today would fail (not rendered)

Merchant:

36. `Dialog_copy_matches_CountsTowardCapacity_or_algorithm_matches_copy` — today would fail
37. `Payers_column_uses_taken_for_unlimited_in_flight_or_documents_paid_only`
38. `Copy_disabled_or_still_enabled_when_status_full` — pick one

Fulfillment intersection (even if rails paper owns webhooks):

39. `FulfillPaidAsync_does_not_read_MaxPayers` — characterization until B7 is closed
40. `Over_capacity_second_webhook_does_not_mint_second_RCPT`

Do **not** pretend `PublicPayTests.Start_paid_is_409` covers pay-link occupancy. It seeds a standalone checkout.

---

## Ranked findings

| Rank | Id | Kind | Finding |
|------|----|------|---------|
| P0 | B1 | Bug | COUNT then INSERT without a lock over-admits concurrent Starts. Unique `(link, slot)` does not cap N. |
| P0 | B3 | Bug | Mint commits a seat before email/PSP can fail. A 400/503 fills a one-person link. |
| P0 | B4 | Bug | `open` never expires. Abandoned PSP / cancel under-admits everyone else. |
| P0 | B5 | Bug | Merchant: “closes after one **successful payment**.” Code: closes after one **start**. Unlimited vs limited columns disagree (paid vs taken). |
| P0 | B7 | Bug | Fulfillment pays every `open` child, including extras from B1. |
| P1 | B2 | Bug | Same-slot race 500s on Postgres; InMemory allows duplicates. |
| P1 | B6 | Bug | `localStorage` throw → new `slot_key` per call → payer loses their own seat/receipt. |
| P1 | B8 | Test | Occupancy tests use Test rail instant pay; they do not lock reservation vs paid. |
| P1 | G12 | Gap | Tests cannot see unique index or `FOR UPDATE`. |
| P1 | G1 | Gap | Product rule (reserve vs paid-only vs unique human) is not one sentence the UI and host share. |
| P2 | B9 | Bug | Pause after mint stuck-occupies. |
| P2 | B10 | Bug | Child public token is a second public door. |
| P2 | G2–G7 | Gap | No expire, cancel, TTL, seat list, remaining UX, close-link, PATCH cap. |
| P2 | G8 | Gap | Catalog amount/interval is not the charge; link snapshot is. |
| P2 | G11 | Gap | `MigrateAsync` Development-only; `PaymentLinkPayers` will not appear in Production boot. |
| P2 | G13 | Gap | `pay-spec` unaware (owned by 08). |
| P2 | G14–G16 | Gap | Two devices; no email uniqueness; no max N clamp. |

**What is not a bug (live and coherent):**

- GET full is **200** with `status: full`. POST start full is **409**. Missing `slot_key` is **400**.
- Default cap 1. `unlimited` → null max. `max_payers < 1` → 400 unless unlimited.
- Same slot sequential Start does not take two seats (CHIP test).
- Test rail Start fulfills in-process; success URL is verifying, not paid — SPA paid-check is the host GET.
- Amount charged is the **link snapshot**, copied onto the child, sent to the rail. That is a snapshot, not a catalog live price. Honest if you do not claim otherwise. Merchant dialog does not claim live catalog.

---

## How to solve (concurrency, product rules)

Do not implement in this paper. Sequence below is the design, not a patch.

### 1. Write the product rule in one place and make three surfaces quote it

Pick **one**:

**Rule A — Reserve at Start (what the host does today).**  
“How many people can *begin* paying.” `open`+`paid` count. Dialog must say: “The link closes after N people click Pay, including people who never finish.” Need TTL or cancel-to-release or merchants will trap seats. Buyer page should say “Someone already started this payment” vs “Sold out”.

**Rule B — Count paid only (what the dialog says today).**  
“How many people can *successfully* pay.” `CountsTowardCapacity` becomes `status == "paid"` only. Concurrent Starts on max=1 both reach the PSP. Webhook of the extra must **not** mint a second RCPT (refund / ignore / mark `rejected_over_capacity`). Merchant accepts possible PSP fees on the loser.

**Rule C — Unique payer (not implemented at all).**  
Seat key is email (or PSP customer id), not `slot_key`. `slot_key` remains a browser resume handle but two devices with the same email share one child. Hard for guests who mistype email; CHIP already requires email.

Recommendation given live code + Test rail + HitPay-ish SMB links: **Rule A with a short TTL**, because the host already reserved at Start and PSP sessions cost money. Then **change the dialog string** to match A. Do not keep A in C# and B in JSX.

If the business insists on the current sentence (“closes after one successful payment”), that is **Rule B**: change `PaymentLinkOccupancy.CountsTowardCapacity`, list `taken_count` meaning, Start full check, and add fulfill-time enforcement. Do not only change the string.

### 2. Concurrency for Rule A (reserve at Start)

The COUNT+INSERT must be atomic with the cap.

Minimum that actually works on Postgres:

```
BEGIN;
SELECT id, max_payers FROM payment_links WHERE id = $link FOR UPDATE;
SELECT count(*) FROM checkouts WHERE payment_link_id = $link AND status IN ('open','paid');
-- if full: ROLLBACK 409
INSERT checkout ...
COMMIT;
```

`FOR UPDATE` on the parent row serializes mint per link. Unlimited links still lock; acceptable at SMB volume. Resume path: `SELECT` by `(link, slot)` first **inside the same transaction**, or catch unique violation on `(PaymentLinkId, SlotKey)` and return the existing row (never 500).

Stronger, if you want a constraint the database can see without locking the parent:

- Table `payment_link_seats(link_id, n)` with `PRIMARY KEY (link_id, n)` and application `n` in `1..MaxPayers`, plus unlimited skipping this table; or
- Stored `taken_count` on `payment_links` with  
  `UPDATE payment_links SET taken_count = taken_count + 1 WHERE id = $id AND (max_payers IS NULL OR taken_count < max_payers) RETURNING *`  
  then insert checkout. Unique slot still required so resume does not increment twice.

Do **not** add a check-only in C# and call it done. B1 is a database problem.

Apply the unique index in **all** providers used by tests, or run occupancy tests on Testcontainers Postgres. InMemory `EnsureCreated` is why B1/B2 are invisible.

Catch `DbUpdateException` on slot unique → resume (B2).

### 3. Concurrency for Rule B (paid only)

Start stays racy on purpose. Enforcement moves to `FulfillPaidAsync`:

```
BEGIN;
SELECT … FOR UPDATE payment_links
paid = COUNT paid children
if max is int && paid >= max && this checkout is still open:
  do not fulfill; mark checkout rejected_over_capacity (or trigger refund job)
else:
  existing fulfill
COMMIT;
```

Webhook must not 500 (PSP retries). Return 200 with `ignored: over_capacity` after recording the event id so retries duplicate-ok.

This is a money decision. Rails/fulfillment paper owns refund mechanics; occupancy paper requires the **gate** to exist.

### 4. Stop occupying seats on failing Starts (B3)

Reorder `Start` for links:

1. Validate `slot_key`, paused, email-if-required, provider configured.
2. Then `MintOrResume` in the locked transaction.
3. Then PSP HTTP.
4. If PSP throws: either leave the reservation (Rule A, buyer can retry same slot) **or** delete/expire the child if no `ProviderSessionId` was obtained.

Never insert `open` then 400 email. SPA already withholds the click; the API must match.

Comment already on Start (`PublicPayEndpoints.cs:169-171`) about double PSP session if SaveChanges fails after HTTP — occupancy-adjacent: a retry with a **new** slot would take a second seat. Same-slot retry is the intended recovery; lock it with a test.

### 5. Abandoned starts (B4)

If Rule A:

- Write `expired` (or `abandoned`) from a worker: `open` AND `CreatedAt < now - TTL` AND no `paid`. TTL e.g. 30–60 minutes aligned with PSP session lifetime where known.
- `CountsTowardCapacity` stays `open or paid`; expired drops out.
- Merchant “release” button: same status write.
- Cancel URL: optional POST `/v1/pay/{token}/abandon` with `slot_key` that sets expired **only if still open and unpaid**. Do not expire from a GET.

If Rule B: abandoned `open` does not block anyone; still garbage-collect PSP sessions so you do not pay for infinite hosted invoices.

The `'expired'` branches in Start/Get are waiting for this. Implement them or delete them; dead status strings are how UI lies start.

### 6. UI honesty (B5, G5, G6)

Merchant:

- Dialog helper must quote the chosen rule (A or B) in the same words as `CountsTowardCapacity`.
- Payers column: one definition. Suggested: `paid / max` plus a muted `n started` if Rule A and `taken > paid`. Unlimited: `n paid · unlimited` and if Rule A `m started`.
- Stop remapping `full`→`paid` unless the API status becomes `paid`. Prefer host `status` to grow a third value `paid` when `max==1 && paid>=1`, so merchant and public GET match.
- Copy/Open when full: keep Copy so the slot owner can return; badge the row `full` / `paid`.

Buyer:

- Render `remaining` when not null (“2 payments left”).
- Do not show “Payment received” unless `mine` is paid **or** (if you keep the max=1 convenience) you accept the leak. Safer: max=1 paid + unmatched slot → “Link is full” / “This payment was already completed”, **not** the thank-you card with amount.
- 409 path is fine (refetch GET). Add 409 `detail` switch: `full` vs `Checkout is not open`.
- `slotKey`: if `setItem` throws, keep the in-memory UUID for the lifetime of the page module (module-level `Map<token, string>`) so GET and POST match even when storage dies.

### 7. Fulfillment intersection (B7)

Even under Rule A, after a lock on mint, fulfill should still be occupancy-neutral. After a **failed** lock (legacy extra rows), fulfill should refuse extras:

- If `PaymentLinkId` set and `IsFull(max, count paid excluding this id? wait)` — simpler: if `MaxPayers` is int and `COUNT(paid) >= MaxPayers` and this row is still `open`, do not pay.

That closes the window between old over-admitted rows and new webhooks.

Fulfillment must load the parent link when `checkout.PaymentLinkId` is set. Today it never does.

### 8. Snapshot vs catalog (G8)

Pick: “pay links freeze amount at mint of the **link**.” Document on the dialog (“Amount is fixed on this URL”). Do not read `prices` on Start unless you add “update link amount” which existing children would disagree with.

Align currency: payment-link create should reject non-MYR the same as catalog, or catalog should not claim Bar B MYR alone.

Stop creating a new product on every dialog if the label is only a display string — or keep it but do not pretend it is inventory.

### 9. Migration (G11)

`Database.MigrateAsync()` on boot for every environment that owns this database, or a documented `dotnet ef database update` in the Pay image entrypoint. Additive migration is safe on existing checkout rows (null columns). Do not EnsureCreated in Production.

Keep the filtered unique index. Add FK `checkouts.PaymentLinkId` → `payment_links.Id` when you are sure no orphans exist (new table; should be clean).

### 10. Tests

Move occupancy tests that claim the cap onto Postgres. Keep InMemory for authz/health. Add the method names in “Tests vs missing”, especially `Chip_start_on_max_one_without_paying_409s_a_second_slot` as a characterization test **before** changing Rule A/B so the next patch does not “fix” the dialog by accidentally switching rules.

`PublicPayTests` should grow link-token twins of `Start_paid_is_409` and paused, with `slot_key`. Today those methods do not touch `PaymentLinkId`.

### 11. Two mint doors

Leave `POST /v1/checkouts` as the kernel one-off session (no occupancy). Pay-links are the shared URL with a cap. Do not route merchant at the old door (already true). Do not apply `MaxPayers` to standalone checkouts.

---

## Refuse

- This file is analysis. It does not patch C#, TypeScript, tests, or specs.
- Processor vault details, rail HTTP vs Hub, TypeSpec-as-a-paper: out of scope. `pay-spec` silence is named as G13 only.
- No choice of CHIP vs Stripe vs Test beyond how Test **instant-pay** hides reservation.
- No Hub `lazuar-api` payment-link leftovers (`RazorpayGatewayAdapter.BuildPaymentLinkRequest`, comms `UpdatePaymentLink`). Those are not this host.
- No flip of 011 checklist cells.
- Do not “fix” occupancy by counting paid only in the SPA while the host still counts `open`.

---

## Appendix: quoted evidence

### Occupancy class (started + paid)

```5:12:apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkOccupancy.cs
    public static bool CountsTowardCapacity(string status) =>
        status is "open" or "paid";

    public static bool IsFull(int? maxPayers, int taken) =>
        maxPayers is int max && taken >= max;

    public static int? Remaining(int? maxPayers, int taken) =>
        maxPayers is int max ? Math.Max(0, max - taken) : null;
```

### Create: unlimited vs default 1 vs `max_payers`

```73:85:apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs
        int? maxPayers;
        if (body.Unlimited)
        {
            maxPayers = null;
        }
        else
        {
            maxPayers = body.MaxPayers ?? 1;
            if (maxPayers < 1)
            {
                return PayErrors.Status(400, "Bad Request", "max_payers must be at least 1");
            }
        }
```

Link list status is computed, never stored:

```156:175:apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs
    static PaymentLinkView Map(PaymentLinkRow row, int taken, int paid, string? label = null)
    {
        var remaining = PaymentLinkOccupancy.Remaining(row.MaxPayers, taken);
        var full = PaymentLinkOccupancy.IsFull(row.MaxPayers, taken);
        return new PaymentLinkView
        {
            ...
            Status = full ? "full" : "open",
            ...
            MaxPayers = row.MaxPayers,
            Unlimited = row.MaxPayers is null,
            PaidCount = paid,
            TakenCount = taken,
            Remaining = remaining,
```

### Start: 400 slot vs 409 full; insert open then return

```219:264:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
        var slot = NormalizeSlotKey(body?.SlotKey);
        if (slot is null)
        {
            return (null, PayErrors.Status(400, "Bad Request", "slot_key is required"));
        }

        var existing = await db.Checkouts.FirstOrDefaultAsync(x => x.PaymentLinkId == link.Id && x.SlotKey == slot, ct);
        if (existing is not null)
        {
            if (existing.Status is "paid" or "expired")
            {
                return (null, PayErrors.Status(409, "Conflict", "Checkout is not open"));
            }

            return (existing, null);
        }

        var taken = await db.Checkouts.CountAsync(
            x => x.PaymentLinkId == link.Id && (x.Status == "open" || x.Status == "paid"),
            ct);
        if (PaymentLinkOccupancy.IsFull(link.MaxPayers, taken))
        {
            return (null, PayErrors.Status(409, "Conflict", "This pay link is full"));
        }
        ...
            Status = "open",
        ...
        db.Checkouts.Add(row);
        await db.SaveChangesAsync(ct);
        return (row, null);
```

Email check **after** that return, in `Start`:

```146:149:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
        if (PayProviders.RequiresEmail(name) && !BuyerEmail.IsUsable(row.PayerEmail))
        {
            return PayErrors.Status(400, "Bad Request", "email is required");
        }
```

```35:36:apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs
    public static bool RequiresEmail(string provider) =>
        provider is not Stripe and not Test;
```

### GET full: 200; max=1 paid leak

```66:75:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
        if (PaymentLinkOccupancy.IsFull(link.MaxPayers, taken))
        {
            if (link.MaxPayers == 1 && paid >= 1)
            {
                var paidRow = children.First(c => c.Status == "paid");
                return CheckoutView(link.PublicToken, paidRow, remaining, link.MaxPayers, paid, taken);
            }

            return LinkView(link, "full", remaining, paid, taken, started: false, redirectUrl: null);
        }
```

### Unique is per slot, Npgsql only

```43:48:apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs
            if (Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
            {
                e.HasIndex(x => new { x.PaymentLinkId, x.SlotKey })
                    .IsUnique()
                    .HasFilter("\"SlotKey\" IS NOT NULL");
            }
```

Migration equivalent: `20260825120000_PaymentLinkPayers.cs:69-75`.

### Fulfillment does not know links

```26:37:apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs
        if (checkout.Status != "open")
        {
            return;
        }
        ...
        checkout.Status = "paid";
```

### Test rail instant pay (why occupancy tests look like paid-only)

```11:21:apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestHosted.cs
    public Task<HostedSession> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct)
    {
        ...
        return Task.FromResult(new HostedSession(
            CheckoutUrls.Success(checkout, config, env),
            "test:" + checkout.Id));
    }
```

```176:186:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
            if (PayProviders.IsTest(name))
            {
                db.PspWebhookEvents.Add(...);
                await fulfillment.FulfillPaidAsync(row.Id, name, hosted.ProviderSessionId, ct);
            }
```

### Merchant dialog vs table

```89:101:apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx
function payersLabel(row: PayLink): string {
  if (row.unlimited || row.max_payers == null) {
    const paid = row.paid_count ?? 0
    return paid === 0 ? 'Unlimited' : `${paid} paid · unlimited`
  }
  const taken = row.taken_count ?? 0
  return `${taken} / ${row.max_payers}`
}

function statusLabel(row: PayLink): string {
  if (row.status === 'full' && row.max_payers === 1 && (row.paid_count ?? 0) >= 1) return 'paid'
  return row.status
}
```

```397:400:apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx
                  {capacity === 'one'
                    ? 'The link closes after one successful payment.'
                    : 'Anyone with the URL can pay. It does not close on its own.'}
```

### Buyer full card; slot persistence; 409

```135:139:apps/lazuar-pay-checkout/src/App.tsx
      if (response.status === 409) {
        const again = await fetch(payPath(token))
        if (again.ok) setPay((await again.json()) as PayView)
        else setError(detail ?? 'This pay link is full')
```

```226:239:apps/lazuar-pay-checkout/src/App.tsx
  if (pay.status === 'full') {
    return (
      <Shell>
        <Card>
          ...
            <CardTitle className="text-xl">Link is full</CardTitle>
            <CardDescription>This pay link has no remaining payments.</CardDescription>
```

### Migrate Development only; tests InMemory

```73:78:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<PayDbContext>().Database.MigrateAsync();
}
```

```53:54:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs
            services.AddDbContext<PayDbContext>(o => o.UseInMemoryDatabase(_dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
```

### Catalog MYR vs link currency

```32:36:apps/lazuar-pay/src/Lazuar.Pay/Catalog/CatalogEndpoints.cs
        var currency = string.IsNullOrWhiteSpace(body?.Currency) ? "MYR" : body!.Currency!.Trim().ToUpperInvariant();
        if (currency != "MYR")
        {
            return PayErrors.Status(400, "Bad Request", "Bar B currency is MYR");
        }
```

Payment-link create (`PaymentLinkEndpoints.cs:87`) uppercases default MYR and does not reject others. Child copies `link.Amount` / `link.Currency` (`PublicPayEndpoints.cs:254-255`). Stripe charges `checkout.Amount` (`StripeHosted.cs:29-45`).

### Sequential occupancy test (Test rail)

```122:145:apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs
    public async Task Two_people_can_pay_a_link_of_two
    {
        ...
        var (token, _) = await PayTest.SeedPaymentLink(client, maxPayers: 2);
        var a = await PayTest.StartPay(client, token, "slot-aaa-1");
        ...
        var b = await PayTest.StartPay(client, token, "slot-bbb-2");
        ...
        var c = await PayTest.StartPay(client, token, "slot-ccc-3");
        Assert.That(c.StatusCode, Is.EqualTo(HttpStatusCode.Conflict), ...);
        Assert.That(await c.Content.ReadAsStringAsync(), Does.Contain("full"));
        ...
        Assert.That(paidDoc.RootElement.GetProperty("status").GetString(), Is.EqualTo("paid"));
        ...
        Assert.That(otherDoc.RootElement.GetProperty("status").GetString(), Is.EqualTo("full"));
```

`SeedPaymentLink` default provider is `"test"` (`PayTest.cs:49-50`).

### Same slot does not double-count (CHIP, the only `open`-occupancy lock)

```148:171:apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs
    public async Task Same_slot_start_twice_does_not_take_two_seats
    ...
        Assert.That(factory.Psp.SendCount, Is.EqualTo(1));
        ...
        Assert.That(doc.RootElement[0].GetProperty("taken_count").GetInt32(), Is.EqualTo(1));
        Assert.That(doc.RootElement[0].GetProperty("remaining").GetInt32(), Is.EqualTo(1));
```

### `expired` is read, never written (host)

Grep of `apps/lazuar-pay/src`: status `"expired"` only in `PublicPayEndpoints.cs:116` and `:228`. No assignment `Status = "expired"`. No `canceled`.

### Snapshot of max-payers column

`PayDbContextModelSnapshot.cs:417-418` `MaxPayers` integer nullable on `PaymentLinkRow`. `checkouts` `PaymentLinkId` + `SlotKey` + unique filter at `152-154`.
