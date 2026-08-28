# 07 — Money leftover after 002 occupancy / fulfill / webhook work

**Date:** 28 August 2026  
**Branch:** `fix/002-pay-host-bugs`  
**HEAD:** `6d730d15` — `fix(pay): store per-org One webhook secrets`  
**Type:** Uncondensed evaluation. **Not** an implementation. **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md) Status cells. **Not** a project reference into `apps/lazuar-api`.

**Slice:** Money leftover after 002 occupancy/fulfill/webhook work — what still blocks production money, and what second apps will need (refunds, cancel, expire, subscriptions) that is missing.

Live files on **this SHA** are authority. [issues/002](../../issues/002/README.md) claims 001–080 resolved on this branch. That YAML is a tracker, not a proof. This paper re-opens the money files and names what is closed, what YAML over-claims, and what is still a hole. [019-evals/05-payment-links-occupancy.md](../019-evals/05-payment-links-occupancy.md) and [019-evals/06-rails-webhooks-fulfillment.md](../019-evals/06-rails-webhooks-fulfillment.md) froze occupancy as P0 on `9f04ad58`. This paper is the `6d730d15` re-read.

Standing law this report does not weaken:

- One Pay binary, one Pay database. Bezos is the door (`/v1`); Linux is the room (in-process).
- Pay talks to One over HTTP. No PAT, no OpenFGA admin, no `SELECT` from One.
- Buyers are not One humans.
- Receipt ≠ tax invoice. SST / LHDN stay off the pay path.
- Steal HTTP **judgment** from Hub; Hub `apps/lazuar-api` / ops :3003 / portal :3004 stay museum.
- IsolationTests stay red on cathedral strings (`MediatR`, `IEnumerable<IHostedRail>`, Hub `@repo/api-types-ts`).

---

## 0. Coordinates

| Item | Value on `6d730d15` |
|------|---------------------|
| Host | `apps/lazuar-pay` — focused C# host, public schema, Postgres 5435 in local compose, listen **8081** |
| Occupancy class | `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkOccupancy.cs` |
| Mint (capacity) | `POST /v1/payment-links` → `PaymentLinkRow.MaxPayers` |
| Mint (no capacity) | `POST /v1/checkouts` → standalone `CheckoutRow`, `PaymentLinkId` null |
| Public GET / start | `GET\|POST /v1/pay/{token}` in `PublicPayEndpoints.cs` |
| Fulfill | `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs` |
| Plane B | `POST /v1/webhooks/{provider}/{orgId}` in `WebhookEndpoints.cs` |
| Rails | `stripe` `chip` `billplz` `xendit` `razorpay` `test`. Capability string is `hosted_link`. |
| Unique fulfill belt | Migration `20260828001217_FulfillmentUniques` — unique `charges(CheckoutId)`, unique `documents(CheckoutId)`, unique `documents(OrgId, Number)` |
| TTL config | `Pay:ReservationTtlMinutes` default **30** in `appsettings.json` |
| Start rate limit | `Pay:StartMaxPerMinute` default **20** (`PublicPayLimiter`) |
| Tests | `PaymentLinkTests`, `FillTests`, `PostgresTxTests`, `PublicPayTests`, `TestRailTests`, `WebhookTests`, `CatalogTests`, `CheckoutTests`, `PaymentQueryTests`, per-rail `*RailTests` |
| Spec | `packages/pay-spec/main.tsp` — no refund, no cancel, no expire, no subscription, no `provider_ref` lookup |
| Isolation | `IsolationTests` bans `MediatR`, `IEnumerable<IHostedRail>`, Hub Payments factory, LHDN |

JSON on the wire is snake_case (`Program.cs` `JsonNamingPolicy.SnakeCaseLower`). Field names below are C# in source and snake_case on the wire.

Parent 019 verdict on `9f04ad58` was: **the new P0 is occupancy**. Count-then-insert, Test unsigned in every non-Production env, Stripe `payment_status` unread, no unique charge. This SHA closed those as a **hosted cashier**. It did not grow a refund API, a cancel door, a subscription engine, or a kernel lookup by `provider_ref`. Those absences are **missing features**, not regressions — except where live code still races or still lies. This paper separates them.

---

## 1. Verdict (money only)

002 made the hosted cashier **honest enough to take first-party one-off money** on a capped pay link, if you accept these product rules:

1. A payer is an `open` reservation **or** a `paid` child.
2. Unpaid `open` older than 30 minutes becomes `expired` **lazily** on GET/start of that link. There is no worker.
3. Fulfillment will not mint a second Official Receipt on the same checkout (unique `charges.CheckoutId` + in-process `SemaphoreSlim`).
4. Fulfillment will not pay a link-child once `paid` seats already meet `MaxPayers` (sequential belt; parent `FOR UPDATE` is on **start**, not on fulfill).
5. Test rail exists only in environments named `Development` or `Testing`. Staging named `Staging` and Production refuse it.
6. Capability is `hosted_link`. Start creates a processor hosted session. Plane B verifies HMAC and amount/currency then fulfills. There is no capture/partial-capture/refund/dispute verb on this host.

That is a **cashier**, not a payments kernel. A second app that needs “refund this `pay_…`”, “cancel the unpaid checkout”, “look up by CHIP purchase id”, “list paid since Tuesday”, or “bill monthly” will clone this repo and invent those doors, or it will not integrate.

**Production money (first-party dogfood One + Pay merchant + Pay checkout) is no longer blocked by 019’s occupancy P0.** It is still blocked by:

- No refund path when TTL expires a child whose PSP session the buyer later completes (late webhook sees `expired`, returns without paying, money sits at the processor).
- PSP HTTP then persist still minting a second hosted session on CHIP/Billplz/Xendit/Razorpay if `SaveChanges` fails after the processor already created one (014 live; YAML says resolved).
- Catalog `product_id` on **checkout** mint is still a label sidecar; payment-link mint now 400s amount drift, checkout mint does not (023 leftover).
- Amount/currency mismatch 400 still does **not** consume the event (015 live, **by design**). Hostile payloads retry forever; a wrong unit map of ours would stall a paid buyer with no receipt. Xendit and Razorpay still have no mismatch fixture.
- Fulfillment occupancy re-check is count-then-act **without** parent `FOR UPDATE`. If extras exist (legacy rows, 014 second session, grief), two concurrent extra-child fulfills can both see `paid = 0` and both mint receipts. Sequential test is green.
- Expire is lazy. A 1-person CHIP link abandoned for 31 minutes stays `full` until **someone** GETs or starts that token. There is no `IHostedService`.
- `slot_key` is still client-supplied. Rate limit 20/min per token is a grief brake, not a seat mint. YAML 019 “resolved” is a mitigation, not a server-issued slot.
- Test start **is** fulfill. Fine for laptop dogfood. Not a production rail. Overview copy “Test is always available” is honest **only** when the host listed Test (`testListed`).

**Must not say on this SHA:** we have refunds; we have subscriptions; we are a Stripe-shaped kernel; catalog prices every mint door; Official Receipt is an e-invoice; 001–080 YAML means the live files have no money holes; InMemory tests prove the occupancy transaction.

**May say:** a capped pay link under two simultaneous Pays admits one PSP session (CHIP FakePsp + InMemory `SemaphoreSlim` **and** Postgres `FOR UPDATE` Testcontainers). Abandoned CHIP starts expire after 30 minutes **on the next GET/start**. Over-admit leftover displays `over_capacity` and remaining `-1`. Merchant copy says start + 30 minutes, not “successful payment”. Stripe unpaid `checkout.session.completed` is ignored; `async_payment_succeeded` pays. Unique charge-per-checkout exists. Test is off in Production and in an environment named `Staging`.

---

## 2. Occupancy product rule as LIVE

### 2.1 The class is the rule

`PaymentLinkOccupancy` is no longer a pair of helpers. On this SHA it owns the product rule, the in-process gate, the parent row lock, and the two expire writers. Quote the whole live type, because 019 quoted a 13-line helper and that file is gone.

```1:114:apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkOccupancy.cs
/// <summary>
/// A payer is an <c>open</c> reservation or a <c>paid</c> child.
/// Unpaid <c>open</c> rows older than <see cref="ReservationTtl"/> become <c>expired</c>
/// and no longer occupy. Do not invent expiry in the SPA.
/// </summary>
internal static class PaymentLinkOccupancy
{
    public const int DefaultReservationTtlMinutes = 30;

    static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

    public static bool CountsTowardCapacity(string status) =>
        status is "open" or "paid";

    public static bool IsFull(int? maxPayers, int taken) =>
        maxPayers is int max && taken >= max;

    public static bool IsOverCapacity(int? maxPayers, int taken) =>
        maxPayers is int max && taken > max;

    public static string MerchantStatus(int? maxPayers, int taken)
    {
        if (IsOverCapacity(maxPayers, taken))
        {
            return "over_capacity";
        }

        return IsFull(maxPayers, taken) ? "full" : "open";
    }

    /// <summary>Buyer remaining is clamped. Merchant list uses <see cref="RemainingUnclamped"/> so over-admit is visible.</summary>
    public static int? Remaining(int? maxPayers, int taken) =>
        maxPayers is int max ? Math.Max(0, max - taken) : null;

    public static int? RemainingUnclamped(int? maxPayers, int taken) =>
        maxPayers is int max ? max - taken : null;
    // ... ReservationTtl, SerializeAsync, LockParentAsync, ExpireStaleAsync, ExpireOpenAsync
}
```

Write the rule in one paragraph so the rest of the paper can quote it:

**A seat is a child checkout in `open` or `paid`. `expired` does not occupy. Cap N is `MaxPayers`. Null `MaxPayers` is unlimited. Occupancy is always a `COUNT`, never a stored remaining column. Merchant list remaining is unclamped (can be negative) and status becomes `over_capacity` when `taken > max`. Buyer remaining is clamped at 0. TTL is 30 minutes from `CreatedAt`, applied lazily. Pause expires every `open` child on the next public GET. The SPA must not invent expiry.**

That is 003+004+079 as live code. 019’s “`expired` is read, never written” is **false** on this SHA. Writers:

