# 03 — Plane C: Pay POSTs signed events to a stranger app

**Program:** 020-evals  
**Slice:** Plane C — Pay → merchant / second app (`payment.completed` and friends)  
**Date:** 28 August 2026  
**Type:** Uncondensed evaluation. **Not** an implementation. **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md) Status cells. **Not** a project reference into `apps/lazuar-api`. **Not** a copy of sibling `Modules/One`.  
**This file is the deliverable.**

| Tree | Path | HEAD | Tip |
|------|------|------|-----|
| Pay (this tree) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` | `6d730d15` (`6d730d155c871465c35c192cf7730bfd270b47fa`) | `fix(pay): store per-org One webhook secrets` (2026-08-28 09:29:02 +0800) |
| One (sibling, pattern only) | `/Users/akmalfirdaus/Code/lazuar/lazuar-one` | `6b78e9d4` (`6b78e9d455618f2a0cfadf46089d5f993f081983`) | `fix(api): allow Pay merchant :5178 as a CORS origin` (2026-08-25 03:12:50 +0800) |
| Branch | `fix/002-pay-host-bugs` | | |

Index: [README.md](./README.md). Parent judgment is `00-evaluation.md` after `01`–`10`. This paper is Plane C only.

---

## 0. How to read this paper

Three webhook planes already live in this family. Mixing them is how Hub ended up with an in-process catalog talking to itself **and** a public HMAC door **and** Stripe/CHIP/Billplz callbacks, all called “webhooks.” This paper does not mix tables, secrets, or routes.

| Plane | Direction | Job | Owner in 020 |
|-------|-----------|-----|--------------|
| **A. One → Pay** | One POSTs signed JSON to Pay | `tenant.suspended` / `tenant.reactivated` pause | **04** (inbound). Live on this SHA: `POST /v1/one/webhooks` + per-org `PUT /v1/orgs/{orgId}/one-webhook`. |
| **B. PSP → Pay** | Stripe / CHIP / Billplz / Xendit / Razorpay / Test POST to Pay | Money. Verify, unique `(org, provider, event_id)`, fulfill in the same TX | **04** (inbound). Live: `POST /v1/webhooks/{provider}/{orgId}`. |
| **C. Pay → merchant / second app** | Pay POSTs to a stranger | `payment.completed` and friends so the other app does not poll Pay forever | **This paper.** |

Plane A is One’s product consumed by Pay. Plane B is Pay’s product consumed by Pay. Plane C is the Bezos door a second app needs. Stealing **judgment** from Hub museum and from sibling One is allowed. Copying Hub types (`OutboundWebhookDispatcherJob`, `GatewayPaymentCompletedIntegrationEvent`, `WebhookDeliveryOutbox`) into `apps/lazuar-pay` is refused. Copying One’s AES wrapping key (`Webhooks:SigningSecretEncryptionKey`) into Pay is refused. Pay already has `Pay:WrapKey` / `SecretBox`.

**What “live evidence” means here.** Greps and file reads on `6d730d15` under `apps/lazuar-pay`, `packages/pay-spec`, `apps/lazuar-pay-merchant`, `apps/lazuar-pay-checkout`. Hub `apps/lazuar-api` is museum — quoted only for judgment. Sibling One is quoted with line numbers as a pattern Pay could **mirror without copying code**. Historical 011 / 012 / 013 / 018 / 019 papers are quoted to answer “still true on this SHA?” Live files win if they disagree.

**What this paper will not do.** It will not design a cathedral dispatcher with seventeen event types, CloudEvents, Standard Webhooks *libraries*, dual-write into Hub’s `one.WebhookDeliveryOutboxes`, or buyer entitlement in One. The hatch is one signed POST after fulfill.

---

## 1. Search: outbound dispatch in focused Pay — honest empty set

### 1.1 What was searched

Focused Pay, this SHA:

| Path | Role |
|------|------|
| `apps/lazuar-pay/src` | Host |
| `apps/lazuar-pay/tests` | Tests |
| `packages/pay-spec/main.tsp` | Contract |
| `apps/lazuar-pay-merchant` | Staff SPA `:5178` |
| `apps/lazuar-pay-checkout` | Buyer SPA `:5179` |

Needles: `webhook_endpoints` table, `WebhookDispatcher`, `OutboundWebhook`, `payment.completed`, `X-Lazuar-Signature` as an **egress** header, `whsec_` as a **merchant signing** secret (not PSP / not One inbound), HttpClient POST to a merchant URL, event outbox that is not `mail_outbox`.

### 1.2 `payment.completed` under the new host and pay-spec

```
rg 'payment\.completed' apps/lazuar-pay/src
→ no matches

rg 'payment\.completed' packages/pay-spec
→ no matches
```

The string does not exist in the focused host source or its TypeSpec. It exists in museum (`apps/lazuar-api/Modules/Payments/.../IntegrationCheckoutGatewayEventsHandler.cs`), in Hub sample (`examples/hub-cashier-next`), in `packages/api-spec` (Hub TypeSpec), and in historical plans. Those are not Plane C on 8081.

### 1.3 `webhook_endpoints` table / `/v1/orgs/{orgId}/webhooks`

```
rg 'webhook_endpoints|/v1/orgs/.*/webhooks|orgs/\{orgId\}/webhooks' --glob '*.{cs,tsp,ts,tsx}'
→ no matches under Pay host, pay-spec, merchant, checkout
```

`PayDbContext` tables on this SHA, quoted live:

```8:25:apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs
    public DbSet<OrgSettingsRow> OrgSettings => Set<OrgSettingsRow>();
    public DbSet<CheckoutRow> Checkouts => Set<CheckoutRow>();
    public DbSet<PaymentLinkRow> PaymentLinks => Set<PaymentLinkRow>();
    public DbSet<IdempotencyKeyRow> IdempotencyKeys => Set<IdempotencyKeyRow>();
    public DbSet<ProductRow> Products => Set<ProductRow>();
    public DbSet<PriceRow> Prices => Set<PriceRow>();
    public DbSet<GatewayCredentialRow> GatewayCredentials => Set<GatewayCredentialRow>();
    public DbSet<PspWebhookEventRow> PspWebhookEvents => Set<PspWebhookEventRow>();
    public DbSet<ChargeRow> Charges => Set<ChargeRow>();
    public DbSet<SubscriptionRow> Subscriptions => Set<SubscriptionRow>();
    public DbSet<JournalEntryRow> JournalEntries => Set<JournalEntryRow>();
    public DbSet<JournalLineRow> JournalLines => Set<JournalLineRow>();
    public DbSet<DocumentRow> Documents => Set<DocumentRow>();
    public DbSet<DocumentSequenceRow> DocumentSequences => Set<DocumentSequenceRow>();
    public DbSet<PayerRow> Payers => Set<PayerRow>();
    public DbSet<AuditEventRow> AuditEvents => Set<AuditEventRow>();
    public DbSet<MailOutboxRow> MailOutbox => Set<MailOutboxRow>();
    public DbSet<OneWebhookEventRow> OneWebhookEvents => Set<OneWebhookEventRow>();
```

Tables implied by `ToTable(...)` in the same file:

- `org_settings`
- `checkouts`
- `payment_links`
- `idempotency_keys`
- `products`
- `prices`
- `gateway_credentials`
- `psp_webhook_events`
- `charges`
- `subscriptions`
- `journal_entries`
- `journal_lines`
- `documents`
- `document_sequences`
- `payers`
- `audit_events`
- `mail_outbox`
- `one_webhook_events`

There is no `webhook_endpoints`. There is no `webhook_deliveries`. There is no outbound event outbox.

`mail_outbox` is **not** that table. It is a receipt-mail hatch that is unused (see §2.3). Mixing `kind=receipt` with HMAC delivery would be the cathedral in one column.

### 1.4 HttpClient POST targets in the host

`Program.cs` registers HTTP clients for **One** (Plane A client) and **PSP hosted-session create** (Plane B egress to Stripe/CHIP/Billplz/Xendit/Razorpay). There is no `"webhooks"` / `"DeveloperWebhooks"` / `"merchant-webhooks"` named client.

```31:46:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
builder.Services.AddHttpClient<OneClient>();
builder.Services.AddHttpClient("chip");
builder.Services.AddHttpClient("billplz");
builder.Services.AddHttpClient("xendit");
builder.Services.AddHttpClient("razorpay");
builder.Services.AddDataProtection();
builder.Services.AddSingleton<SecretBox>();
builder.Services.AddScoped<CheckoutStore>();
builder.Services.AddScoped<StripeHosted>();
builder.Services.AddScoped<ChipHosted>();
builder.Services.AddScoped<BillplzHosted>();
builder.Services.AddScoped<XenditHosted>();
builder.Services.AddScoped<RazorpayHosted>();
builder.Services.AddScoped<TestHosted>();
builder.Services.AddScoped<Fulfillment>();
builder.Services.AddScoped<IFulfillPaid>(sp => sp.GetRequiredService<Fulfillment>());
```

`rg BackgroundService|AddHostedService|IHostedService` under `apps/lazuar-pay/src` is **empty**. There is no dispatcher worker. There is no poll loop. Fulfill is a scoped function called from the inbound webhook handler.

### 1.5 `whsec_` and `X-Lazuar-Signature` on 8081 — inbound only

Every live `whsec_` / `X-Lazuar-Signature` hit under `apps/lazuar-pay` is **Plane A or Plane B inbound**, not Plane C:

| Hit | Plane | Job |
|-----|-------|-----|
| `GatewayCredentialRow.WebhookCiphertext` | B | PSP verify (Stripe `whsec_`, CHIP PEM, Billplz X-Signature, Xendit token, Razorpay HMAC) |
| `OrgSettingsRow.OneWebhookCiphertext` | A | One HMAC verify (`PUT /v1/orgs/{orgId}/one-webhook`) |
| `Pay:OneWebhookSecret` | A | one-shop process fallback |
| `Pay:StripeWebhookSecret` | B | Testing-only fallback |
| `Pay:TestWebhookSecret` | B | Test rail |
| `OneWebhookSignature.TryVerify` | A | Receive One’s POST |
| Merchant `GatewayPage.tsx` “Webhook secret” | B | Staff pastes **PSP** signing secret; UI prints `/v1/webhooks/{provider}/{orgId}` as the **callback Pay listens on** |

Merchant UI is explicit that the webhook path is **Pay inbound from the PSP**, not Pay outbound to the shop:

```319:327:apps/lazuar-pay-merchant/src/pages/org/GatewayPage.tsx
                <p className="text-xs leading-relaxed text-slate-500">
                  Webhook path:{' '}
                  <code>
                    /v1/webhooks/{editing}/{orgId}
                  </code>
                </p>
                <p className="text-xs text-slate-500">
                  Dashboard callback must be public https on Pay:PublicBaseUrl. This SPA does not know that
                  origin. Localhost will fail.
                </p>