- `ExpireStaleAsync` — `open` and `CreatedAt < UtcNow - ttl` → `expired`
- `ExpireOpenAsync` — every `open` on the link → `expired` (pause path)
- `ExpireFailedReservation` — start failed after mint, no `PspRedirectUrl` yet → `expired`
- `FulfillPaidCoreAsync` — link already at paid cap → extra child `expired` instead of paid

### 2.2 SemaphoreSlim + FOR UPDATE

Start serializes occupancy **twice**: an in-process `SemaphoreSlim` per `linkId`, then a Postgres `SELECT … FOR UPDATE` on the parent `payment_links` row inside a relational transaction.

```50:74:apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkOccupancy.cs
    public static async Task<T> SerializeAsync<T>(string linkId, Func<Task<T>> work, CancellationToken ct)
    {
        var gate = Gates.GetOrAdd(linkId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await work().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public static async Task LockParentAsync(PayDbContext db, string linkId, CancellationToken ct)
    {
        if (db.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) != true)
        {
            return;
        }

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT 1 FROM public.payment_links WHERE "Id" = {linkId} FOR UPDATE""",
            ct).ConfigureAwait(false);
    }
```

`MintOrResume` is the money path. Email / callback-base / paused / provider are validated **before** the insert. Then:

```289:350:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
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
                    // resume
                    ...
                }

                var taken = await db.Checkouts.CountAsync(
                    x => x.PaymentLinkId == link.Id && (x.Status == "open" || x.Status == "paid"),
                    ct);
                if (PaymentLinkOccupancy.IsFull(link.MaxPayers, taken))
                {
                    return ((CheckoutRow?)null, PayErrors.Status(409, "Conflict", "This pay link is full"));
                }
                // insert open child, SaveChanges, Commit
```

Same-slot unique violation is no longer a 500. The catch resumes the raced row or 409s full:

```354:368:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
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
```

Unique `(PaymentLinkId, SlotKey)` is **still** Npgsql-only. InMemory tests do not install it. That is why 001’s concurrent proof had to grow `PostgresTxTests` and why InMemory concurrent tests lean on `SemaphoreSlim`.

```43:48:apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs
            if (Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
            {
                e.HasIndex(x => new { x.PaymentLinkId, x.SlotKey })
                    .IsUnique()
                    .HasFilter("\"SlotKey\" IS NOT NULL");
            }
```

There is still **no** unique constraint on the Nth seat. Two different `slot_key`s are allowed until `IsFull` says no. The cap is the lock + count, not a `UNIQUE (payment_link_id, seat_n)`. That is acceptable if `FOR UPDATE` holds. It is not a belt if a replica skips the lock (InMemory, or a future code path that inserts children without `MintOrResume`).

`LockParentAsync` no-ops when the provider name does not contain `Npgsql`. Hermetic InMemory occupancy therefore proves the **gate**, not the row lock. The comment on `PayApiFactory` still says it:

```35:40:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs
    /// <summary>When set, tests run against Npgsql. InMemory is not a transaction proof — see PostgresTxTests.</summary>
    public string? PostgresConnection { get; init; }
```

### 2.3 Open TTL, expire on pause

TTL is configuration, floor 1 minute, default 30:

```44:48:apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkOccupancy.cs
    public static TimeSpan ReservationTtl(IConfiguration config)
    {
        var minutes = config.GetValue("Pay:ReservationTtlMinutes", DefaultReservationTtlMinutes);
        return TimeSpan.FromMinutes(Math.Max(1, minutes));
    }
```

```13:15:apps/lazuar-pay/src/Lazuar.Pay/appsettings.json
  "Pay": {
    "ReservationTtlMinutes": 30
  }
```

Public GET runs expire **before** painting occupancy. Pause is the stronger writer: every `open` child dies, not only stale ones.

```68:82:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
        await PaymentLinkOccupancy.SerializeAsync(link.Id, async () =>
        {
            var settings = await db.OrgSettings.FindAsync([link.OrgId], ct);
            if (settings?.ChargesPaused == true)
            {
                await PaymentLinkOccupancy.ExpireOpenAsync(db, link.Id, ct);
            }
            else
            {
                await PaymentLinkOccupancy.ExpireStaleAsync(
                    db, link.Id, PaymentLinkOccupancy.ReservationTtl(config), ct);
            }

            return 0;
        }, ct);
```

Start also expires stale inside the locked TX (quote in §2.2). There is **no** `IHostedService`, **no** `BackgroundService`, **no** `AddHostedService` in `apps/lazuar-pay/src`. Grep of those tokens in the host is empty. Abandoned seats recover when a buyer or a crawler hits the token, not at minute 31 on the clock.

That is a remaining product hole, not a lie: 003’s suggested fix was “TTL reservation”. Live implements TTL **on demand**. A 1-person CHIP link that nobody revisits stays `taken_count = 1`, `paid_count = 0`, `status = full` for the merchant list until GET/start. Merchant list **does not** expire. List is `AsNoTracking` count of current statuses. Staff can see a “full” link that would reopen if a buyer loaded it. Name this: **lazy TTL, merchant list is stale relative to GET.**

Pause test `Pause_expires_open_reservations` proves GET recovers remaining after `ChargesPaused = true`. Start after pause is 403 at the top of `MintOrResume`. Webhook while paused is 409 and does **not** consume the event (`Paused_org_does_not_mint_receipt`). Residual window: Start checks pause **then** enters `SerializeAsync`. A pause that lands between the check and the insert can mint an `open` child on a paused org. Next GET expires it. P2, not a double-receipt.

### 2.4 `over_capacity` display

019 B11: remaining clamped to 0, status silently `full` after over-admit. Live merchant list:

```176:193:apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs
    static PaymentLinkView Map(PaymentLinkRow row, int taken, int paid, string? label = null)
    {
        return new PaymentLinkView
        {
            ...
            Status = PaymentLinkOccupancy.MerchantStatus(row.MaxPayers, taken),
            ...
            PaidCount = paid,
            TakenCount = taken,
            Remaining = PaymentLinkOccupancy.RemainingUnclamped(row.MaxPayers, taken),
            Label = label
        };
    }
```

Buyer GET still uses clamped `Remaining`. That is intentional: strangers should not see `-1`. Staff should. Merchant SPA:

```9:25:apps/lazuar-pay-merchant/src/lib/occupancyDisplay.ts
export function occupancyOverCapacity(row: OccupancyRow): boolean {
  return row.status === 'over_capacity'
}

export function occupancyStatusLabel(row: OccupancyRow): string {
  if (row.status === 'over_capacity') return 'over capacity'
  if (row.status === 'full' && row.max_payers === 1 && (row.paid_count ?? 0) >= 1) return 'paid'
  return row.status
}

export function occupancyPayersLabel(row: OccupancyRow): string {
  const taken = row.taken_count ?? 0
  if (row.unlimited || row.max_payers == null) {
    return taken === 0 ? 'Unlimited' : `${taken} started · unlimited`
  }
  return `${taken} / ${row.max_payers}`
}
```

Unlimited column now uses **taken** (starts), not paid-only. 004’s mixed definition is closed on the table. Banner when any row is `over_capacity`:

```229:233:apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx
      {links.some(occupancyOverCapacity) ? (
        <p role="alert" className="text-sm text-red-700">
          A pay link has more payers than its cap. Money already moved — this is leftover over-admit, not a designed full link.
        </p>
      ) : null}
```

Dialog copy no longer says “successful payment”:

```424:426:apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx
                  {capacity === 'one'
                    ? 'The link closes after one person starts Pay. Unpaid starts free after 30 minutes.'
                    : 'Anyone with the URL can pay. It does not close on its own.'}
```

Locks:

```104:106:apps/lazuar-pay-merchant/src/locks.test.ts
    expect(src).toContain('The link closes after one person starts Pay. Unpaid starts free after 30 minutes.')
    expect(src).not.toContain('The link closes after one successful payment.')
    expect(src).toContain('started · unlimited')
```

Buyer full card says seats, not payments:

```332:333:apps/lazuar-pay-checkout/src/App.tsx
            <Heading>Link is full</Heading>
            <CardDescription>This pay link has no remaining seats.</CardDescription>
```

`locks.test.ts` on checkout greps `no remaining seats` and `not.toContain('no remaining payments')`. 004 is closed as copy. The grain in the host is still start-or-paid, which now matches the dialog.

Max=1 paid stranger GET is `already_paid`, not a thank-you leak of payer email:

```98:106:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
        if (PaymentLinkOccupancy.IsFull(link.MaxPayers, taken))
        {
            if (link.MaxPayers == 1 && paid >= 1)
            {
                return LinkView(link, "already_paid", remaining, paid, taken, started: false, redirectUrl: null);
            }

            return LinkView(link, "full", remaining, paid, taken, started: false, redirectUrl: null);
        }
```

Test: `One_person_link_shows_already_paid_without_slot_after_pay` asserts `mine: false` and no `payer_email`. Slot owner still sees `paid` via `?slot_key=`.

### 2.5 Child token aliases parent

032 on `9f04ad58`: child `PublicToken` was a second pay URL without occupancy fields; `POST /start` on the child skipped `slot_key` and occupancy. Live GET:

```41:56:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
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
                return await GetLink(parent, row.SlotKey ?? slot_key, db, config, ct);
            }
        }
```

Child GET is an alias of the parent occupancy view, with the child’s slot. Test `Child_public_token_loads_parent_occupancy` asserts `remaining`, `max_payers`, `taken_count` on the child token after a CHIP start.

**Start on the child token is still the standalone branch.** `Start` looks up payment-links by token first; a child token misses, then `store.GetByPublicTokenAsync`, then proceeds **without** `MintOrResume` occupancy. The seat already exists, so this does not over-admit by itself. It **does** skip `slot_key` and can call `CreateHostedUrlAsync` on that child. Combined with 014, a leaked child token is a retry of PSP HTTP. 032 is half-closed: GET aliases, POST start does not re-enter the parent lock. Document the namespace: link tokens and checkout tokens share `/v1/pay/{token}`; start-on-child is “resume this seat”, not “take a new one”.

Children still mint a 64-hex `PublicToken` at insert (`MintOrResume`). They are not 404. Merchant checkout list no longer leaks them (see §2.6).

### 2.6 List one-off only