```

Checkout SPA has one webhook sentence, and it is Plane B: the buyer waits for the **PSP → Pay** hop, not for Pay to POST anywhere.

```350:351:apps/lazuar-pay-checkout/src/App.tsx
              The processor success URL is not paid. Waiting for the webhook.
```

### 1.6 pay-spec `Webhooks` interface is inbound only

```406:430:packages/pay-spec/main.tsp
@route("/v1")
@tag("Webhooks")
interface Webhooks {
  /** Plane B. provider is stripe|chip|billplz|xendit|razorpay|test. 200 is paid `{ok}`, replay `{duplicate}`, or `{ignored}`. */
  @post
  @route("/webhooks/{provider}/{orgId}")
  psp(@path provider: string, @path orgId: string): PspWebhookResult;

  @post
  @route("/one/webhooks")
  one(
    @header("X-Lazuar-Signature") signature?: string,
    @header("X-Lazuar-Timestamp") timestamp?: string,
    @body body?: OneWebhookEvent,
  ): OneWebhookResult;

  /** Writer stores this shop's One `whsec_`. Process `Pay:OneWebhookSecret` is the one-shop fallback. */
  @put
  @route("/orgs/{orgId}/one-webhook")
  putOne(@path orgId: string, @body body: PutOneWebhook): OneWebhookView;

  @get
  @route("/orgs/{orgId}/one-webhook")
  getOne(@path orgId: string): OneWebhookView;
}
```

No `POST /v1/orgs/{orgId}/webhooks`. No outbound envelope model. No `payment.completed` union. `Money` is GET-only (`listPayments`, `listReceipts`, `getReceipt`). That is the poll door, not the push door.

README on this SHA is honest about Plane A and silent about Plane C:

```69:69:apps/lazuar-pay/README.md
Pay never holds a Zitadel PAT. Staff **VIEWER** is not a One tenant role (`owner` / `admin` / `member` only); `/v1/orgs/{orgId}/ready` checks `member` and then whether the shop can take money (not `charges_paused`, plus a vault row or Test in Dev/Testing). `POST /v1/checkouts` requires writer. Unversioned `GET /ready` is a host probe, not org ready. One pause/reactivate HMAC is per-org `PUT /v1/orgs/{orgId}/one-webhook`; process `Pay__OneWebhookSecret` is the one-shop fallback. Pay does not POST One `/tenants/{id}/webhooks`.
```

“Pay does not POST One `/tenants/{id}/webhooks`” is Plane A **registration** (Pay as One’s customer). It is not Plane C. The sentence does not claim Pay POSTs `payment.completed` to anyone. It does not.

### 1.7 Honest empty set

| Thing a second app needs | Live on `6d730d15` under focused Pay? |
|--------------------------|----------------------------------------|
| Table of merchant webhook URLs | **No** |
| Per-org signing secret minted by Pay (`whsec_` shown once) | **No** |
| HttpClient POST of a signed envelope to that URL | **No** |
| Event outbox (not `mail_outbox`) | **No** |
| Dispatcher / retry worker | **No** |
| Route `PUT/POST /v1/orgs/{orgId}/webhooks` | **No** |
| TypeSpec event `payment.completed` | **No** |
| Merchant UI to paste **their** receiver URL | **No** (the webhook field is the PSP secret) |
| Checkout SPA subscribe | **No** (it polls `GET /v1/pay/{token}`) |

Empty set. The rest of this paper is: what fulfillment does instead, what Hub and One already know, which catalog a second app actually needs, and the smallest hatch.

---

## 2. What fulfillment does after paid — and what a second app can subscribe to

### 2.1 The only paid writer

Inbound Plane B handler verifies, inserts `psp_webhook_events`, then calls `IFulfillPaid` **in the same Postgres transaction**. That is the Linux room. There is no `PublishAsync`. IsolationTests ban the Hub event that used to be the hop:

```8:10:apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs
        "MediatR", "Modules.One", "BuildingBlocks", "IPaymentGatewayAdapter", "PaymentGatewayFactory",
        "IPaymentGatewayFactory", "AddPaymentsModule", "GatewayPaymentCompletedIntegrationEvent", "Modules.Payments",
```

Handler (commit is after fulfill, not after a bus):

```143:173:apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.PspWebhookEvents.Add(new PspWebhookEventRow
            {
                OrgId = orgId,
                Provider = name,
                EventId = parsed.EventId,
                ReceivedAt = DateTimeOffset.UtcNow
            });
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
            // Dedicated type so this cannot be shadowed by InvalidOperationException → 500.
            await tx.RollbackAsync(ct);
            return PayErrors.Status(409, "Conflict", "Org charges are paused");
        }
        catch (InvalidOperationException)
        {
            await tx.RollbackAsync(ct);
            return PayErrors.Status(500, "Internal Server Error", "fulfill failed");
        }

        return Results.Json(new { ok = true }, OneClient.Json);