031 on `9f04ad58`: `GET /v1/orgs/{orgId}/checkouts` mixed one-off mints and occupancy children. Live:

```158:161:apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs
        var rows = await db.Checkouts.AsNoTracking()
            .Where(x => x.OrgId == orgId && x.PaymentLinkId == null)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
```

Test: `List_omits_payment_link_children` — seed one-off + start a link child, list length 1, status `open` (the one-off, because Test start on the link would have paid the child). TypeSpec still **lies**:

```316:319:packages/pay-spec/main.tsp
  /** Member list. Mixes one-off mints and payment-link children. */
  @get
  @route("/orgs/{orgId}/checkouts")
  list(@path orgId: string): CheckoutListItem[];
```

Honesty leftover in the contract paper’s sibling file. Host wins. A kernel client that trusts the tsp comment will expect children and not find them. That is a spec bug, not a money bug. Named here because second-app list shape is money-adjacent.

Pay-link list is a separate door: `GET /v1/orgs/{orgId}/payment-links` with `taken_count` / `paid_count` / `remaining` / `status`. Merchant Vite lists **that**, not checkouts. Kernel clients must not sum both.

### 2.7 Seat reserved before start can succeed — closed on the 400 paths 002 named

Email required is checked **inside** `MintOrResume` before insert, and again after for the standalone branch.

```275:278:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
        if (PayProviders.RequiresEmail(providerName) && !BuyerEmail.IsUsable(body?.Email))
        {
            return (null, PayErrors.Status(400, "Bad Request", "email is required"));
        }
```

`CheckoutUrls.Base` throw is caught before insert (022). After insert, PSP/Stripe failures expire the reservation **if** `PspRedirectUrl` is still empty:

```373:384:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
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
```

Tests that close 002:

- `Chip_start_without_email_does_not_occupy_the_only_seat` — 400, `Psp.SendCount = 0`, remaining 1, second slot 200.
- `Billplz_localhost_callback_400_frees_the_seat` — 400 `callback base`, remaining 1, taken 0.

If CHIP HTTP **succeeds** and then `SaveChanges` of `PspRedirectUrl` fails, `ExpireFailedReservation` will **not** fire (URL already set on the tracked entity even if persist failed — actually if SaveChanges throws, the in-memory row has `PspRedirectUrl` set). The method checks `!string.IsNullOrWhiteSpace(row.PspRedirectUrl)` and returns. That is 014, not 002. The seat stays `open` with a processor session that may not be stored. Retry mints a second CHIP purchase. See §3.7 / §6.

### 2.8 Remaining races (occupancy)

Closed under one process + Postgres:

- Two different slots, `max_payers = 1`, concurrent start → one 200, one 409, one PSP HTTP. Tests: `Concurrent_start_on_one_person_link_admits_one_psp` (InMemory + `SemaphoreSlim` + CHIP FakePsp sleep 120ms), `Concurrent_starts_on_one_seat_leave_one_open` (Testcontainers Postgres + Test rail), `Concurrent_test_start_on_one_person_link_mints_one_receipt`.
- Same slot sequential → one PSP HTTP. `Same_slot_start_twice_does_not_take_two_seats`.
- Same slot concurrent → in-process gate serializes; multi-process parent `FOR UPDATE` serializes; unique `(PaymentLinkId, SlotKey)` + `DbUpdateException` resume. **No dedicated Postgres same-slot race test.** Residual: if someone removes the gate and the unique, 013 returns.

Still open:

1. **`SemaphoreSlim` is process-local.** Two Pay replicas minting the same link rely entirely on `FOR UPDATE`. That is the correct Linux-room design **if** every insert goes through `MintOrResume`. A future admin SQL insert, or a code path that `Checkouts.Add` without the lock (none today except tests’ `OpenChild` fixture), bypasses it.
2. **No worker.** Lazy TTL. Merchant list does not expire. Ops screenshot of `1 / 1` full with `paid_count = 0` is still possible for 30+ minutes of silence.
3. **Client `slot_key`.** `NormalizeSlotKey` is trim, length 8–128. Anyone with the public token can occupy seats until cap. Rate limit `PublicPayLimiter` is an in-memory `ConcurrentDictionary` per process, default 20/min per token. Two replicas = 40/min. Unlimited Test links can still be receipt-bombed at 20/min from one process. Capped links: one grief start fills until TTL. 019 suggested server-minted slot. Live did rate limit instead. Mitigation, not the suggested fix. YAML 019 `resolved` is over-claim if “resolved” means server-issued seats.
4. **GET expire does not `LockParentAsync`.** GET uses `SerializeAsync` only. Multi-process GET expire vs Start insert: Start holds parent `FOR UPDATE`; GET updates children. Intended (TTL should win). Not a double-admit by itself.
5. **Fulfillment occupancy is a different lock domain.** See §3.4. Start can be perfect and extras from 014 / fixtures still double-pay if two extra children fulfill at once.
6. **Late PSP after expire.** Seat reissued to slot B. Slot A’s CHIP page still takes money. Webhook finds checkout A `expired`, `FulfillPaidCoreAsync` returns on `status != "open"`. No receipt, no refund. This is the **largest remaining cash hole** on occupancy, and it is a missing refund feature plus a missing “expire the processor session” call. `IHostedRail` has only `CreateHostedUrlAsync`.

```5:10:apps/lazuar-pay/src/Lazuar.Pay/Rails/IHostedRail.cs
public interface IHostedRail
{
    string Provider { get; }

    Task<HostedSession> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct);
}
```

README is explicit: “New verbs (refunds, pause mail, PDF) get their own folder; they do not hang extra methods on `IHostedRail`.” The folder does not exist.

---

## 3. Fulfillment

### 3.1 Unique `charges(CheckoutId)`, unique documents

Migration `20260828001217_FulfillmentUniques`:

```13:32:apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260828001217_FulfillmentUniques.cs
            migrationBuilder.CreateIndex(
                name: "IX_documents_CheckoutId",
                schema: "public",
                table: "documents",
                column: "CheckoutId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_documents_OrgId_Number",
                schema: "public",
                table: "documents",
                columns: new[] { "OrgId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_charges_CheckoutId",
                schema: "public",
                table: "charges",
                column: "CheckoutId",
                unique: true);
```

Snapshot agrees: `charges` `HasIndex("CheckoutId").IsUnique()`; `documents` unique on `CheckoutId` and `(OrgId, Number)`. `document_sequences` PK is still `(OrgId, Series, YearMyt)` with `LastN` mutated in place. There is **no** `FOR UPDATE` on the sequence row. Concurrent fulfills of **different** checkouts in the same org can read the same `LastN`, both mint `RCPT-{year}-00006`, unique `(OrgId, Number)` trips one. The loser is the swallow in `FulfillPaidCoreAsync` (next subsection). Retry then takes `00007`. Gaps in the series are acceptable. Two receipts with the same number are not, and the unique forbids them.

`psp_webhook_events` PK is still `(OrgId, Provider, EventId)`. Same delivery is a duplicate 200. Two event ids for one checkout are two grains; unique charge is the belt.

There is **no** unique on `charges.ProviderRef`. Two checkouts can store the same CHIP purchase id if 014 double-created and both somehow fulfilled (occupancy + status guards should prevent the second). A second app cannot `GET /v1/payments?provider_ref=purch_1` because the column is write-only.

### 3.2 In-process checkout gate + swallow

```13:29:apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs
public sealed class Fulfillment(PayDbContext db) : IFulfillPaid
{
    static readonly ConcurrentDictionary<string, SemaphoreSlim> CheckoutGates = new(StringComparer.Ordinal);

    public async Task FulfillPaidAsync(string checkoutId, string provider, string? providerRef, CancellationToken ct)
    {
        var gate = CheckoutGates.GetOrAdd(checkoutId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            await FulfillPaidCoreAsync(checkoutId, provider, providerRef, ct);
        }
        finally
        {
            gate.Release();
        }
    }
```

Per-checkout, not per-org, not per-link. Two children of the same link fulfill in parallel.

Status guard is still the first money IF:

```44:47:apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs
        if (checkout.Status != "open")
        {
            return;
        }
```

Then pause throw, then occupancy re-check, then the book. SaveChanges unique failure is **swallowed**:

```164:174:apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged))
            {
                entry.State = EntityState.Detached;
            }
        }
```

Webhook:

```143:173:apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.PspWebhookEvents.Add(new PspWebhookEventRow { ... });
            await fulfillment.FulfillPaidAsync(checkout.Id, name, parsed.ProviderRef, ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            return Results.Ok(new { duplicate = true });
        }
        catch (ChargesPausedException)
        {
            await tx.RollbackAsync(ct);
            return PayErrors.Status(409, "Conflict", "Org charges are paused");
        }
        ...
        return Results.Json(new { ok = true }, OneClient.Json);
```

If fulfill swallows, webhook does **not** see `DbUpdateException`. It commits whatever is still attached (often nothing, because detach) and returns `{ ok: true }`. The second event id may not be consumed. PSP retries. On retry, checkout is `paid`, fulfill returns early, event inserts, 200. That is money-safe for **one checkout**. It is sloppy ops: first response said ok without consuming. Not a double `RCPT-`.

`ChargesPausedException` is a dedicated type that does **not** inherit `InvalidOperationException`:

```178:178:apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs
public sealed class ChargesPausedException() : Exception("Org charges are paused");
```

025 YAML said it was an `InvalidOperationException` catch-order footgun. Live closed the inheritance. Comment in the webhook catch: “Dedicated type so this cannot be shadowed by InvalidOperationException → 500.” 025 is actually closed in source, not just YAML.

### 3.3 Amount mismatch 400 consumes event? **No.**

Mismatch runs **before** the TX:

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

No `PspWebhookEvents` insert. 015’s suggested fix was **keep** 400 + no insert, and add one mismatch fixture per rail. Live kept the policy. Fixtures:

| Rail | Test | Event consumed? |
|------|------|-----------------|
| Stripe | `FillTests.Amount_mismatch_does_not_mint_receipt` | No (`PspWebhookEvents.Count() == 0`, checkout `open`) |
| Stripe | `FillTests.Currency_mismatch_does_not_mint_receipt` | No |
| CHIP | `ChipRailTests.Chip_amount_mismatch_does_not_consume_event` | No |
| Billplz | `BillplzRailTests.Billplz_amount_mismatch_does_not_consume_event` | No |
| Test | `TestRailTests.Test_webhook_wrong_amount_does_not_consume_event` | No |
| Xendit | **missing** | — |
| Razorpay | **missing** | — |

015 is **closed as a policy** and **open as Xendit/Razorpay fixtures**. Do not “fix” 015 by inserting a poison event on every 400 — 015’s own paper forbade that. The remaining work is lived JSON pins: CHIP `total: 1000` for RM10 (parser already treats sen; `ChipHosted` sends `ToMinor`), Xendit `paid_amount` major then `ToMinor`, Billplz sen, Razorpay minor, Stripe `amount_total` cents. Those comments exist in the parsers. The missing tests are Xendit/Razorpay **mismatch**, not the parse comments.

If **our** map is wrong on a lived payload, Plane B 400s forever, buyer paid, no receipt. That is still the 016 P0-D residual. Do not call units production-proven until a lived payload is checked in. Hermetic FakePsp bodies are not lived.

### 3.4 Over-capacity children

```55:69:apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs
        if (checkout.PaymentLinkId is not null)
        {
            var link = await db.PaymentLinks.FirstOrDefaultAsync(x => x.Id == checkout.PaymentLinkId, ct);
            if (link is not null)
            {
                var paid = await db.Checkouts.CountAsync(
                    x => x.PaymentLinkId == link.Id && x.Status == "paid",
                    ct);
                if (PaymentLinkOccupancy.IsFull(link.MaxPayers, paid))
                {
                    checkout.Status = "expired";
                    await db.SaveChangesAsync(ct);
                    return;
                }
            }
        }
```

Cap for fulfill is **paid** seats, not `open|paid`. That is a different grain from start. It is the right grain for “do not mint a second receipt when the product already collected N payments.” Extras become `expired`. The webhook TX still commits the event (fulfill saved expired). Buyer paid at PSP, no Official Receipt, no refund. 005’s sequential test:

`Second_fulfill_on_max_one_link_does_not_mint_a_second_receipt` — fixture two `open` children on max=1, fulfill A then B, documents=1, charges=1, extra status `expired`.

**Remaining race:** the count is not under parent `FOR UPDATE`. `CheckoutGates` is per `checkoutId`. Two extra children, two threads:

1. Both read `paid = 0`.
2. Both see not full.
3. Both insert charges (different `CheckoutId`, unique does not conflict).
4. Both mint `RCPT-`.
5. Link shows `over_capacity`.

001 is supposed to prevent extras. Belt in fulfill is sequential. `PostgresTxTests.Concurrent_fulfill_same_checkout_one_receipt` is **same** checkout, two event ids — not two children. Name the hole: **005 is closed for sequential extras and open for concurrent extras.** Production risk is low if 001 holds and 014 does not mint a second child. 014 mints a second **processor session on the same child**, which unique charge already stops. Concurrent extras need a second **row**. How would a second row exist on this SHA? Grief + race across replicas without `FOR UPDATE` (001 closed on Postgres), or test fixtures, or a future bug. Rank this below 014 and below late-webhook-no-refund.

### 3.5 `ChargesPaused`

Webhook checks pause **before** the TX and returns 409 without inserting the event. Fulfill throws `ChargesPausedException` if the flag flipped inside the TX. GET expires `open` children. Start 403 even when `PspRedirectUrl` is stored (`Start_paused_is_403_even_with_stored_url`). 033 is closed as occupancy: `Pause_expires_open_reservations` remaining 1, taken 0.

Pause still does not expire **paid**. Correct. Pause does not call any PSP to void hosted sessions. In-flight CHIP pages can still take money; webhook 409s until unpause, then pays — unless TTL expired the child in between. That is the pause × TTL cross: a paused org’s GET expires `open`; later unpause + late webhook hits `expired` and does not pay. Money at PSP, no receipt, no refund. Same family as §2.8 item 6.

### 3.6 Journal two-line, `RCPT-` series

```111:155:apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs
        var entryId = Guid.NewGuid().ToString("N");
        db.JournalEntries.Add(new JournalEntryRow { ... Currency = checkout.Currency, CreatedAt = DateTimeOffset.UtcNow });
        db.JournalLines.Add(new JournalLineRow { Account = "cash", Dc = "D", Amount = checkout.Amount });
        db.JournalLines.Add(new JournalLineRow { Account = "revenue", Dc = "C", Amount = checkout.Amount });

        var year = MalaysiaTime.Year(DateTimeOffset.UtcNow);
        var seq = await db.DocumentSequences.FindAsync([checkout.OrgId, "RCPT", year], ct);
        if (seq is null)
        {
            seq = new DocumentSequenceRow { OrgId = checkout.OrgId, Series = "RCPT", YearMyt = year, LastN = 0 };
            db.DocumentSequences.Add(seq);
        }

        seq.LastN += 1;
        var number = $"RCPT-{year}-{seq.LastN:00000}";
        db.Documents.Add(new DocumentRow
        {
            ...
            Number = number,
            Title = "Official Receipt",
            CreatedAt = DateTimeOffset.UtcNow
        });
```

Year is Malaysia (`Asia/Kuala_Lumpur` / Windows `Singapore Standard Time`). Series is **RCPT**, not INV. Title is **Official Receipt**, not tax invoice. `OrgSettingsRow.SstRegistered` exists with a comment: “Unused. Tax is out of this program. Column kept; do not read on the pay path.” IsolationTests ban `Lhdn`, `MyInvois`, `UBL`, `XAdES`, `Irbm`.

This is **not** a ledger. Two lines, cash debit / revenue credit, no accounts payable, no processor-fee line, no GST. CHIP `tax`/`fee` on Razorpay payloads are ignored. Journal is a receipt companion so merchant UI can show a number. Do not tell finance this is MYOB.

Payer rows: every fulfill with name or email inserts a **new** `PayerRow`. No unique on email. Repeat buyers duplicate. Fine for a cashier; wrong for a CRM. `MailOutbox` table exists, nothing writes it. No receipt email.

Audit: `Action = "checkout.paid"`. No `payment.completed` outbound. Plane C is out of 002 and still absent (sibling report 03).

### 3.7 Test start is fulfill

```218:229:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
            var hosted = await rail.CreateHostedUrlAsync(row, ct);
            row.Provider = name;
            row.PspRedirectUrl = hosted.RedirectUrl;
            row.ProviderSessionId = hosted.ProviderSessionId;
            if (PayProviders.IsTest(name))
            {
                db.PspWebhookEvents.Add(new PspWebhookEventRow { ... EventId = hosted.ProviderSessionId ?? "test:" + row.Id ... });
                await fulfillment.FulfillPaidAsync(row.Id, name, hosted.ProviderSessionId, ct);
            }
            else
            {
                await db.SaveChangesAsync(ct);
            }
```

Comment immediately above:

```213:214:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
            // PSP HTTP then persist. A SaveChanges failure after the processor
            // already created a session may mint a second session on retry.
```

014 is **live in the comment and the control flow**. Stripe has `IdempotencyKey = "lazuar-checkout:" + checkout.Id`. CHIP/Billplz/Xendit/Razorpay do not send an idempotency header. Stored-URL short-circuit:

```188:198:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
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
```

If persist failed, both fields are empty in the database (SaveChanges threw). Retry calls `CreateHostedUrlAsync` again. Test `Start_twice_returns_same_url_without_second_psp_http` is the **happy** replay after persist succeeded. The missing test 014 asked for (SaveChanges-fail after FakePsp success, retry send count 1) is still missing. YAML 014 `resolved` is a lie against live files.

If `ProviderSessionId` is set and `PspRedirectUrl` is empty, live 409s and does **not** recreate. That part of 014’s suggested fix is in. The empty-both retry path is not.

---

## 4. Rails: `hosted_link` only

### 4.1 The six names

```5:40:apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs
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
        env.IsDevelopment() || env.IsEnvironment("Testing");
```

`IHostedRail` is one method. Hosted session is a URL + optional processor id. No refund, no capture, no expire, no retrieve.

Stripe create is `Mode = "payment"`. Not `subscription`, not `setup` (setup/zero webhooks are ignored). Line item name is the string `"Pay"`. Amount is `MoneyMath.ToMinor`. Currency lowercased.

CHIP create now **does** send `purchase.currency` (016 P1-4 / 002-016). Price is sen. Metadata `checkout_id` / `org_id`. Join on paid webhook: metadata `checkout_id` **or** `HostedSessionId` = purchase id (`Chip_paid_without_metadata_joins_on_purchase_id`). Preauthorized ignored. `purchase.paid` is the paid type.

Billplz: sandbox vs live host from vault `environment`. Callback must be public https (`TryPublicBase`). Localhost 400 before HTTP. Form HMAC. Unpaid ignored.

Xendit: invoice `amount` is **major** (`FromMinor(ToMinor(...))`). Paid webhook `paid_amount` major then `ToMinor`. `SETTLED` / `invoice.settled` is **ignored** (`IgnoreReason = "settled"`). Paid is `PAID` / `invoice.paid`. Join: metadata `checkout_id` or `external_id`.

Razorpay: payment_links API, amount minor, notes `checkout_id`. Webhook paid type is **`payment.captured` only**. `payment_link.paid`, `payment_link.expired`, `order.paid` are ignored (the condition is a bit of a pretzel: those three **or** anything that is not `payment.captured`). Join: notes `checkout_id` or payment_link entity id as `HostedSessionId` (`Razorpay_captured_without_notes_joins_plink`). `payment.failed` ignored.

Test: `CreateHostedUrlAsync` returns success URL with `status=verifying` and `ProviderSessionId = "test:" + checkout.Id`. No keys. Start fulfills. Plane B HMAC `X-Pay-Test-Signature` over the raw body with `Pay:TestWebhookSecret`. Missing `id` / `checkout_id` / `amount_total` / `currency` → 400 `PspVerifyException`. Unsigned → 400. Replay same id → `{ duplicate: true }`.

Stripe unpaid completed ignored (`Unpaid_completed_session_is_ignored`). `async_payment_succeeded` pays after (`Async_payment_succeeded_pays_after_unpaid_completed`). 009 closed.