```

### 2.2 `Fulfillment.FulfillPaidCoreAsync` — live rows, this SHA

Gate: per-checkout `SemaphoreSlim`, then:

1. Load checkout. Missing / `Amount <= 0` / `Status != "open"` → return (idempotent no-op).
2. `OrgSettings.ChargesPaused` → throw `ChargesPausedException` (409, event row rolled back).
3. If the checkout is a payment-link child and the parent is already full of **paid** children → set this checkout `expired`, save, return. **No charge. No journal. No RCPT. No mail. No outbound.**
4. Else mark `paid` and write, in one `SaveChanges`:
   - `charges` (unique on `CheckoutId` since `20260828001217_FulfillmentUniques`)
   - optional `payers`
   - optional `subscriptions` if `Interval is "mo" or "yr"` (mint doors currently force `one_off`; dead branch)
   - `journal_entries` + two `journal_lines` (`cash` D / `revenue` C, same amount)
   - `document_sequences` bump + `documents` Official Receipt `RCPT-{MYT year}-{#####}`
   - `audit_events` action **`checkout.paid`**
5. `DbUpdateException` detaches — unique race becomes a silent no-op at the fulfill layer; the webhook handler maps that to `{ duplicate: true }`.

Live write of the paid facts:

```72:166:apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs
        checkout.Status = "paid";
        db.Charges.Add(new ChargeRow
        {
            Id = Guid.NewGuid().ToString("N"),
            OrgId = checkout.OrgId,
            CheckoutId = checkout.Id,
            Provider = provider,
            ProviderRef = providerRef,
            Amount = checkout.Amount,
            Currency = checkout.Currency,
            Status = "paid"
        });

        string? payerId = null;
        if (!string.IsNullOrWhiteSpace(checkout.PayerEmail) || !string.IsNullOrWhiteSpace(checkout.PayerName))
        {
            payerId = Guid.NewGuid().ToString("N");
            db.Payers.Add(new PayerRow
            {
                Id = payerId,
                OrgId = checkout.OrgId,
                Email = checkout.PayerEmail,
                Name = checkout.PayerName
            });
        }

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

        var entryId = Guid.NewGuid().ToString("N");
        db.JournalEntries.Add(new JournalEntryRow
        {
            Id = entryId,
            OrgId = checkout.OrgId,
            CheckoutId = checkout.Id,
            Currency = checkout.Currency,
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.JournalLines.Add(new JournalLineRow
        {
            Id = Guid.NewGuid().ToString("N"),
            EntryId = entryId,
            Account = "cash",
            Dc = "D",
            Amount = checkout.Amount
        });
        db.JournalLines.Add(new JournalLineRow
        {
            Id = Guid.NewGuid().ToString("N"),
            EntryId = entryId,
            Account = "revenue",
            Dc = "C",
            Amount = checkout.Amount
        });

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
            Id = Guid.NewGuid().ToString("N"),
            OrgId = checkout.OrgId,
            CheckoutId = checkout.Id,
            Number = number,
            Title = "Official Receipt",
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.AuditEvents.Add(new AuditEventRow
        {
            Id = Guid.NewGuid().ToString("N"),
            OrgId = checkout.OrgId,
            Action = "checkout.paid",
            At = DateTimeOffset.UtcNow
        });

        try
        {
            await db.SaveChangesAsync(ct);
```

`audit_events.Action = "checkout.paid"` is an **in-process** audit row. It is not an HMAC envelope. 019 quoted this at old line numbers (`121:127`); on `6d730d15` the insert is **156–162**. Same fact.

What fulfill does **not** write:

- `MailOutboxRow`
- any webhook delivery row
- checkout `Status = "failed"` (that status does not exist as a writer in `apps/lazuar-pay/src`)
- refund rows (zero `refund` tokens in the focused host)

Occupancy-full path inside fulfill, the one a second app might have wanted as `occupancy.full` / `checkout.expired`:

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

That save is still in-process. The PSP is ACKed `{ ok: true }` after commit. The second app never hears it.

### 2.3 `mail_outbox` — table exists, writer does not

Initial migration created the table:

```171:184:apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260821152601_Initial.cs
                name: "mail_outbox",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    OrgId = table.Column<string>(type: "text", nullable: false),
                    ToEmail = table.Column<string>(type: "text", nullable: true),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
```

Row type:

```172:179:apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs
public sealed class MailOutboxRow
{
    public required string Id { get; set; }
    public required string OrgId { get; set; }
    public string? ToEmail { get; set; }
    public required string Kind { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

`rg MailOutbox` under `apps/lazuar-pay` hits **only** the row, the DbSet, and designers. `Fulfillment` never inserts. There is no mail worker. 011 `NP-MAIL-001` (“Receipt email after paid”) is still todo. 019 G12 named this. Still true.

**Do not** overload `mail_outbox.kind` with `payment.completed`. Receipts and HMAC deliveries have different retry, different secrets, different SSRF, different 2xx meaning. Same-transaction insert of a **new** delivery table is the hatch; stuffing the mail table is the cathedral.

### 2.4 What a second app can do today besides GET payments/receipts

**Staff poll (human JWT):**

```10:14:apps/lazuar-pay/src/Lazuar.Pay/Money/Queries/PaymentQueryEndpoints.cs
        app.MapGet("/v1/orgs/{orgId}/payments", List);
        app.MapGet("/v1/orgs/{orgId}/receipts", ListReceipts);
        app.MapGet("/v1/orgs/{orgId}/receipts/{id}", Receipt);
```

Both list doors call `MemberGate.RequireMemberAsync` — One membership, Bearer. There is no machine key on 8081 (that is 020 paper 02). A stranger app that is not a One human **cannot** even poll.

Merchant SPA loads payments **once per mount**, not on an interval:

```48:65:apps/lazuar-pay-merchant/src/pages/org/PaymentsPage.tsx
  useEffect(() => {
    let stop = false
    setLoaded(false)
    setListError(null)
    payJson<Payment[]>(token, `/v1/orgs/${orgId}/payments`, { orgHint: orgId })
      .then((rows) => {
        if (stop) return
        setPayments(rows)
        setLoaded(true)
      })
```

That is a dashboard refresh, not an integration. 018’s sentence still applies: until a second app that is not `:5178` can mint a checkout with a machine token **and** get a signed `payment.completed`, Pay is a hosted cashier.

**Buyer poll (no auth):** `:5179` polls `GET /v1/pay/{token}` while `?status=verifying`. That is UX for the cardholder. It is not a second-app door. Success URL is not paid. The other app cannot sit in the buyer’s browser.

**No other hook.** No WebSocket. No SSE. No `GET /v1/orgs/{orgId}/events`. No outbox cursor. Plane C is the missing door.

### 2.5 Charge / document / checkout fields a payload would copy

Live rows a `payment.completed` body would draw from (not a proposed DTO — the facts that exist):

| Source | Fields |
|--------|--------|
| `CheckoutRow` | `Id`, `OrgId`, `PublicToken`, `Amount`, `Currency`, `Status` (`open` / `paid` / `expired`), `Interval`, `Provider`, `ProviderSessionId`, `PaymentLinkId`, `SlotKey`, `ProductId`, `PayerName`, `PayerEmail`, `CreatedAt` |
| `ChargeRow` | `Id`, `CheckoutId`, `Provider`, `ProviderRef`, `Amount`, `Currency`, `Status` (`paid`) |
| `DocumentRow` | `Id`, `Number` (`RCPT-…`), `Title` (`Official Receipt`), `CheckoutId`, `CreatedAt` |
| `JournalEntryRow` | `Id`, `CheckoutId`, `Currency`, `CreatedAt` — two lines, balanced |
| `PayerRow` | `Email`, `Name` (only if checkout had them) |
| `AuditEventRow` | `Action = checkout.paid` |

Unique: one charge per checkout, one document per checkout. Idempotency of “this checkout is paid” is already a unique index. Plane C’s idempotency key should be **the Pay event id**, not the PSP event id (Plane B already consumed that). Natural candidate: a new GUID stored on the delivery row, **or** a stable `payevt_{checkoutId}_completed` so a retry of fulfill cannot mint a second type. Fulfill itself is already unique on checkout; the outbox insert must use a unique `(endpoint_id, event_id)` the way One does.

---

## 3. Old Hub outbound (museum) — steal judgment, do not copy types

Museum lives in this repo under `apps/lazuar-api`. IsolationTests exist so Plane C does not resurrect it as a project reference. `plans/006-sample/05-webhook-verify-nextjs.md` is the sample paper that already extracted the algorithm. This section steals that judgment.

### 3.1 Algorithm (Hub `OutboundWebhookSignature`)

```8:23:apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookSignature.cs
/// Standard Webhooks–style signing: <c>t={unix},v1={hmac_hex}</c> over <c>{timestamp}.{body}</c>.
public static string ComputeHeaderValue(string secret, string body, long unixTimestampSeconds)
{
    var signedPayload = $"{unixTimestampSeconds}.{body}";
    var keyBytes = Encoding.UTF8.GetBytes(secret);
    var payloadBytes = Encoding.UTF8.GetBytes(signedPayload);
    using var hmac = new HMACSHA256(keyBytes);
    var hash = hmac.ComputeHash(payloadBytes);
    var hex = Convert.ToHexString(hash).ToLowerInvariant();
    return $"t={unixTimestampSeconds},v1={hex}";
}
```

Judgment to steal:

- Sign **`{unix_seconds}.{raw_body}`**, not a re-serialized JSON object.
- HMAC-SHA256. Hex lowercase.
- Secret is the **full UTF-8 string**, including `whsec_` if present. **Not** Stripe’s “strip prefix then base64-decode.”
- Default skew **300s**.
- Fixed-time hex compare.
- Header name `X-Lazuar-Signature`.

Hub packs `t` and `v1` in **one** header. Product One later split them (`v1=` in `X-Lazuar-Signature`, unix in `X-Lazuar-Timestamp`). Pay’s **inbound** Plane A verifier already accepts both (see §4.2). That is the dialect a second Lazuar app already implements if it talks to One.

Hub called this “Standard Webhooks–style.” It is **not** the Standard Webhooks spec (see §9.3). Steal the *idea* (timestamp + HMAC over raw body). Do not claim spec compliance Hub never had.

### 3.2 Headers and raw body (Hub dispatcher)

```93:107:apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookDispatcherJob.cs
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url);
                var unixTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var signingSecret = ResolveSigningSecret(vault, endpoint);
                var signature = OutboundWebhookSignature.ComputeHeaderValue(
                    signingSecret,
                    delivery.Payload,
                    unixTs);

                request.Headers.TryAddWithoutValidation("X-Lazuar-Signature", signature);
                request.Headers.TryAddWithoutValidation("X-Lazuar-Event", delivery.EventType);
                request.Headers.TryAddWithoutValidation("X-Lazuar-Delivery-Id", delivery.Id.ToString());
                request.Headers.TryAddWithoutValidation("X-Lazuar-Webhook-Id", endpoint.Id.ToString());
                request.Content = new StringContent(delivery.Payload, Encoding.UTF8, "application/json");
```

Judgment:

- POST JSON. Raw stored payload bytes, not a new serialize at send time (Hub stored the string it signed conceptually; One stores `PayloadJson` and signs those bytes).
- Type in a header **and** in the body. Receiver may key off either.
- Delivery id ≠ event id. Delivery is the attempt/log row. Event is the fact. At-least-once retries the **same** event with a **new** delivery id (Hub actually reused the same outbox row and bumped attempts — One splits event vs delivery more cleanly).
- 2xx = success. 4xx = Hub treats as **permanent** (`IsPermanentHttpFailure` = any 4xx). 5xx / transport retry.

`006-sample/05` already told the Next.js sample: never `request.json()` before HMAC; use `req.text()`. That law survives.

### 3.3 Retry (Hub `WebhookDeliveryOutbox`)

```55:75:apps/lazuar-api/Modules/One/Domain/WebhookDeliveryOutbox.cs
    public void RecordFailure(string error)
    {
        AttemptCount++;
        LastError = error;
        if (AttemptCount >= 5)
        {
            Status = "FAILED";
        }
        else
        {
            NextAttemptAt = DateTime.UtcNow.AddMinutes(Math.Pow(2, AttemptCount));
        }
    }

    /// <summary>401 / 422 / other 4xx — fail immediately (secret or payload bug; do not hide in backoff).</summary>
    public void RecordPermanentFailure(string error)
    {
        AttemptCount++;
        LastError = error;
        Status = "FAILED";
    }
```

Judgment: at-least-once, exponential backoff, cap, 4xx is the receiver rejecting the **contract** (wrong secret, 422 mapping bug) so do not hammer. Claim with `FOR UPDATE SKIP LOCKED` (Hub grew this after the 001-gaps paper said it was missing). Poll ~10s. Do not fire HTTP inside the money transaction.

Hub’s disease, not to steal: `IEventBus` + `OutboundWebhookRequestedIntegrationEvent` + Payments handler that publishes after marking an `IntegrationCheckoutSession`. New Pay already killed that hop. Plane C attaches to `Fulfillment.SaveChanges`, not to a bus.

### 3.4 Envelope and catalog (Hub)

Envelope wrap in `OutboundWebhookEventHandlers`:

```55:71:apps/lazuar-api/Modules/One/Infrastructure/EventHandlers/OutboundWebhookEventHandlers.cs
        var jsonPayload = JsonSerializer.Serialize(new
        {
            id = Guid.CreateVersion7().ToString(),
            event_type = @event.EventType,
            created_at = DateTime.UtcNow,
            data = @event.Payload
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
```

Inner `data` for M2M payment events, `IntegrationCheckoutGatewayEventsHandler.BuildPayload`:

```206:218:apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/IntegrationCheckoutGatewayEventsHandler.cs
        var payloadObj = new
        {
            event_id = eventId.ToString(),
            checkout_id = session.Id.ToString(),
            gateway = session.GatewayName,
            gateway_transaction_id = gatewayTransactionId,
            provider_session_id = session.ProviderSessionId,
            amount,
            currency,
            status,
            metadata,
            description = session.Description,
            customer_email = session.CustomerEmail
        };
```

Event types Hub actually produced for the cashier: **`payment.completed`**, **`payment.failed`**. Commerce also produced `subscription.*`, `order.completed`, `payment_link.paid` through the same dispatcher. That sprawl is why 018 said the kernel is one object. Pay v1 catalog is not Hub’s five SaaS names.

Hub TypeSpec `PaymentWebhookPayloadDto` was **flat** and marked orphan. Runtime was envelope+data. Steal: **document the runtime envelope**. Do not ship a tsp model that lies.

Sample verify helper (`examples/hub-cashier-next/lib/webhook-verify.ts`) is museum for Hub. A Pay sample (paper 09) should verify **Pay’s** dialect, not keep `HUB_WEBHOOK_SECRET`.

### 3.5 Hub registration / secret

`TenantWebhookEndpoint`: URL + `SecretKey` + `EnabledEvents` (empty = all) + `IsActive`. Secret minted `whsec_` + token. Later GET leaked plaintext in old trees; later encrypt-at-rest. Rotate existed as `RotateSecret` on the aggregate.

Do not copy the class. Steal: secret shown once, prefix on list, empty filter = all **of a closed catalog**, disable without deleting delivery history.

### 3.6 What not to steal

- `Modules.One` namespaces, MediatR, `IEventBus`, `OutboundWebhookRequestedIntegrationEvent`.
- `TargetUrl` exact-match silent drop (001-gaps / 18-outbound-customer-webhooks). Fan-out by org + event type, like live One, not by product URL equality.
- Commerce lifecycle types as Pay events.
- `DecryptOrPlaintext` leftover `whsec_` rows (013-06 anti-goal 21).
- Dual-write: Pay must not enqueue into Hub `one.WebhookDeliveryOutboxes`.

---

## 4. One’s outbound product (sibling) — pattern Pay could mirror without copying code

Sibling SHA `6b78e9d4`. This is a **working** HMAC product: CRUD, `whsec_` once, closed catalog, SSRF, outbox fan-out, SKIP LOCKED dispatcher, delivery log, rotate, test ping. Pay is already Consumer-0 of the **inbound** side of this dialect. Plane C should look like One’s door so a second app that already verifies One can verify Pay with the same helper.

Pay must **not** reuse `Webhooks:SigningSecretEncryptionKey`. Pay wraps with `Pay:WrapKey` / `SecretBox`. Different product, different key, different AAD.

### 4.1 Closed catalog

```6:48:/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/Webhooks/WebhookEventCatalog.cs
public static class WebhookEventCatalog
{
    public const string ApiVersion = "v1";

    public const string TenantCreated = "tenant.created";
    public const string TenantDeleted = "tenant.deleted";
    public const string TenantSuspended = "tenant.suspended";
    public const string TenantReactivated = "tenant.reactivated";
    public const string MemberInvited = "member.invited";
    public const string MemberAccepted = "member.accepted";
    public const string MemberRemoved = "member.removed";
    public const string MemberLeft = "member.left";
    public const string MemberRoleChanged = "member.role_changed";
    public const string OwnershipTransferred = "ownership.transferred";
    public const string ApiKeyCreated = "api_key.created";
    public const string ApiKeyRevoked = "api_key.revoked";
    public const string OidcAppCreated = "oidc_app.created";
    public const string OidcAppRevoked = "oidc_app.revoked";
    public const string InviteRevoked = "invite.revoked";
    public const string InviteResent = "invite.resent";
    public const string WebhookTest = "webhook.test";

    public static readonly IReadOnlyList<string> All = new[]
    {
        TenantCreated, TenantDeleted, TenantSuspended, TenantReactivated,
        MemberInvited, MemberAccepted, MemberRemoved, MemberLeft, MemberRoleChanged,
        OwnershipTransferred, ApiKeyCreated, ApiKeyRevoked, OidcAppCreated, OidcAppRevoked,
        InviteRevoked, InviteResent, WebhookTest,
    };
```

Judgment: **closed list**. Unknown type on create → 400 with the allowed list. Empty filter → all catalog types. Fan-out **refuses** unknown types even if filter is empty (`WebhookFanoutOutboxHandler` 47–51). `webhook.test` is a ping, inserted as a delivery directly, not via domain outbox.

Pay’s catalog is a **different closed list**. Do not emit One’s `tenant.*` from Pay. Do not emit Pay’s `payment.*` from One.

### 4.2 Signing — the dialect Pay already verifies inbound

```6:26:/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Webhooks/WebhookSigning.cs
/// HMAC-SHA256 webhook signatures.
/// signed_payload = "{unix_seconds}.{raw_body}"; header = "v1=" + hex(digest).
public static class WebhookSigning
{
    public static string ComputeSignature(string secret, long unixSeconds, ReadOnlySpan<byte> body)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);

        var prefix = Encoding.UTF8.GetBytes(unixSeconds.ToString() + ".");
        var message = new byte[prefix.Length + body.Length];
        prefix.CopyTo(message.AsSpan());
        body.CopyTo(message.AsSpan(prefix.Length));

        var key = Encoding.UTF8.GetBytes(secret);
        var hash = HMACSHA256.HashData(key, message);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string FormatHeader(string hexDigest) => "v1=" + hexDigest;
```

Dispatcher attaches split headers (live One, this SHA):

```122:141:/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Webhooks/WebhookDispatcher.cs
        var bodyBytes = Encoding.UTF8.GetBytes(delivery.PayloadJson);
        var unixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var sigHex = WebhookSigning.ComputeSignature(secret, unixSeconds, bodyBytes);
        var signatureHeader = WebhookSigning.FormatHeader(sigHex);
        // ...
        request.Headers.TryAddWithoutValidation("X-Lazuar-Event-Id", delivery.EventId.ToString());
        request.Headers.TryAddWithoutValidation("X-Lazuar-Event-Type", delivery.EventType);
        request.Headers.TryAddWithoutValidation("X-Lazuar-Tenant-Id", delivery.TenantId.ToString());
        request.Headers.TryAddWithoutValidation("X-Lazuar-Timestamp", unixSeconds.ToString());
        request.Headers.TryAddWithoutValidation("X-Lazuar-Signature", signatureHeader);
        request.Headers.TryAddWithoutValidation("X-Lazuar-Delivery-Id", delivery.Id.ToString());
```

Pay inbound already implements the **verify** side of this exact dialect, plus Hub’s combined header as compat:

```6:11:apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookSignature.cs
/// Product One signs <c>X-Lazuar-Signature: v1=&lt;hex&gt;</c> and
/// <c>X-Lazuar-Timestamp</c> over <c>{unix}.{body}</c>. Combined
/// <c>t=&lt;unix&gt;,v1=&lt;hex&gt;</c> in one header is accepted as compat.
/// Raw body hex is rejected.
```

```42:45:apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookSignature.cs
        var signedPayload = $"{timestamp}.{body}";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signedPayload));
        var expectedHex = Convert.ToHexString(hash).ToLowerInvariant();
        return FixedTimeEqualsHex(v1Hex, expectedHex);
```

That is the company dialect. A Node sample already exists on the One side (`examples/node-webhook-verify/server.mjs`, recipe R5). Plane C should emit what those files already verify.

Envelope One stores and POSTs:

```8:24:/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Outbox/OutboxEnvelope.cs
public sealed class OutboxEnvelope
{
    public string Id { get; init; } = "";
    public string Type { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
    public Guid TenantId { get; init; }
    public string ApiVersion { get; init; } = "v1";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OriginRequestId { get; init; }
    public object Data { get; init; } = new { };
}
```

Snake_case JSON. Hub used `event_type`; One uses `type`. Pay inbound `ApplyAsync` reads `type`. Plane C should use **`type`**, not Hub’s `event_type`, so the same parser works. Receivers that only know Hub `event_type` are museum.

Idempotency: `X-Lazuar-Event-Id` = outbox / event GUID. `X-Lazuar-Delivery-Id` is the attempt. Pay inbound keys `one_webhook_events.DeliveryId` on body `id` else header event id. Plane C receivers should key on **event id**, ACK 2xx even on replay.

### 4.3 `whsec_` shown once, prefix on list, rotate kills the old secret immediately

```16:16:/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/Webhooks/WebhookService.cs
    public const string SecretPrefixLiteral = "whsec_";
```

```75:113:/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/Webhooks/WebhookService.cs
        var secret = GenerateSecret();
        var prefix = secret.Length >= 16 ? secret[..16] : secret;
        var endpointId = Guid.CreateVersion7();
        // ...
            SecretEncrypted = _protector.Protect(secret, endpointId),
            SecretPrefix = prefix,
        // ...
        return new WebhookEndpointCreated(entity, secret);
```

```431:436:/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/Webhooks/WebhookService.cs
    internal static string GenerateSecret()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return SecretPrefixLiteral + Base64UrlEncode(bytes);
    }
```

Create DTO includes `Secret = secret`. List/get `MapMeta` does **not**. Rotate remints, wraps, returns once; previous secret stops verifying on the next dispatch (no dual-window).

HTTP surface (steal the **shape**, not the path prefix — Pay is `/v1/orgs/{orgId}/…` not `/tenants/{guid}/…`):

```16:54:/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/Webhooks/WebhookEndpoints.cs
        var group = api.MapGroup("/tenants/{tenantId:guid}/webhooks")
            .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser);

        group.MapPost("/", CreateWebhook)
            .WithName("CreateWebhook")
            .WithSummary("Create webhook endpoint (secret returned once)");

        group.MapGet("/", ListWebhooks)
            .WithName("ListWebhooks")
            .WithSummary("List webhook endpoints");

        group.MapGet("/{webhookId:guid}", GetWebhook)
            .WithName("GetWebhook")
            .WithSummary("Get webhook endpoint");

        group.MapPatch("/{webhookId:guid}", UpdateWebhook)
            .WithName("UpdateWebhook")
            .WithSummary("Update webhook endpoint");

        group.MapDelete("/{webhookId:guid}", DeleteWebhook)
            .WithName("DeleteWebhook")
            .WithSummary("Delete webhook endpoint");

        group.MapPost("/{webhookId:guid}/rotate-secret", RotateSecret)
            .WithName("RotateWebhookSecret")
            .WithSummary("Rotate signing secret (returned once)");

        group.MapPost("/{webhookId:guid}/test", TestWebhook)
            .WithName("TestWebhook")
            .WithSummary("Enqueue webhook.test delivery");

        group.MapGet("/{webhookId:guid}/deliveries", ListDeliveries)
            .WithName("ListWebhookDeliveries")
            .WithSummary("List deliveries for endpoint");

        api.MapGet("/webhook-event-types", ListEventTypes)
            .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
            .WithName("ListWebhookEventTypes")
            .WithSummary("List closed v1 webhook event types");
```

Write is JWT `admin|owner` or API key `webhooks:write`. Pay’s analogue is `MemberGate.RequireWriterAsync` (`owner` / `admin`). Members must not mint a signing secret.

### 4.4 SSRF URL rules — One blocks loopback; Pay laptop dogfood must not copy that blindly

```190:278:/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Webhooks/WebhookUrlValidator.cs
    internal static bool IsBlockedAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            // 0.0.0.0/8
            if (bytes[0] == 0)
            {
                return true;
            }

            // 10.0.0.0/8
            if (bytes[0] == 10)
            {
                return true;
            }

            // 127.0.0.0/8
            if (bytes[0] == 127)
            {
                return true;
            }

            // 169.254.0.0/16 link-local / cloud metadata
            if (bytes[0] == 169 && bytes[1] == 254)
            {
                return true;
            }

            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            {
                return true;
            }

            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168)
            {
                return true;
            }

            // 100.64.0.0/10 CGNAT
            if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127)
            {
                return true;
            }

            // broadcast
            if (bytes[0] == 255 && bytes[1] == 255 && bytes[2] == 255 && bytes[3] == 255)
            {
                return true;
            }

            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // ::1 handled by IsLoopback
            // fc00::/7 unique local
            var bytes = address.GetAddressBytes();
            if ((bytes[0] & 0xfe) == 0xfc)
            {
                return true;
            }

            // fe80::/10 link-local
            if (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80)
            {
                return true;
            }

            if (address.Equals(IPAddress.IPv6Any))
            {
                return true;
            }
        }

        return false;
    }