### 4.2 Search: Refund, dispute, cancel, expire checkout

Grep of `Refund`, `dispute`, `partial capture` in `apps/lazuar-pay/**/*.{cs,ts,tsp}` (Pay host + its tests + pay-spec): **no matches** for a refund/dispute type. Hits on “cancel” are `CancelUrl`, `CancellationToken`, `CheckoutUrls.Cancel`, Razorpay `payment_link.expired` **ignored**. Hits on “expire” are occupancy `expired` status and Razorpay ignore. Hits on “capture” are Razorpay `payment.captured` (the paid event), not a Pay capture API.

Live HTTP verbs on money:

| Method | Path | What it is |
|--------|------|------------|
| POST | `/v1/checkouts` | Mint one-off |
| GET | `/v1/checkouts/{id}` | Member read |
| GET | `/v1/orgs/{orgId}/checkouts` | One-off list |
| POST | `/v1/payment-links` | Mint link |
| GET | `/v1/orgs/{orgId}/payment-links` | Link list + occupancy |
| GET | `/v1/pay/{token}` | Public occupancy |
| POST | `/v1/pay/{token}/start` | Hosted session |
| GET | `/v1/orgs/{orgId}/payments` | All charges, no filters |
| GET | `/v1/orgs/{orgId}/receipts` | All receipts |
| GET | `/v1/orgs/{orgId}/receipts/{id}` | One receipt |
| POST | `/v1/webhooks/{provider}/{orgId}` | Plane B |
| PUT/GET | `/v1/orgs/{orgId}/gateway(s)` | BYOK |
| POST/GET | catalog products | Label + price row |

There is no `DELETE /v1/checkouts/{id}`, no `POST .../cancel`, no `POST .../refunds`, no `POST .../expire`, no `GET .../payments?provider_ref=`. `CancelUrl` is a **buyer return URL** for the processor page, not a Pay state change. Cancel bounce does not write `expired`. TTL does.

Stripe dispute events (`charge.dispute.created`, `charge.refunded`, `checkout.session.expired`) fall through Stripe parser’s type filter and return `{ ignored: "<type>" }` after consuming the event id. Consuming ignore is correct for “we do not handle this.” It is **incorrect** if a second app expected Pay to mark the charge refunded. The charge stays `status: "paid"`. Receipt stays issued. Journal stays cash/revenue. There is no reversing entry.

Partial capture: Stripe Checkout `Mode = payment` captures on success for most methods. Pay never calls PaymentIntent capture. Delayed methods wait for `async_payment_succeeded`. There is no “capture RM 5 of RM 10”.

### 4.3 What Hub museum has that this host does not

Judgment only, not a project reference: Hub Payments grew refunds, disputes, dunning workers, `FOR UPDATE SKIP LOCKED` claim jobs, subscription aggregates. IsolationTests forbid importing them. Steal the **HTTP shape** later (POST refund with amount + checkout id, idempotency key, return a refund id) without stealing MediatR. This paper refuses to copy Hub modules. Named so 00-evaluation does not rediscover “maybe we should reference lazuar-api”.

---

## 5. Subscriptions

### 5.1 The table exists

```110:118:apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs
public sealed class SubscriptionRow
{
    public required string Id { get; set; }
    public required string OrgId { get; set; }
    public required string CheckoutId { get; set; }
    public string? PayerId { get; set; }
    public required string Status { get; set; }
    public required string Interval { get; set; }
}
```

Mapped `ToTable("subscriptions")`. PK `Id` only. **No** unique on `CheckoutId`. **No** `CurrentPeriodEnd`, **no** `ProviderSubscriptionId`, **no** `Dunning`, **no** `CanceledAt`. Initial migration created the table. Nothing in 002 dropped it. Fulfillment writes a row **only** if:

```98:109:apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs
        if (checkout.Interval is "mo" or "yr")
        {
            db.Subscriptions.Add(new SubscriptionRow
            {
                Id = Guid.NewGuid().ToString("N"),
                OrgId = checkout.OrgId,
                CheckoutId = checkout.Id,
                PayerId = payerId,
                Status = "active",
                Interval = checkout.Interval
            });
        }
```

### 5.2 Interval on checkout? Dead from mint doors

Checkout mint hardcodes `Interval = "one_off"`:

```91:92:apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs
            Status = "open",
            Interval = "one_off",
```

`CreateCheckoutRequest` has **no** `Interval` property. Payment-link children hardcode `"one_off"` in `MintOrResume`. Catalog **does** store interval on `prices`:

```57:57:apps/lazuar-pay/src/Lazuar.Pay/Catalog/CatalogEndpoints.cs
            Interval = string.IsNullOrWhiteSpace(body.Interval) ? "one_off" : body.Interval.Trim()
```

It is never copied onto the checkout or the payment link. A merchant who POSTs a product `{ interval: "mo" }` and then a pay link gets a one-off child. Stripe hosted `Mode` is never `subscription`. There is no e-mandate, no Razorpay recurring, no CHIP auto-debit (CHIP copy on merchant processors.ts: “Auto-debit later, not this program”).

Grep of `dunning` in `apps/lazuar-pay`: **no matches**. Hub `DunningEngineJob` is museum.

Honest missing: **subscriptions are a leftover schema and a dead `if` in fulfill.** Not a feature. A second app that stores `interval=mo` on a checkout via raw SQL could make fulfill insert a row that nobody renews. Do not ship that. Do not expose `GET /v1/orgs/{id}/subscriptions` until there is a rail that actually re-bills. Stripe Billing / e-mandate is a later program, not a flip of this table.

pay-spec `CheckoutSession.interval` is optional. Host always sends `one_off` on mint. Spec does not describe a subscriptions resource.

---

## 6. Catalog products/prices vs mint amount honesty (002 023)

023 on `9f04ad58`: mint stored `product_id` raw, amount from body, list names without org filter, catalog MYR-only vs link any currency.

Live payment-link mint:

```88:106:apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs
        var productId = string.IsNullOrWhiteSpace(body.ProductId) ? null : body.ProductId.Trim();
        if (productId is not null)
        {
            var product = await db.Products.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == productId && p.OrgId == orgId, cancellationToken);
            if (product is null)
            {
                return PayErrors.Status(404, "Not Found", "product not found");
            }

            var price = await db.Prices.AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProductId == productId, cancellationToken);
            if (price is not null
                && (price.Amount != body.Amount.Value
                    || !string.Equals(price.Currency, currency, StringComparison.OrdinalIgnoreCase)))
            {
                return PayErrors.Status(400, "Bad Request", "amount must match the catalog price");
            }
        }
```

Test `Payment_link_amount_must_match_catalog_price` — product RM 99, link amount 10, 400 contains `"catalog"`. Other-org product id 404s (lookup is `p.OrgId == orgId`). List names on payment-links filter `p.OrgId == orgId`. Checkout list names also filter org. Receipt-by-id label lookup does **not** filter org (`Where(p => p.Id == checkout.ProductId)` only). Residual P2 tenancy on that one join; needs a guessed GUID.

**Remaining 023 on checkout mint:**

```87:92:apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs
            ProductId = string.IsNullOrWhiteSpace(body.ProductId) ? null : body.ProductId.Trim(),
            PublicToken = ...,
            Amount = body.Amount.Value,
            Currency = currency,
            Status = "open",
            Interval = "one_off",
```

No product lookup. No amount match. No 404. Interval not copied. A kernel client can POST `/v1/checkouts` with another org’s `product_id` and a different amount; list label will not leak (org filter on names) but the sidecar id is stored. Payment-link is the merchant Vite door; checkout mint is the kernel door. 023 is **closed for pay links, open for one-off checkouts.**

If `product_id` is set and the product exists but **has no price row**, payment-link mint does not 400. Amount is free. Catalog create always inserts a price, so this is an orphan/SQL case.

Currency: catalog create still rejects non-MYR (`"Bar B currency is MYR"`). Payment-link mint still uppercases whatever string (`ToUpperInvariant`) and does **not** reject USD. Child copies the link. Stripe will charge USD if the link said USD. Bar B MYR is a catalog-create check, not a money-path check. Honesty: dogfood MYR by SPA (`currency: 'MYR'` in `createProductAndLink`). API is wider.

Merchant still POSTs product then link with the **same** amount twice. Host now 400s drift. 038 orphan product on link 400 remains: Vite sets `productCreated = true` then errors “A product was created. Pay link failed”. try/finally busy is fixed (038 YAML). Orphan products still accumulate if rail 400s after product 201. Catalog is not a SKU service; it is a label factory. Prefer copy-amount-from-price and drop the second amount field later. Not this paper’s implement.

Prices table allows multiple rows per product in the schema (no unique on `ProductId`). Create inserts one. List returns all. Mint match uses `FirstOrDefault`. Two prices on one product: match is whichever EF returns first. No test. Residual honesty if someone SQL-inserts a second price.

---

## 7. Test rail production off. Staging named Testing vs Production

Live allow list:

```21:22:apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs
    public static bool AllowsTest(IHostEnvironment env) =>
        env.IsDevelopment() || env.IsEnvironment("Testing");
```

019 P0 was `!IsProduction()` — Staging unnamed-as-Production was a forge path (unsigned Test webhook, receipts that look like Stripe). 002-006 closed the allow list. Test:

```81:87:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Test/TestRailTests.cs
    public void AllowsTest_is_laptop_and_hermetic_only()
    {
        Assert.That(PayProviders.AllowsTest(new NamedEnv("Development")), Is.True);
        Assert.That(PayProviders.AllowsTest(new NamedEnv("Testing")), Is.True);
        Assert.That(PayProviders.AllowsTest(new NamedEnv("Staging")), Is.False);
        Assert.That(PayProviders.AllowsTest(new NamedEnv("Production")), Is.False);
    }
```

Mint doors 400 `"test processor is not enabled"` when not allowed (`CheckoutEndpoints`, `PaymentLinkEndpoints`). Plane B 400 `"rail not configured"` for Test when not allowed (before parse). `TestHosted.CreateHostedUrlAsync` throws `"rail not configured"` if someone reaches it.

**Staging named `Testing`:** Test is **on**. Hermetic factory `EnvironmentName = "Testing"` is why CI can mint Test without vault. If a cloud slot copies that name, Test Plane B is live. HMAC is now required (`Unsigned_test_webhook_is_400`), amount/currency/id required, so it is not 019’s unsigned forge. It is still a rail that pays without processor keys. **Do not name production-adjacent slots `Testing`.** Name them `Staging` or `Production`. Document that in deploy, not here as an implement.

**Production:** Test omitted from `GET /v1/orgs/{id}/gateways` (`PayProviders.Listed`). Merchant `readyMintRails` / `visibleRails` / `hostListsTest` trust that list. `defaultMintRail` prefers first non-test. Locks forbid `withTest` and default `'test'`. Overview “Test is always available.” is rendered **only** when `testListed` (host included it). Production overview will not show that sentence. Honest.

PUT Test is 400 (`Put_test_processor_is_400`). Test is not a vault row.

Test start=fulfill remains. Laptop dogfood. Not production money.

---

## 8. Idempotent start (`PspRedirectUrl`). Abandoned open expire

### 8.1 Start replay

Happy path: `PspRedirectUrl` set → return stored URL, no second PSP HTTP. `Start_twice_returns_same_url_without_second_psp_http` (CHIP). Same-slot link start: `Same_slot_start_twice_does_not_take_two_seats`, `Psp.SendCount = 1`.

Paid or expired start: 409 `"Checkout is not open"`.

Idempotency on **mint checkout** (not start): `Idempotency-Key` header or body, fingerprint amount+currency+provider, replay 200 same id, different body 409. `Create_idempotent_on_key`. Catch unique on `(OrgId, Key)` for races. Payment-links have **no** Idempotency-Key. Two clicks on Create pay link = two links. Merchant SPA does not retry-key that POST. Missing feature for kernel, not a cashier bug.

Stripe create idempotency key `lazuar-checkout:{id}` is processor-side. CHIP/Billplz/Xendit/Razorpay lack it. See 014.

Rate limit: `Start_rate_limit_is_429` with `StartMaxPerMinute = 2`. Production default 20. In-memory, per process, per token. Not per IP. Not a distributed limiter.

### 8.2 Abandoned open expire

`Abandoned_open_reservation_expires_and_second_slot_can_start`:

1. CHIP start slot-stale-1, 200, PSP HTTP 1.
2. Fixture `CreatedAt = UtcNow.AddMinutes(-31)`.
3. GET `?slot_key=slot-stale-1` → `status: expired`.
4. Start slot-fresh-2 → 200.
5. Direct `FulfillPaidAsync` on the expired id → `Documents.Count == 0`.

That is 003’s missing test, live. Late fulfill after reissue does not mint. The **processor** still has `purch_old`. Nobody voids it. If the abandoned buyer pays CHIP after minute 31, Plane B finds `expired` (or, if they pay **before** GET expires, the child is still `open` and **will** pay — TTL has not run). Order:

- Pay CHIP at minute 29, webhook at 29: child `open`, fulfills, seat paid. Correct.
- Walk away, GET at minute 31: expired, seat free, B starts. A then pays CHIP: webhook on expired, no receipt. A’s money at CHIP. **Hole.**
- Walk away, no GET, A pays at minute 40: child still `open` (lazy TTL), fulfills, B cannot start. Merchant thought it was abandoned. **Stale full.** Both are consequences of lazy TTL + no PSP expire + no refund.

Do not invent expiry in the SPA. Checkout `App.tsx` has an expired Card that renders when host says `expired`. Locks grep `Link expired`.

---

## 9. What a second app needs beyond hosted cashier

This host is a cashier for `:5178` / `:5179`. A second app (Hub-shaped SaaS, invoicing, another storefront) typically needs:

| Need | Live on `6d730d15` | Bug or missing feat |
|------|--------------------|---------------------|
| Mint a hosted checkout with amount + rail | `POST /v1/checkouts` writer Bearer | Feat exists |
| Mint a capped pay link | `POST /v1/payment-links` | Feat exists |
| Idempotent mint | Header on checkouts only; not on links | Missing feat on links |
| Start / hosted URL | `POST /v1/pay/{token}/start` public | Feat exists; 014 bug on persist-after-HTTP |
| Poll paid | `GET /v1/pay/{token}` or `GET /v1/checkouts/{id}` | Feat exists; GET by id is member Bearer, 401 before 404 |
| Official Receipt number | `GET /v1/orgs/{id}/receipts` | Feat exists |
| **Refund API** | None | **Missing feat** |
| **Cancel unpaid** | None (TTL / pause expire only) | **Missing feat** |
| **Merchant expire / release seat** | None | **Missing feat** |
| **Get payment by `provider_ref`** | Column written, never queried | **Missing feat** |
| **List with filters** (`status`, `provider`, `from`, `to`, `checkout_id`) | `GET /payments` returns every charge | **Missing feat** |
| Outbound `payment.completed` | None (sibling 03) | Missing feat / kernel door |
| Machine key `lzr_sk_` | None (sibling 02) | Missing feat / kernel door |
| Subscriptions / dunning | Table leftover | Missing feat; refuse to pretend |
| Disputes | Ignored Stripe types | Missing feat |
| Partial capture | No | Missing feat; refuse for hosted_link MVP |
| PDF receipt / email | `MailOutbox` empty | Missing feat |
| Multi-currency production | Catalog MYR; links any ISO-ish 3-letter | Honesty leftover |

`GET /v1/orgs/{orgId}/payments` shape:

```43:61:apps/lazuar-pay/src/Lazuar.Pay/Money/Queries/PaymentQueryEndpoints.cs
        return Results.Json(rows
            .OrderByDescending(...)
            .Select(c => new
            {
                id = c.Id,
                org_id = c.OrgId,
                checkout_id = c.CheckoutId,
                amount = c.Amount,
                currency = c.Currency,
                status = c.Status,
                provider = c.Provider,
                payer_name = ch?.PayerName,
                created_at = ch?.CreatedAt,
                label = ...
            }), OneClient.Json);
```

No `provider_ref` on the wire. No query string. Loads **all** charges for the org into memory. Fine for dogfood tens of rows. Wrong for a second app with thousands. Missing feat: pagination + filters. Not a bug until someone lists 100k.

`GET /v1/checkouts/{id}` is by Pay id, not by processor id. A CHIP dashboard ticket with `purch_…` cannot be resolved from Pay HTTP. Staff would SQL `charges."ProviderRef"`. That is not a door.

Cancel unpaid: a second app that minted a checkout and then abandoned it has no DELETE. The row stays `open` forever (standalone checkouts are **not** on the payment-link TTL path — `ExpireStaleAsync` filters `PaymentLinkId == linkId`). **Standalone open checkouts never expire.** Occupancy TTL is a **link** feature. One-off mint + start + walk away = a live CHIP purchase and an `open` checkout until Plane B or forever. Named: **TTL does not apply to `PaymentLinkId is null`.** Second apps that use `/v1/checkouts` instead of payment-links inherit no occupancy and no expire. They must be told. Missing feat: expire/cancel on one-off.

Refund: without it, 005 extras, 014 doubles, late-TTL pays, Stripe `charge.refunded` ignored — finance cannot unwind Official Receipts. Journal has no reverse. `documents` unique on checkout forbids a second “refund receipt” on the same checkout id unless the model grows a document type. Do not hang refund on `IHostedRail`. New folder `Money/Refunds/` when the program exists. IsolationTests will stay red on Hub `Modules.Payments`.

---

## 10. 019 occupancy P0 vs this SHA — closed or not, with tests named

019 parent §5 P0 money list (occupancy-related) mapped onto live files:

### P0-1 Count-then-insert overfills capped pay links (issue 001)

**Closed on this SHA** for Start, with tests:

- `PaymentLinkTests.Concurrent_start_on_one_person_link_admits_one_psp` — two slots, CHIP FakePsp, one 200 one 409, `Psp.SendCount == 1`, `Documents == 0` (CHIP not auto-fulfill), one `open`.
- `PaymentLinkTests.Concurrent_test_start_on_one_person_link_mints_one_receipt` — Test rail, one Official Receipt, one `paid`.
- `PostgresTxTests.Concurrent_starts_on_one_seat_leave_one_open` — Testcontainers Postgres 16, `taken_count == 1`, status `full`. Skips if Docker unavailable (`PayPostgres.FactoryAsync` `Assert.Ignore`).
- Sequential belts still present: `Two_people_can_pay_a_link_of_two`, `Two_chip_starts_hold_open_seats_on_a_link_of_two`.

Mechanism: `SerializeAsync` + relational TX + `LockParentAsync` `FOR UPDATE` + count `open|paid` + insert + `DbUpdateException` resume.

**Not closed:** InMemory is still not a transaction proof (077). Factory still ignores transactions. Postgres test is the proof; CI without Docker skips it. Call occupancy production-proven **only** where Testcontainers ran.

### P0-2 Seat reserved before start can succeed (issue 002)

**Closed** on the 400 paths 019 named:

- `Chip_start_without_email_does_not_occupy_the_only_seat`
- `Billplz_localhost_callback_400_frees_the_seat`

Email / callback base / `CheckoutUrls.Base` validate before insert. PSP/Stripe throw after insert → `ExpireFailedReservation` if URL not stored.

**Not closed:** persist-after-HTTP (014) can leave a seat `open` with a processor session.

### P0-3 Abandoned `open` children never expire (issue 003)

**Closed as lazy TTL**, test `Abandoned_open_reservation_expires_and_second_slot_can_start` (CHIP, −31 minutes, second slot 200, late fulfill on expired mints 0 receipts).

**Not closed as a worker.** No `IHostedService`. Merchant list does not expire. Standalone checkouts never expire.

### P0-4 Occupancy copy lies successful payment vs start (issue 004)

**Closed.** Dialog: “The link closes after one person starts Pay. Unpaid starts free after 30 minutes.” Unlimited: `started · unlimited`. Buyer: “no remaining seats.” Locks grep the new sentence and ban the old. Host grain is still `open|paid`, which now matches copy.

### P0-5 Fulfillment pays over-capacity children (issue 005)

**Closed sequentially:** `Second_fulfill_on_max_one_link_does_not_mint_a_second_receipt`. Extra child `expired`.