```

Also: absolute URI, no userinfo, https (http only in Development/Testing when `AllowHttpInDevelopment`), port 443 in strict env, DNS rebind re-check at delivery, connect pin to the resolved address, no redirects (`AllowAutoRedirect = false`).

013-08 already warned Pay-as-One-customer: `http://localhost:8081/v1/one/webhooks` **fails One SSRF** unless allowlisted. The same rule, copied into Pay Plane C without a Testing hatch, **fails the second-app sample on a laptop** (`http://127.0.0.1:3456/webhooks/pay`).

Judgment to steal: block link-local / cloud metadata / RFC1918 **in Staging/Production**. Hatch for Dev/Testing: allow http + loopback + RFC1918 so `examples/` and Aura on the same docker network can dogfood. Host allowlist is the staff escape, not the default. Do not weaken One’s validator from Pay — this is Pay’s own egress policy.

Timeout: One `HttpTimeoutSeconds = 10`. User-Agent `Lazuar-One-Webhooks/1.0`. Pay’s would be `Lazuar-Pay-Webhooks/1.0`. Receiver must 2xx within that window.

### 4.5 Delivery log, claim, backoff, auto-disable

Fan-out is an **outbox handler**, not HTTP:

```12:15:/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Webhooks/WebhookFanoutOutboxHandler.cs
/// Outbox consumer: inserts pending <see cref="WebhookDelivery"/> rows for matching active endpoints.
/// Does not perform HTTP (dispatcher owns delivery).
```

Unique `(endpoint_id, event_id)` so at-least-once re-handle is quiet. Dispatcher claims with `FOR UPDATE SKIP LOCKED`, lease ≥ batch × timeout, attempt++. Backoff:

```27:31:/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Webhooks/WebhookDispatcher.cs
    /// Backoff seconds indexed by attempt count after failure (index 0 unused).
    /// attempt 1→30s, 2→2m, 3→10m, 4→1h, 5→6h, 6→24h.
    private static readonly int[] BackoffSeconds = [0, 30, 120, 600, 3600, 21600, 86400];
```

`MaxAttempts = 7`. Auto-disable after 15 consecutive failures. Delivery log: status, attempt_count, last_http_status, last_error, response_snippet (truncated), duration_ms, next_attempt_at. Worker poll 1s. Tests disable the worker and call `ProcessBatchAsync`.

Encryption: AES-256-GCM, AAD = endpoint id, key from `Webhooks:SigningSecretEncryptionKey`. **Pay must not read that config.** Pay `SecretBox` is a different wrap (nonce||tag||cipher, `Pay:WrapKey`, no AAD today). Plane C secrets go through `SecretBox` like BYOK and One inbound `whsec_`. Optional later: bind AAD to endpoint id; not required for the hatch.

### 4.6 Pattern vs copy

Mirror:

- Closed catalog constant class (Pay-owned names).
- `whsec_` + 32 random bytes, shown once, prefix stored.
- Writer-only register / rotate / delete.
- Outbox row in the same DB as the domain write.
- Worker signs One dialect, POST, 2xx, retry, log.
- SSRF with a **Pay** Dev/Testing loopback hatch.

Do not copy:

- `Lazuar.One.Api` projects, `IOutbox`, `OutboxMessage`, MediatR-shaped publishers.
- One’s encryption key or `AesWebhookSecretProtector` type.
- One’s 17 identity event names.
- One’s GUID tenant ids as Pay `org_id` format requirement (Pay `org_id` is already the One tenant id **string**).
- Dual-verify rotation window (One does not have one; do not invent one).

---

## 5. Event catalog a second app actually needs from Pay

Hub cashier sold two money types. 018 still writes that on the slide. Live Pay has **one successful money writer** and **no failed checkout status**. Friends named in the assignment are ranked against live rows, not against Hub’s Commerce list.

Idempotency key column below is the **receiver** key (header `X-Lazuar-Event-Id` / body `id`). Delivery id is not it.

### 5.1 `payment.completed` — the only v1 type

**When it would fire.** After `Fulfillment` successfully commits `checkout.Status = "paid"` plus charge + balanced journal + `RCPT-`. Same transaction as those rows: insert the outbox/delivery row. Not on PSP `{ ignored }`. Not on `{ duplicate }`. Not on occupancy-expire. Not on `Amount <= 0`. Not on `ChargesPaused` 409.

**Payload fields from live rows** (hatch: keep it boring; do not invent Hub `metadata.order_id` — that is the **second app’s** column):

| Field | Live source | Notes |
|-------|-------------|-------|
| `id` | new event GUID (or stable `pay:{checkout_id}:completed`) | Idempotency |
| `type` | `"payment.completed"` | Closed catalog |
| `created_at` | `DateTimeOffset.UtcNow` at fulfill | Envelope |
| `org_id` / `tenant_id` | `CheckoutRow.OrgId` | One tenant id string |
| `api_version` | `"v1"` | |
| `data.checkout_id` | `CheckoutRow.Id` | Unique paid unit |
| `data.charge_id` | `ChargeRow.Id` | |
| `data.amount` | `ChargeRow.Amount` (= checkout amount) | Major units, decimal |
| `data.currency` | `ChargeRow.Currency` | MYR |
| `data.status` | `"paid"` | Charge status |
| `data.provider` | `ChargeRow.Provider` | stripe\|chip\|…\|test |
| `data.provider_ref` | `ChargeRow.ProviderRef` | PSP session / payment id |
| `data.receipt_number` | `DocumentRow.Number` | `RCPT-2026-00001` |
| `data.receipt_id` | `DocumentRow.Id` | GET receipts/{id} |
| `data.payer_email` | `CheckoutRow.PayerEmail` | May be null |
| `data.payer_name` | `CheckoutRow.PayerName` | May be null |
| `data.payment_link_id` | `CheckoutRow.PaymentLinkId` | Null on standalone mint |
| `data.product_id` | `CheckoutRow.ProductId` | Optional |
| `data.public_token` | `CheckoutRow.PublicToken` | Only if a second app hosted the buyer; default omit |

Do **not** put Hub `metadata` as a first-class Pay map until mint doors accept it. Today `CreateCheckoutRequest` in pay-spec has no metadata. Stuffing a second app’s `order_id` is paper 01’s `/v1` problem, not a reason to delay `payment.completed`. The second app keys its own order by `checkout_id` it received at mint, or it polls. That is enough for Aura.

**Idempotency key:** `id` = stable function of `(org_id, checkout_id, type)`. Unique `(endpoint_id, event_id)` on the delivery table. Receiver stores `id` and no-ops replays. PSP event id is **not** this key.

### 5.2 `payment.failed` — not a Pay fact yet

Live PSP parsers **ignore** failure types. They do not mark a checkout failed.

| Rail | Failure input | Live result |
|------|---------------|-------------|
| Stripe | anything other than `checkout.session.completed` / `async_payment_succeeded` with paid | `{ ignored: type }` |
| Stripe unpaid `checkout.session.completed` | `{ ignored: payment_status:unpaid }` |
| CHIP | `purchase.payment_failure` | `{ ignored: payment_failure }` |
| Billplz | `paid=false` | `{ ignored: unpaid }` |
| Xendit | not PAID | `{ ignored: status }` |
| Razorpay | `payment.failed` | `{ ignored: payment_failed }` |
| Test | wrong amount | 400, event **not** consumed |

Checkout statuses written in src: `open`, `paid`, `expired`. **No `failed`.** A `payment.failed` event would be a lie or a new writer. Hub had `GatewayPaymentFailedIntegrationEvent` → `session.MarkFailed()` → outbound. New Pay refused that hop and did not replace the fact.

**When it would fire (later).** When a rail has a verified, amount-matching, checkout-bound failure and Pay decides `open → failed` without booking cash. Not v1. Second app treats “no `payment.completed`” as unpaid and uses its own timeout. Occupancy TTL already expires stale `open` children; that is `checkout.expired`, not failed.

Do not emit `payment.failed` from `{ ignored }`. Ignored means Pay did not change money.

### 5.3 `checkout.expired` — live status, no subscriber

Writers of `expired`:

- Fulfill occupancy-full of **paid** seats (`Fulfillment.cs` 65–66).
- `PaymentLinkOccupancy.ExpireStaleAsync` (open older than TTL, default 30 min).
- `ExpireOpenAsync` when `ChargesPaused` on public GET.
- `ExpireFailedReservation` when Stripe rejects the org key **before** a hosted URL is stored.

**When it would fire (later, maybe).** After those saves, if a second app reserved a seat and needs to release its own hold. Hatch v1: skip. The second app can GET the checkout (once it has a machine key) or TTL itself in parallel. Emitting this without a machine-key GET is still a push the app cannot reconcile if it missed the POST.

**Payload (if later):** `checkout_id`, `org_id`, `payment_link_id`, `reason` (`occupancy_full` | `reservation_ttl` | `charges_paused` | `psp_start_rejected`). Idempotency: `pay:{checkout_id}:expired`.

### 5.4 `occupancy.full` — live as HTTP 409 / buyer `status=full`, not an event

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

Start path 409 `"This pay link is full"`. Merchant list uses `PaymentLinkOccupancy.MerchantStatus` → `full` / `over_capacity` / `open`.

**When it would fire (later).** When remaining crosses 0 after a **paid** fulfill (not after a reservation). One event per link per crossing, not per rejected start (that is a hot loop). Idempotency: `pay:{payment_link_id}:full:{paid_count}` or a monotonic version. Skip in v1. The Nth `payment.completed` with `payment_link_id` + `paid_count` (if we add it) is enough for an app that minted the link.

### 5.5 `charges.paused` — Plane A already has the SoT

Pay sets `OrgSettings.ChargesPaused` from One `tenant.suspended` / `tenant.reactivated`. Effects: mint 409/403, webhook 409, fulfill throw, public GET expires open children.

A second app that is also a One customer **already** gets `tenant.suspended` from One. Pay re-emitting `charges.paused` is a duplicate control-plane. A second app that is **not** a One customer (pure Pay M2M later) would need it.