**Not closed concurrently** across two extra children (no parent lock in fulfill). Low risk if 001 holds.

### 019 parent P0 Test unsigned / amount optional (issues 006, 007, 008)

**Closed.** HMAC required, amount/currency/id required, Staging/Production `AllowsTest` false. Tests: `Unsigned_test_webhook_is_400`, `Test_webhook_without_amount_is_400`, `Test_webhook_without_id_is_400`, `Test_webhook_wrong_amount_does_not_consume_event`, `Test_webhook_replay_same_id_is_duplicate`, `AllowsTest_is_laptop_and_hermetic_only`.

### 019 parent P0 Stripe `payment_status` / unique charge (issues 009, 010)

**Closed.** Unpaid completed ignored; async succeeded pays. Unique indexes + `SemaphoreSlim` + `PostgresTxTests.Concurrent_fulfill_same_checkout_one_receipt` + `FillTests.Concurrent_fulfill_of_one_checkout_mints_one_receipt`. Swallow unique is sloppy but not a double book.

### 019 P0 product One HMAC (issue 011) — **out of this money slice’s proof**

Sibling 04/05 own Plane A. Fulfillment **does** honor `ChargesPaused` (409, no receipt, event not consumed). Whether product One actually sets the flag is not this file. Occupancy pause expire is live **if** the flag is set (`Pause_expires_open_reservations`).

### 019 occupancy P1/P2 that 002 claimed

| Issue | 019 | Live `6d730d15` | Tests |
|-------|-----|-----------------|-------|
| 012 CHIP join metadata-only | P0 | Closed: purchase id join | `Chip_paid_without_metadata_joins_on_purchase_id` |
| 013 same-slot 500 | P1 | Closed: catch unique resume | sequential `Same_slot_start_twice`; no Postgres same-slot race |
| 014 PSP HTTP then persist | P1 | **OPEN in source** (comment + else SaveChanges) | happy replay only |
| 015 mismatch 400 no consume | P1 | Policy kept; fixtures 4/6 rails | Stripe/CHIP/Billplz/Test yes; Xendit/Razorpay no |
| 019 client slot_key | P1 | Mitigated (rate limit 20/min), not server-minted | `Start_rate_limit_is_429`; grief still 400s a 1-seat link |
| 023 catalog not money | P1 | Closed on **links**; open on **checkouts** | `Payment_link_amount_must_match_catalog_price` |
| 025 pause catch order | P2 | Closed: dedicated `Exception` subclass | `Paused_org_does_not_mint_receipt` |
| 031 list mixes children | P2 | Closed: `PaymentLinkId == null` | `List_omits_payment_link_children` |
| 032 child token second URL | P2 | GET aliases parent; POST start still standalone | `Child_public_token_loads_parent_occupancy` |
| 033 pause stuck occupy | P2 | Closed: `ExpireOpenAsync` on GET | `Pause_expires_open_reservations` |
| 034 Test occupancy hides reservation | P1 | Closed: CHIP `open` tests exist | `Two_chip_starts_hold_open_seats_on_a_link_of_two`, `Abandoned_…` |
| 042 Test always offered | P1 | Closed: trust host list | merchant locks `readyMintRails`, not `withTest` |
| 079 remaining clamp | P1 | Closed: `over_capacity`, unclamped remaining | `List_over_admit_is_over_capacity_not_silent_full` |
| 077 InMemory not TX | P1 | Still true; PostgresTxTests added | `Fulfill_save_then_throw_rolls_back_event` |

**Headline:** 019 occupancy P0 is closed on this SHA with named tests, except the **late-PSP-after-TTL** cash hole (a missing refund/expire-at-processor, not the original count-then-insert) and the **014 persist-after-HTTP** hole YAML marked resolved. Do not trust the YAML.

---

## 11. How to solve remaining money holes (ranked)

Do not staff SST, LHDN, escrow, or MediatR in the same slice. IsolationTests already ban the last two and the LHDN tokens. `SstRegistered` column stays unread.

### Rank 1 — Refund + expire-at-processor (missing feat, unblocks TTL)

The cashier now expires seats. The processor does not. Every TTL recovery, every pause expire, every 005 extra child that already paid CHIP is a **stranded processor capture** with no Official Receipt or a receipt you cannot reverse.

Solve without a cathedral:

1. New folder `Money/Refunds/` (README already reserved the name). Not a method on `IHostedRail`.
2. Per-rail small types: Stripe refund on PaymentIntent/session; CHIP refund purchase; Billplz delete/refund bill if their API allows; Xendit invoice expiry; Razorpay payment refund. HTTP judgment from Hub, code from lived docs, **no** `Modules.Payments`.
3. Door: `POST /v1/orgs/{orgId}/refunds` writer, body `{ checkout_id, amount? }`, Idempotency-Key, 201 refund row, reverse journal (cash credit / revenue debit) or a second document type `RCPT` credit. Unique `(CheckoutId)` on documents currently forbids a second doc — **schema change** or refunds live only on `charges.status`.
4. On `ExpireStaleAsync` / `ExpireOpenAsync` / over-capacity expire: enqueue “void hosted session if not captured else refund” **or** document the operator runbook (“money at PSP, no receipt, refund in dashboard”). A runbook is honest; silent drop is not.
5. Plane B `charge.refunded` / CHIP refund webhook → mark charge `refunded`, do not mint a second paid receipt.

Until Rank 1 exists, **do not sell TTL as cash-safe**. Sell it as occupancy-safe. Those are different.

Refuse in Rank 1: partial refunds of 3/10 line items, marketplace split, escrow hold. Full-amount refund of a one-off hosted payment is enough.

### Rank 2 — Close 014 persist-after-HTTP (bug)

Live comment admits it. YAML resolved is wrong.

Solve:

- Persist `ProviderSessionId` / `PspRedirectUrl` **intent** before HTTP, or
- Send each rail’s idempotency header (Stripe already; Xendit `Idempotency-key`; CHIP if any; Razorpay none → persist first), or
- If `CreateHostedUrlAsync` succeeded and `SaveChanges` failed, do not 200 with the URL that is not stored; 503 so the client retries after a human, **and** still persist on retry using Stripe-style key.

Test that 014 asked for: CHIP FakePsp success, injected SaveChanges fail, retry `Psp.SendCount == 1`. Postgres not required if the probe is in `PublicPayEndpoints`. Do not add a factory.

### Rank 3 — Fulfill occupancy under the same parent lock (bug belt)

`FulfillPaidCoreAsync` should `LockParentAsync` when `PaymentLinkId` is set, inside the webhook TX, then count paid, then insert. Concurrent extras then serialize. Test: two threads, two fixture `open` children, max=1, documents=1. Postgres. Sequential test already exists; this is T0 for 005 concurrent.

Do not put occupancy inside every rail parser.

### Rank 4 — Expire worker for silent links + one-off cancel door (missing feat)

Lazy TTL is occupancy-correct only when GET happens. A worker every minute: `ExpireStaleAsync` for links with `open` children older than TTL. Simple hosted service in `Hosting/` or `PaymentLinks/`. IsolationTests do not ban `IHostedService`; they ban `IEnumerable<IHostedRail>`. Keep it stupid: one SQL update, no outbox.

One-off checkouts: `POST /v1/checkouts/{id}/cancel` writer, only if `open`, sets `expired`. Second apps need this more than pay-links (pay-links have TTL). Do not call it refund.

### Rank 5 — Catalog money on **checkout** mint (023 leftover)

Same lookup as payment-links: org-scoped product, amount/currency must match price, copy interval or 400 unknown interval. Or drop `product_id` from `CreateCheckoutRequest` and keep catalog as pay-link labels. Either way, one money field. Test: checkout amount ≠ price → 400. Other-org product → 404. Do not leak names (already filtered on list).

Reject non-MYR on payment-link mint if Bar B is still the product. Today only catalog create rejects. Honesty.

### Rank 6 — List/get money for a second app (missing feat)

- `GET /v1/orgs/{orgId}/payments?status=&provider=&from=&to=&checkout_id=`
- `GET /v1/orgs/{orgId}/payments/by-ref/{provider}/{providerRef}` or query `provider_ref`
- Put `provider_ref` on the list JSON
- Pagination (`limit`/`cursor`) before anyone dogfoods volume
- Unique index on `(OrgId, Provider, ProviderRef)` **if** refs are unique per rail (Stripe session ids yes; confirm CHIP)

These are kernel doors. They do not take money. They unblock support tickets. Rank below cash holes.

### Rank 7 — Mismatch fixtures for Xendit and Razorpay (015 leftover)

One method each, event row absent, checkout `open`. Pin lived units in comments (already in parsers). Do not consume on 400.

### Rank 8 — Server-minted `slot_key` (019 leftover)

Rate limit is a grief brake. Server GET that returns a reservation + slot under the same `FOR UPDATE` as insert would make the public token insufficient to occupy. Buyers remain without One accounts (cookie or signed slot). Do not require login on `:5179`. Unlimited links still need the rate limit.

### Rank 9 — Subscriptions: **do not**

Do not expose the table. Do not copy Hub dunning. Do not set Stripe `Mode = subscription`. If a program wants recurring, it is a new rail capability string, not `hosted_link`, and a new folder. Leave `SubscriptionRow` as schema leftover or drop it in a migration when someone cares. Dead `if (checkout.Interval is "mo" or "yr")` can stay; mint never sets those.

### Rank 10 — Spec honesty for money doors

pay-spec still says checkout list mixes children. It has no refunds (honest — they do not exist). Fix the list comment. Do not add refund operations to tsp until Rank 1 exists. Dist OpenAPI is sibling 09.

---

## 12. Refuse

**SST.** `SstRegistered` is unused. Receipt is Official Receipt `RCPT-`. Catalog copy “Bar B currency is MYR” is a create check, not a tax engine. Do not compute 6% on the pay path. Do not print “tax invoice”.

**LHDN.** IsolationTests fail on `Lhdn`, `MyInvois`, `UBL`, `XAdES`, `Irbm`. e-Invoice is a different product. Do not submit `RCPT-` to MyInvois. Do not add `packages/lhdn-sdk-dotnet` to `Lazuar.Pay.csproj`.

**Escrow.** Capability is `hosted_link`. Money lands on the merchant’s PSP account. Pay does not hold funds. Processor page is the processor’s. Do not invent a Pay wallet.