**When it would fire (later, only if Pay is sold without One membership):** after `ApplyAsync` flips the flag. Payload: `org_id`, `paused: true|false`, `source: "one.tenant.suspended"`. Idempotency: One’s event id (already on `one_webhook_events.DeliveryId`) — do not mint a second id if you are just forwarding.

v1 hatch: **do not**. Mixing Plane A facts into Plane C catalogs is how Hub confused three planes. If Aura is the second app, it is already subscribed to One.

### 5.6 `refund.created` — 07 owns money leftover; live absence

```
rg refund apps/lazuar-pay --glob '*.{cs,tsp}'
→ no matches
```

No refund table, no reverse journal, no PSP refund parse. 011 `NP-SOON-006` partial refunds; `NP-MON-005` “Refunds reverse the journal once (v1 later).” Emitting `refund.created` before a refund writer is fiction.

When 07 adds refunds: fire **after** the reverse journal commits, same outbox pattern, type `refund.created`, idempotency `pay:{refund_id}:created`. Not this slice.

### 5.7 Catalog close for the hatch

| Type | v1 hatch? | Why |
|------|-----------|-----|
| `payment.completed` | **Yes. Only type.** | The kernel. Money is true in Pay; the other app must hear it. |
| `webhook.test` | Optional same PR as register door | Prove HMAC without taking a card. Steal One’s ping. |
| `payment.failed` | No | No failed writer. |
| `checkout.expired` | No | Local TTL is enough until a second app holds seats. |
| `occupancy.full` | No | Infer from completed + max_payers, or GET the link. |
| `charges.paused` | No | One’s catalog, or later if Pay is standalone. |
| `refund.created` | No | 07. No rows. |
| Hub `subscription.*` / `order.completed` / `payment_link.paid` | Refuse | Commerce cathedral. Pay subscription row is a stub (`one_off` mint). |

Unknown types at register → 400. Empty filter on a one-type catalog = that type. Adding types later is a new constant + a new fulfill insert, not a bus.

---

## 6. Registration door missing

### 6.1 What does not exist

- `POST /v1/orgs/{orgId}/webhooks`
- `PUT /v1/orgs/{orgId}/webhooks`
- `GET /v1/orgs/{orgId}/webhooks`
- rotate, test, deliveries, event-types
- Merchant page “your receiver URL”
- Env `PAY_MERCHANT_WEBHOOK_URL` as a process-wide paste (would be the one-shop fallback, like `Pay:OneWebhookSecret`, and would be the wrong default for N orgs)

### 6.2 What must not be the door

**“Ops pastes URL” in a compose file** is how Plane A got a one-shop `Pay:OneWebhookSecret` fallback. For Plane C, a process env pointing at Aura is **first-party cheating**: it skips writer authz, skip-shown-once secret, skip-SSRF, skip-per-org. Acceptable only as a Testing hatch (`Pay:OutboundWebhookUrl` + `Pay:OutboundWebhookSecret`) the way Test rail exists. Not production.

**Reusing `PUT /v1/orgs/{orgId}/one-webhook`.** That stores **One’s** `whsec_` so Pay can **verify inbound**. Plane C mints **Pay’s** `whsec_` so the stranger can verify Pay. Same column would be a secret-plane collision. 012/09 standing law: do not share a table, a secret, or a route prefix across planes.

**Reusing `PUT /v1/orgs/{orgId}/gateway` webhook_secret.** That is the PSP’s signing secret. The merchant UI already confuses staff (“Webhook secret” next to `sk_test_`). Adding a receiver URL on that form would mix Plane B and Plane C in one card.

### 6.3 Hatch door (analysis, not tickets)

Steal One’s shape, Pay’s path grammar:

| Method | Path | Auth | Returns |
|--------|------|------|---------|
| `POST` | `/v1/orgs/{orgId}/webhooks` | Bearer + **writer** (`owner`/`admin`) | **201** `{ id, url, events, secret_prefix, secret }` — **secret once** |
| `GET` | `/v1/orgs/{orgId}/webhooks` | member | list metadata, **no raw secret** |
| `GET` | `/v1/orgs/{orgId}/webhooks/{id}` | member | metadata |
| `PATCH` | `/v1/orgs/{orgId}/webhooks/{id}` | writer | url / events / status |
| `DELETE` | `/v1/orgs/{orgId}/webhooks/{id}` | writer | 204 |
| `POST` | `/v1/orgs/{orgId}/webhooks/{id}/rotate-secret` | writer | 200 new `secret` once; previous stops |
| `POST` | `/v1/orgs/{orgId}/webhooks/{id}/test` | writer | 202 `{ delivery_id, event_id }` enqueues `webhook.test` |
| `GET` | `/v1/orgs/{orgId}/webhooks/{id}/deliveries` | member | log |
| `GET` | `/v1/webhook-event-types` | any authenticated member of some org, or public 200 of the closed list | `{ events: ["payment.completed"], api_version: "v1" }` |

Smaller hatch if POST+GET+rotate is still fat: **one endpoint per org** (`PUT /v1/orgs/{orgId}/webhook` singular) with url in, secret out once, GET returns `webhook_configured` + prefix + url host. That matches `one-webhook` and `gateway` singular habits on this host. Multi-endpoint (One’s max 10) is how Hub/One sprawled. A second app needs **one URL**. Start singular. Add list when a second URL is real (staging vs prod, or two apps). NP-SOON-007 is “first extra consumer,” not “Stripe-style endpoint fan-out.”

Writer only: `MemberGate.RequireWriterAsync` already exists and returns 403 `"Writer role required"` for `member`. Reuse it. Do not invent a Pay-native role.

Machine key: paper 02. Until `lzr_sk_` can call this door, only a human owner registers the URL. That is enough for Aura’s first engineer. It is not enough for a CI job. Do not block the table on M2M; do not pretend PUT from `:5178` is a kernel.

### 6.4 Secret shown once, rotate

Mint `whsec_` + 32 random bytes (One’s `GenerateSecret` judgment). Wrap with `SecretBox` (`Pay:WrapKey`). Store prefix `whsec_` + first 12 hex/base64 chars. GET never returns the raw secret (One inbound GET already returns only `webhook_configured` — copy that honesty). Rotate: new wrap, new prefix, old secret dead immediately. Cut during quiet. Accept one failed delivery during cut.

Do not log the secret. Do not put it on `NEXT_PUBLIC_`. Sample env is `PAY_WEBHOOK_SECRET=whsec_…` server-side only.

---

## 7. Delivery: outbox in the same transaction as fulfill vs fire-and-forget

### 7.1 Fire-and-forget is refused

HTTP inside `FulfillPaidCoreAsync` before `SaveChanges` makes the PSP webhook wait on a stranger. One’s timeout is 10s; Stripe retries Pay if Pay is slow. A hung Aura takes down CHIP callbacks. Hub already learned “do not ACK 200 before the unique insert.” Symmetric: do not ACK the PSP after a slow outbound POST.

HTTP after `SaveChanges` but in the same request without a row is **loss on crash**: money is true, the POST never happened, no retry. That is the parked-event tax 011 paid, inverted.

### 7.2 Hatch: row in the same TX, HTTP in a worker

Same `SaveChanges` as charge / journal / RCPT / `checkout.paid`:

1. If the org has an active endpoint whose filter matches `payment.completed` (or empty filter on a one-type catalog).
2. Insert `webhook_deliveries` (`pending`, `next_attempt_at = now`, `event_id` stable, `payload_json` already serialized).
3. Unique `(endpoint_id, event_id)` so a fulfill race cannot double-insert.
4. Commit with the money.

Worker (in-process `BackgroundService`, Testing can disable): claim SKIP LOCKED, re-validate URL, Unprotect secret, sign, POST, 2xx → succeeded, else backoff. **At-least-once.** Receiver must be idempotent on `id`.

Do not wait for the worker in the PSP handler. Plane B success is money in Pay. Plane C is eventual. 011/07 rule 2: “Pay must not POST One/grant-buyer-access in the webhook as the only fulfillment.” Symmetric: Pay must not POST Aura as the only fulfillment. Buyer access is the Pay row. Aura unlocks **after** verify.

### 7.3 Retry / timeout / 4xx

Steal One’s numbers unless a test proves otherwise: 10s HTTP timeout, no redirects, 7 attempts, 30s…24h backoff, ±10% jitter, snippet truncated, auto-disable after a consecutive-failure threshold so a dead URL does not fill the table forever. Hub’s “any 4xx is permanent” is harsher (a 429 would kill). One retries 4xx until max attempts then dead. Hatch: **4xx except 429 permanent** (Hub judgment for 401/422 — secret or mapping bug), **429/5xx/timeout retry**. Document that 422 means “I understood you and I refuse” so Pay stops.

Lease ≥ batch × timeout (One issue 035). Hub originally under-leased. Do not copy that bug.

### 7.4 SSRF vs laptop dogfood

Production: https, port 443, deny loopback / RFC1918 / link-local / metadata, pin connect, re-resolve at delivery.

Development/Testing: allow `http://127.0.0.1` and `http://localhost` and docker-compose service DNS. This is the **Pay** hatch. Do not ask One to allowlist Pay’s loopback. Do not ship `AllowHttpInDevelopment=true` in Production.

Billplz already taught the host that localhost callbacks 400 at the **PSP**. Plane C is the opposite direction: Pay is the client. Localhost as **destination** is the sample app. Different plane, different rule.

### 7.5 `mail_outbox` vs event outbox

Keep `mail_outbox` for `NP-MAIL-001` kind=`receipt`. New table for HMAC. Both inserts can share the fulfill `SaveChanges`. Two workers. Two secrets. Two SSRF policies (SES is not a merchant URL).

---

## 8. 011 / 012 / 018-evals: “Plane C is later / not v1 first-party” — still true on `6d730d15`?

Yes. Live absence in §1 is the proof. The papers named it; the SHA did not grow the door. 002 closed occupancy, Plane B HMAC, CORS, spec honesty, per-org One secret **as a hosted cashier**. Kernel doors were out of 002. 020’s README says so.

### 8.1 011 — first-party dogfood does not require Plane C

`01-product.md` “Later (not v1)”:

```79:83:plans/011-new-lazuar-pay/01-product.md
## Later (not v1)

- Tax **provider** (someone else’s MyInvois). You send amount + buyer; they return VALID + QR. Pay never owns UBL, consolidation, types 03–14, or XAdES.
- More rails (Razorpay, Xendit) as reminder-only, labelled as such.
- Entitlement grant for a **second** Lazuar app — HTTP or a function if in-process; not an in-process event catalog talking to yourself.
```

M2M for a second of *your* apps is **soon**, not v1:

```202:202:plans/011-new-lazuar-pay/11-checklist.md
| NP-SOON-007 | M2M checkout for a second of *your* apps (same `/v1`) | soon | Pay | — | todo | First extra consumer |
```

First slice 03 does not mention outbound `payment.completed`. Steps 8–12 are BYOK, pay link, buyer pays, inbound webhook idempotent, merchant sees receipt. That loop is live (minus mail). Plane C is the **second** consumer, which 011 parked under later/soon.

Bezos door paper (`08-bezos-door.md`) says `/v1` is the sold interface; it does not implement push. Push is how a stranger avoids polling `/v1`. Both are the door. Neither existed as M2M+HMAC on 011’s SHA; only GET `/v1/orgs/{orgId}/payments` exists now, and it is human-gated.

011/07 rule 2 still binds Plane C’s **refuse**:

```47:49:plans/011-new-lazuar-pay/07-separate-vs-one-binary.md
2. **No sync HTTP for the money path as the only grant.** Pay must not `POST One/grant-buyer-access` in the webhook as the only fulfillment — One down = webhook retries = mess. Buyer access is **Pay’s subscription/session row**. Staff access may lag on One webhooks.
3. **One writer per fact.** Pay owns “paid.” One owns “may use merchant ops.” Notify owns “accepted by SES” when it exists. Do not let both store entitlement.
```

Do not put buyer entitlement in One. Do not make Aura’s unlock the thing that makes Pay’s journal true.

### 8.2 012/09 — Plane C named, “not v1 for first-party dogfood”

```36:42:plans/012-one-to-pay/09-webhooks-events.md
| Plane | Direction | Job | When |
|-------|-----------|-----|------|
| **A. One → Pay** (this paper) | One POSTs signed JSON to Pay | Membership, tenant lifecycle, key revoke | After connection works; **mandatory before live charges** for `tenant.suspended` |
| **B. PSP → Pay** (`NP-GW-004` / `NP-GW-006`) | Stripe / CHIP / Billplz POST to Pay | Money. Verify, idempotent `(tenant, provider, event_id)`, journal + `RCPT-` in one handler | S1 money slice. Unrelated HMAC. |
| **C. Pay → merchant / second app** (Bezos door, later) | Pay POSTs to a stranger | `payment.completed` and friends | Not v1 for first-party dogfood. Old Hub outbound is museum. |
```

Standing law 4 in the same paper: “Do not put buyer entitlement in One.” Still the refuse list.

012/09 also said Pay should **receive** One’s dialect (Plane A). That receiver **is live** on this SHA (`OneWebhookEndpoints`, per-org ciphertext). Plane C remaining is the **emit** side of a **Pay** catalog.

### 8.3 013 — still “not v1 / later”

```36:42:plans/013-prods/07-fulfillment-ledger-docs.md
| **A. PSP → Pay** | … | **Yes.** |
| **B. One → Pay** | … | **No.** |
| **C. Pay → merchant / second app** | Pay POST to a stranger | Bezos door `payment.completed` | **Not v1.** Outbox later. Not how Pay talks to **itself**. |
```

(013-07 swapped A/B labels vs 012/09. The C row is the same sentence.)

```39:39:plans/013-prods/06-money-rails.md
| **C. Pay → merchant / second app** | Pay POSTs to a stranger | `payment.completed` and friends | **No.** Bezos door later. Old Hub outbound is museum. |
```

```862:862:plans/013-prods/06-money-rails.md
- Outbound merchant webhooks (Plane C).
```

```896:896:plans/013-prods/06-money-rails.md
25. **Outbound `payment.completed` to Aura** as a blocker for first-party dogfood. Plane C later. `NP-SOON-007`.
```

013-07’s “What exists on this HEAD” is **stale** (in-memory checkout, no webhook route). Live `6d730d15` has Postgres, fulfill, six rails. Plane C row did **not** go live with that. The paper’s C judgment survived the implementation of A/B.

### 8.4 018 — kernel idea, door missing

```23:24:plans/018-evals/001-evals.md
Pay today is a **hosted cashier** (merchant pastes keys, buyer pays on a link, Official Receipt). It is **not** yet a kernel other apps can swallow in an afternoon: there is no machine key (`lzr_sk_`) and no outbound `payment.completed` on the new host. The idea is ahead of the door.
```

```79:79:plans/018-evals/001-evals.md
Until a second app (not the merchant Vite) can `POST` a checkout with a machine token and get a signed `payment.completed`, you do not have a kernel. You have a dashboard that mints links.
```

Still true. 002 did not add `lzr_sk_` or the POST. 018’s first company step is still the company:

```172:172:plans/018-evals/001-evals.md
1. **Finish the kernel for one second app you own.** Machine auth, `POST` checkout, signed `payment.completed`, idempotency, one sample repo.
```

That is 020 as a program (papers 01, 02, 03, 09), not a hosted-cashier bug.

### 8.5 019 — re-read after 002, still empty; line numbers moved

```603:608:plans/019-evals/08-contracts-spec-honesty.md
### Outbound `payment.completed`

- Fulfillment on verified inbound webhook writes charge, journal, Official Receipt `RCPT-…`, audit action **`checkout.paid`** (`Fulfillment.cs:37-127`). No HTTP client, no outbox table, no HMAC to a merchant URL.
- IsolationTests **ban** `GatewayPaymentCompletedIntegrationEvent`
- TypeSpec has **inbound** `POST /v1/webhooks/{provider}/{orgId}` and `POST /v1/one/webhooks` only.
- **Missing:** merchant-configured egress `payment.completed` (and failed). Inbound PSP webhook is not that door.
```

Audit insert is now lines 156–162; inbound One PUT exists; still no egress.

```325:325:plans/019-evals/06-rails-webhooks-fulfillment.md
One class for all six names. No SST. No tax invoice. No outbound `payment.completed`. No mail.
```

```714:716:plans/019-evals/06-rails-webhooks-fulfillment.md
### G12 — No receipt email / outbound payment.completed

`MailOutbox` unused. Kernel story in 018-evals is still ahead of the door.
```

```881:893:plans/019-evals/10-honesty-bugs-gaps.md
No `lzr_sk_` / `payment.completed` under `apps/lazuar-pay` or `packages/pay-spec`. Fulfillment audit is in-process only:
… Hub still owns outbound `payment.completed` (`…/IntegrationCheckoutGatewayEventsHandler.cs`) — **not** this host.
```

Grep on this SHA confirms. Hub still has the handler. Isolation still forbids importing it.

**Verdict:** “Plane C is later / not v1 first-party dogfood” is **still true** as a **first-party cashier** statement. It is **no longer** an excuse to skip the door if the **program** is 020 (second-app integration). 011 v1 dogfood is done enough to take a card and print `RCPT-`. 018/020’s kernel is not. This paper’s job is the HMAC half of that kernel; paper 02 is the Bearer half.

---

## 9. How to solve (analysis, hatch not cathedral)

### 9.1 Smallest signed POST after fulfill

One event type: **`payment.completed`**.

One endpoint per org (singular PUT/POST), not ten.

One secret, `SecretBox`, shown once.

One delivery table, insert in `Fulfillment`’s `SaveChanges` when an active endpoint exists.

One tiny worker in `apps/lazuar-pay` (not Hub’s job, not One’s job).

Optional same slice: `webhook.test` so register can be proved without Stripe.

That is the hatch. Everything else in §5 is a later insert at a later writer.

### 9.2 Sequence (so the next slice has a place to start — not a ticket list for this file)

1. **Table `webhook_endpoints`.** `org_id`, `id`, `url`, `secret_ciphertext`, `secret_prefix`, `status`, `enabled_events` (empty = all of Pay’s closed list), timestamps. Unique: for the singular hatch, unique `org_id` among `status=active` or simply one row per org.
2. **Table `webhook_deliveries`.** `id`, `org_id`, `endpoint_id`, `event_id` unique with endpoint, `event_type`, `payload_json`, `status`, `attempt_count`, `next_attempt_at`, lease columns, http snippet fields. Not `mail_outbox`.
3. **Closed catalog class** `PayWebhookEventCatalog` with `payment.completed` and maybe `webhook.test`. IsolationTests do not need a new ban if we do not type Hub names.
4. **Writer door** `POST /v1/orgs/{orgId}/webhooks` (or singular PUT). SSRF validate. Mint `whsec_`. 201 secret once. GET configured+prefix+url. Rotate. Member 403.
5. **Serialize envelope** snake_case `{ id, type, created_at, org_id, api_version, data }` with data from §5.1. Sign **after** serialize; store the exact string.
6. **Fulfill insert** after document/audit add, before `SaveChanges`. If no endpoint, skip (Pay still paid). Unique conflict = already queued.
7. **Worker.** Named HttpClient `pay-webhooks`, 10s timeout, no redirect, User-Agent `Lazuar-Pay-Webhooks/1.0`. One dialect headers. Testing: worker off, `ProcessBatch` in tests.
8. **pay-spec** after host (019 honesty: host first, then tsp). Do not generate Hub `PaymentWebhookPayloadDto`.
9. **Tests.** See §9.5.
10. **Sample** is paper 09. Do not block the host on Next.js. A test `HttpListener` is enough.

Refuse to dual-write Hub. Refuse to `POST /members` or grant One entitlement. Refuse to emit from `{ ignored }`.

### 9.3 Pick a dialect: One dialect, not official Standard Webhooks, not Hub combined-only

**Official Standard Webhooks** (spec `standard-webhooks/standard-webhooks`):

- Headers: `webhook-id`, `webhook-timestamp`, `webhook-signature`
- Signed content: `msg_id.timestamp.payload` (three parts)
- Symmetric secret: `whsec_` + **base64** raw key; verifier **strips prefix and base64-decodes**
- Signature: `v1,<base64>` (comma, base64 digest, space-delimited multiple for rotation)

**Hub museum:**

- `X-Lazuar-Signature: t={unix},v1={hex}`
- Signed: `{unix}.{body}` (two parts, **no** msg id)
- Secret: full UTF-8 including `whsec_`, **not** decoded
- Extra: `X-Lazuar-Event`, `X-Lazuar-Delivery-Id`, `X-Lazuar-Webhook-Id`

**Product One (sibling, live):**