**MediatR.** IsolationTests fail on `MediatR`, `Modules.One`, `BuildingBlocks`, `IPaymentGatewayAdapter`, `PaymentGatewayFactory`, `IEnumerable<IHostedRail>`, `namespace Lazuar.Pay.Gateways`. Refunds get a folder and a method. They do not get a notification pipeline. Occupancy stays a static class + SQL. Fulfillment stays one scoped service.

Also refuse in this money leftover program (so 00-evaluation does not grow them):

- Marketplace `application_fee` / `transfer_data` (IsolationTests already ban the strings).
- Hub cutover of ops :3003 / portal :3004 onto 8081.
- Treating Test as a production rail if someone names the slot `Testing`.
- Pretending `subscriptions` is dunning.
- Calling two journal lines a ledger.
- Adding `IEnumerable<IHostedRail>` to “make refunds pluggable”.

---

## 13. YAML vs live (do not trust issues/002)

`issues/002/README.md` line 6: “Status: 001–080 resolved on `fix/002-pay-host-bugs`.” Individual issue files still say `status: open` in the body while YAML frontmatter says `status: resolved` (001, 005, 010, 014, 015, 023, 019, 033, 032, 034). That is tracker rot. Live files win.

Over-claims:

- **014 resolved** — live comment and `else await db.SaveChangesAsync` after PSP HTTP. Not resolved.
- **019 resolved** — slot is still client-supplied; rate limit is a different fix than the one the issue asked for.
- **023 resolved** — payment-links yes, checkouts no.
- **015 resolved** — policy was to keep 400; Xendit/Razorpay fixtures still missing. Call it “policy closed, fixtures incomplete”.
- **032 resolved** — GET aliases; POST start on child token does not re-enter occupancy.
- **003 resolved** — lazy TTL, no worker, one-off checkouts never expire. Occupancy P0 for **links** is closed; the word “never expire” is false for links-on-GET and still true for standalone.

Under-claims (YAML resolved **and** live actually closed, which this paper confirms): 001, 002 (400 paths), 004, 005 sequential, 006–010, 012, 013 sequential, 025, 031, 033, 034, 042, 079.

---

## 14. Production money bar (this slice only)

First-party dogfood (One + Pay merchant + Pay checkout) **may** take real Stripe/CHIP/Billplz one-off MYR on a capped or unlimited pay link if:

- Environment is `Production` (Test off) or `Development` (Test on, laptop).
- `Pay:ReservationTtlMinutes` understood by staff (dialog hardcodes “30 minutes”; config can differ — **honesty leftover**: dialog is not bound to config).
- Staff accept that an abandoned CHIP tab can still charge the card after TTL if the buyer completes it, and Pay will not refund.
- Staff accept 014: a DB blip after CHIP HTTP can create a second purchase on retry.
- WrapKey / CORS / public callback base are sibling 06 (host production). This paper does not re-litigate them except Billplz localhost 400, which is money-adjacent and tested.

First-party dogfood **must not** claim: subscriptions, refunds, disputes, e-invoice, occupancy-safe under replica-without-Postgres-tests, catalog-priced one-off checkouts, Test in Production.

Second-app integrate **must not** start until Rank 1–2 and Rank 6 exist, **or** the app is a clone of this merchant SPA (hosted cashier only) and refunds happen in the PSP dashboard. Kernel doors (M2M, outbound webhook) are siblings 02/03; even with those, money verbs above are still missing.

---

## 15. Evidence index (files opened)

Host occupancy / mint / start:

- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkOccupancy.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/CreatePaymentLinkRequest.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayLimiter.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/CheckoutUrls.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutStore.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CreateCheckoutRequest.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutSession.cs`

Fulfill / Plane B / rails:

- `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Money/MoneyMath.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Money/Queries/PaymentQueryEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Webhooks/PspParseResult.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/IHostedRail.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/HostedSession.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Stripe/StripeHosted.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Stripe/StripeWebhook.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Chip/ChipHosted.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Chip/ChipWebhook.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Billplz/BillplzHosted.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Billplz/BillplzWebhook.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Xendit/XenditHosted.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Xendit/XenditWebhook.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Razorpay/RazorpayHosted.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Razorpay/RazorpayWebhook.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestHosted.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestWebhook.cs`

Schema / catalog / program:

- `apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260828001217_FulfillmentUniques.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/PayDbContextModelSnapshot.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Catalog/CatalogEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/appsettings.json`
- `apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs`
- `apps/lazuar-pay/README.md`

Tests:

- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Webhooks/FillTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Webhooks/PostgresTxTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Webhooks/WebhookTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPay/PublicPayTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Test/TestRailTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Chip/ChipRailTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Billplz/BillplzRailTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Stripe/StripeRailTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Xendit/XenditRailTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Razorpay/RazorpayRailTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Catalog/CatalogTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Checkouts/CheckoutTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Money/PaymentQueryTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayPostgres.cs`

SPAs:

- `apps/lazuar-pay-merchant/src/lib/occupancyDisplay.ts`
- `apps/lazuar-pay-merchant/src/lib/occupancyDisplay.test.ts`
- `apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx`
- `apps/lazuar-pay-merchant/src/pages/org/OverviewPage.tsx`
- `apps/lazuar-pay-merchant/src/lib/processors.ts`
- `apps/lazuar-pay-merchant/src/locks.test.ts`
- `apps/lazuar-pay-checkout/src/App.tsx`
- `apps/lazuar-pay-checkout/src/locks.test.ts`

Spec / tracker / 019:

- `packages/pay-spec/main.tsp`
- `issues/002/README.md` and issue files 001–005, 010, 013–015, 019, 023, 025, 031–034, 042, 079
- `plans/019-evals/00-evaluation.md`
- `plans/019-evals/05-payment-links-occupancy.md`
- `plans/020-evals/README.md`

---

## 16. Named tests (occupancy + fulfill money)

PaymentLinkTests:

- `Create_defaults_to_one_payer`
- `Create_unlimited_has_null_max`
- `Create_max_zero_is_400`
- `Two_people_can_pay_a_link_of_two`
- `Same_slot_start_twice_does_not_take_two_seats`
- `Unlimited_accepts_three_payers`
- `One_person_link_shows_already_paid_without_slot_after_pay`
- `One_person_link_shows_paid_with_payer_slot_after_pay`
- `Start_link_without_slot_key_is_400`
- `Child_public_token_loads_parent_occupancy`
- `Pause_expires_open_reservations`
- `Two_chip_starts_hold_open_seats_on_a_link_of_two`
- `Start_rate_limit_is_429`
- `Concurrent_start_on_one_person_link_admits_one_psp`
- `Concurrent_test_start_on_one_person_link_mints_one_receipt`
- `Chip_start_without_email_does_not_occupy_the_only_seat`
- `Billplz_localhost_callback_400_frees_the_seat`
- `Abandoned_open_reservation_expires_and_second_slot_can_start`
- `Second_fulfill_on_max_one_link_does_not_mint_a_second_receipt`
- `List_over_admit_is_over_capacity_not_silent_full`

PostgresTxTests:

- `Fulfill_save_then_throw_rolls_back_event`
- `Concurrent_starts_on_one_seat_leave_one_open`
- `Concurrent_fulfill_same_checkout_one_receipt`

FillTests:

- `Fulfill_throw_returns_5xx_event_not_committed_retry_pays`
- `Amount_mismatch_does_not_mint_receipt`
- `Currency_mismatch_does_not_mint_receipt`
- `Concurrent_fulfill_of_one_checkout_mints_one_receipt`

WebhookTests (money-adjacent): `Completed_session_writes_receipt_and_replay_is_noop`, `Paused_org_does_not_mint_receipt`, `Unpaid_completed_session_is_ignored`, `Async_payment_succeeded_pays_after_unpaid_completed`.

PublicPayTests: `Start_twice_returns_same_url_without_second_psp_http`, `Start_paid_is_409`, `Start_paused_is_403_even_with_stored_url`.

TestRailTests: `Mint_and_start_pays_without_keys`, `Unsigned_test_webhook_is_400`, `AllowsTest_is_laptop_and_hermetic_only`, amount/id/replay tests.

CatalogTests: `Payment_link_amount_must_match_catalog_price`.

CheckoutTests: `Create_idempotent_on_key`, `List_omits_payment_link_children`.

PaymentQueryTests: list payments/receipts, get receipt by id.

IsolationTests: `Source_does_not_use_mediatr_or_hub_modules`.

Missing tests this paper still wants (not implemented here):

- CHIP SaveChanges-fail after PSP HTTP, retry send count 1 (014).
- Two concurrent extra-child fulfills on max=1 → documents 1 (005 concurrent).
- Xendit amount mismatch does not consume event.
- Razorpay amount mismatch does not consume event.
- Postgres same-slot concurrent start resumes, never 500.
- Checkout mint amount ≠ catalog price 400 (023 leftover).
- Standalone checkout still `open` after 31 minutes (document that TTL is link-only).
- GET payment by `provider_ref` — cannot exist until the door exists.

---

## 17. Closing

002 turned occupancy from a sequential green lie into a locked cashier with a written product rule, a 30-minute lazy TTL, `over_capacity` display, unique charges, unique receipts, Test HMAC, Test off outside Development/Testing, Stripe `payment_status`, CHIP join-on-purchase-id, pause-expires-open, child GET alias, one-off list filter, and merchant copy that finally says start not “successful payment”.

What still blocks **production money** is not “two browsers take the last seat.” That P0 is closed, with Postgres Testcontainers when Docker runs. What blocks it is **the other side of TTL and of 014**: processor sessions Pay no longer wants, cannot void, and cannot refund; plus a persist-after-HTTP retry that can mint a second CHIP purchase on the same child (unique charge stops the second receipt, not the second capture).

What a **second app** needs beyond this cashier is a refund door, a cancel/expire door on unpaid one-offs, a lookup by `provider_ref`, filtered payment lists, and (if they believe the leftover table) subscriptions that this host will not run. Those are missing features. Rank 1–2 before kernel polish. Refuse SST, LHDN, escrow, MediatR.

Live files on `6d730d15` are authority. issues/002 YAML is not.