- `X-Lazuar-Signature: v1={hex}` + `X-Lazuar-Timestamp: {unix}`
- Signed: `{unix}.{body}` (two parts, **no** msg id)
- Secret: full UTF-8 including `whsec_`, **not** decoded
- Extra: `X-Lazuar-Event-Id`, `X-Lazuar-Event-Type`, `X-Lazuar-Tenant-Id`, `X-Lazuar-Delivery-Id`
- Envelope: `{ id, type, created_at, tenant_id, api_version, data }`

**Pick One dialect.** Reasons:

1. **Pay already verifies it** on Plane A. A compute helper can sit next to `OneWebhookSignature` (sign + verify). Do not add a third algorithm in one binary.
2. **First-party second apps already verify One.** Aura / next Lazuar app can reuse R5 / `examples/node-webhook-verify`. Official Standard Webhooks would be a **new** library (`standardwebhooks` npm) and a **wrong** secret decode (base64 after strip) that would break anyone who copies Pay’s inbound tests.
3. Hub’s combined `t=,v1=` is museum. One moved to split headers. Pay inbound accepts both as **compat** for One; **egress** should emit the live One split form, not the Hub combined form. Receivers that only parse `t=` can be told to read `X-Lazuar-Timestamp` — that is one extra header, not a new HMAC.
4. Official Standard Webhooks includes `msg_id` in the signed payload. One/Hub do not. Mixing would make Pay’s inbound tests and outbound tests disagree about “what is signed.”
5. Claiming “we implement Standard Webhooks” while signing two-part `{t}.{body}` with hex `v1=` is the lie Hub already told in a comment. Do not repeat it in pay-spec docs.

Document honestly: “Lazuar HMAC (One dialect). Not Stripe. Not Standard Webhooks. Secret is the full `whsec_` string.”

Optional compat: also set `X-Lazuar-Signature: t={unix},v1={hex}` **in addition** so Hub sample verify helpers work without edits. That is a nice-to-have, not the hatch. Prefer one header form. Split is the one Pay’s tests already construct in `OneWebhookTests`.

### 9.4 Tests (hatch)

Host tests, mirror `OneWebhookTests` / `WebhookTests` style (HTTP, Postgres, no NSubstitute theater):

1. **No endpoint:** fulfill paid → 0 delivery rows. Charge still exists.
2. **Endpoint + paid:** fulfill → 1 pending delivery; payload `type=payment.completed`, amount/currency/`RCPT-`/checkout_id match rows; unique second fulfill does not insert a second event id.
3. **Worker 2xx:** process batch → POST captured raw body + headers; signature verifies with `OneWebhookSignature.TryVerify` (the inbound helper); status succeeded.
4. **Worker 500:** next_attempt in the future; attempt_count++; money unchanged.
5. **Worker 401:** dead/permanent; do not retry forever.
6. **Tamper body / wrong secret / stale timestamp:** a test receiver using the verify helper rejects; Pay does not care (that is the app’s test). Pay unit: `Compute` then `TryVerify` round-trip, tamper fails.
7. **Replay:** same `event_id` POST twice; test app no-ops (document; Pay unique prevents two rows).
8. **SSRF:** register `http://127.0.0.1:9` allowed in Testing; register `http://169.254.169.254/` 400 even in Testing; Production-shaped test host rejects loopback.
9. **Member cannot POST** the door; writer can. Secret not in GET.
10. **Rotate:** old secret fails verify of a new delivery; new secret works.
11. **Isolation:** still no `GatewayPaymentCompletedIntegrationEvent`, no `Modules.One`, no `apps/lazuar-api` csproj. Optionally add bans: `OutboundWebhookDispatcherJob`, `WebhookDeliveryOutbox` (Hub type names).
12. **Do not** POST from the PSP request thread in a test spy that waits on the merchant URL (proves we did not fire-and-forget in-handler).

### 9.5 Refuse (binding)

- **Do not dual-write Hub dispatcher.** No enqueue into `apps/lazuar-api` `WebhookDeliveryOutbox`. No `IEventBus`. No project reference. No “temporarily publish Hub so Aura keeps working.” Aura must cut to 8081.
- **Do not put buyer entitlement in One.** `payment.completed` is information. One membership is staff. Cardholders stay off Zitadel. If Aura unlocks a feature, Aura does it on its own row after verify. Pay’s `subscriptions` stub is not One `member.accepted`.
- **Do not share Plane A/B secrets or routes.** New prefix `/v1/orgs/{orgId}/webhooks`, new table, new `whsec_` minted by Pay.
- **Do not reuse `mail_outbox`.**
- **Do not copy One’s encryption key.**
- **Do not emit a catalog we do not write.**
- **Do not fire HTTP in the fulfill transaction.**
- **Do not treat Standard Webhooks npm as a dependency of the host.** Sign four lines of HMAC. Verify with the helper we already have.

---

## 10. Ranked holes this slice

A **hole** here is missing Plane C, a live lie that will break a second app, or a refuse that must stay refuse. Not “Hub had seventeen types.”

### P0 — missing door (the slice)

1. **No outbound `payment.completed`.** Empty set §1. Second app must poll GET payments with a **human** JWT, or scrape `:5178`. 018/019/020 problem statement. **Missing feature**, not a bug in fulfill.

2. **No registration door / no Pay-minted `whsec_`.** Ops-paste or “Aura hardcodes a URL” would skip writer, shown-once, rotate, SSRF. **Missing feature.**

3. **No delivery outbox / worker.** Even a naive `HttpClient.PostAsync` in fulfill would be the wrong hatch (PSP latency, loss on crash). **Missing feature**, with a refuse on fire-and-forget.

### P0 — coupling that will fake the door

4. **No machine key (paper 02).** A signed POST to Aura without a way for Aura to mint the checkout is half a kernel. 018 listed both in one sentence. This paper does not implement M2M; it names the dependency. Register can be a human owner; mint+listen cannot.

5. **Confusion of three `whsec_`.** Staff already paste Stripe `whsec_` and One `whsec_` on two forms (`gateway`, `one-webhook`). A third secret named the same without UI copy will be pasted into the wrong box. **Product hole** at implement time: labels must say “Pay signs with this; you verify” vs “Stripe signs; Pay verifies.”

### P1 — live facts that make friends-of-completed dishonest if shipped now

6. **No `payment.failed` writer.** Rails ignore failure events. Emitting failed from `{ ignored }` would wake Aura on noise. **Refuse until 07/rails grow a failed status.**

7. **No refunds.** `refund.created` is fiction. **07.**

8. **`MailOutbox` unused** sits next to the future HMAC table. Risk: someone sets `kind=webhook`. **Hygiene refuse.**

9. **Occupancy expire / charges pause / full** are in-process only. Fine for v1 catalog. A second app holding seats will **race** TTL without `checkout.expired`. Accept for hatch; name it.

10. **pay-spec inbound-only Webhooks tag.** After host, tsp must grow or docs will generate a client with no register door. 019 already punished stale dist. **Spec lag**, ordered after host.

### P1 — dialect / copy risks

11. **Hub museum still compiles in this repo.** A helpful PR can `using Modules.One.Infrastructure.Workers`. IsolationTests ban `Modules.One` as a **string in Pay src**, which is the tripwire. Add Hub type names to the ban list when Plane C lands so `OutboundWebhookDispatcherJob` cannot sneak in as a copied file with a new namespace that still contains the token — actually `OutboundWebhookDispatcherJob` is not in the current ban list. **Hole:** IsolationTests do not mention outbound dispatcher type names. They ban `Modules.One` and `GatewayPaymentCompletedIntegrationEvent`. A copy-paste into `Lazuar.Pay.Webhooks.Outbound` would pass. Tests in §9.4.11 should extend the ban to those **tokens** if they appear, or to `IEventBus` / `OutboundWebhookRequested`.

12. **One encryption key reuse.** Someone will “just call `AesWebhookSecretProtector`.” Refuse. Different config, different product.

13. **Official Standard Webhooks branding.** Hub comment already lied. A tsp `@doc` that says “Standard Webhooks” while signing `{t}.{body}` hex will mis-implement npm `standardwebhooks`. **Honesty hole.**

14. **SSRF copy-paste from One without Dev loopback hatch.** Laptop sample dies. Opposite of Billplz’s “localhost callbacks 400” lesson, same family of “local topology.”

### P2 — first-party dogfood vs kernel

15. **`:5179` poll is not a substitute.** Buyers wait for Plane B. Other apps cannot sit there.

16. **Merchant payments page is not a substitute.** One-shot GET, human JWT.

17. **Hub sample `examples/hub-cashier-next` still documents Hub HMAC.** Paper 09 must not tell a second app to verify Hub. Cut or retarget. Out of this slice except as a landmine.

18. **Subscription row on `mo`/`yr` is a dead branch** (019 G11). Do not add `subscription.activated` because the row exists.

19. **Test rail** can mint paid without Stripe. Use it to fire `payment.completed` in CI without ngrok. That is an opportunity, not a hole — unless the worker is written to skip Test. Do not skip Test.

### Ranked short list (implementer order, still analysis)

1. Outbox row + `payment.completed` after fulfill (empty catalog consumer = no-op).
2. Singular register door + `whsec_` once + SecretBox.
3. Worker, One dialect, Testing loopback hatch.
4. Tests in §9.4.
5. pay-spec.
6. Sample (09) + machine key (02) — kernel complete only with both.

Until 1–3 exist, quote 018 without flinching: **hosted cashier, not a kernel.**

---

## 11. Coordinate recap

| | |
|--|--|
| Date | 2026-08-28 |
| Pay HEAD | `6d730d15` `fix(pay): store per-org One webhook secrets` |
| One HEAD (pattern) | `6b78e9d4` `fix(api): allow Pay merchant :5178 as a CORS origin` |
| Plane | **C only** |
| Live outbound dispatcher in focused Pay | **None** |
| Live substitute | GET `/v1/orgs/{orgId}/payments` and `/receipts` (member JWT); buyer poll `GET /v1/pay/{token}` |
| Dialect to emit | **One** (`v1=` + `X-Lazuar-Timestamp` over `{unix}.{raw body}`, full `whsec_` UTF-8) |
| First event | `payment.completed` |
| Insert | Same TX as fulfill |
| HTTP | Worker, at-least-once |
| Refuse | Hub dual-write; buyer entitlement in One; Standard Webhooks fake; `mail_outbox` overload; One AES key |

The file is the deliverable. Do not flip 011. Do not implement from this paper.
