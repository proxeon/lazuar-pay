# 04 — Inbound webhook planes (One → Pay, PSP → Pay)

**Program:** 020-evals  
**Slice:** Inbound webhook planes that must work in production — **not** Pay→app (that is `03-outbound-webhooks.md`, Plane C).  
**Date:** 28 August 2026  
**Type:** Uncondensed evaluation. **Not** an implementation. **Not** a flip of `plans/011-new-lazuar-pay/11-checklist.md` Status cells. **Not** a project reference into `apps/lazuar-api`.

**Repos and SHAs (this write-up):**

| Tree | Path | HEAD | Branch |
|------|------|------|--------|
| Pay (this tree) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` | `6d730d15` — `fix(pay): store per-org One webhook secrets` | `fix/002-pay-host-bugs` |
| One (sibling) | `/Users/akmalfirdaus/Code/lazuar/lazuar-one` | `6b78e9d4` — `fix(api): allow Pay merchant :5178 as a CORS origin` | `main` |

Live files on this SHA are authority. `plans/012-one-to-pay/09-webhooks-events.md`, `plans/013-prods/08-one-identity-production.md`, `plans/014-evals/08-webhooks-secrets-fulfillment.md`, `plans/019-evals/06-rails-webhooks-fulfillment.md`, `plans/019-evals/07-identity-authz-cors.md`, and `issues/002/*` are historical / extraction papers. If they disagree with live files, live files win; this report names the disagreement.

`issues/002/README.md` says 001–080 are **resolved** on `fix/002-pay-host-bugs`. This paper re-reads the **code those issues touched**, not the YAML `status:` lines. Several issue **bodies** still say `Status: open` (011, 006, 007, 015, 024, 027, 073). That is leftover prose in the issue files. The host on `6d730d15` is the thing under evaluation.

---

## 0. How to read this paper

Three different “webhook” stories live in this family. Mixing them is how the old Hub tree got an in-process catalog talking to itself **and** a public HMAC door **and** Stripe/CHIP/Billplz callbacks all called “webhooks”:

| Plane | Direction | Job | Route on this SHA | This paper |
|-------|-----------|-----|-------------------|------------|
| **A. One → Pay** | One POSTs signed JSON to a Pay URL | Tenant lifecycle. **Money gate:** `tenant.suspended` / `tenant.reactivated` → `OrgSettings.ChargesPaused` | `POST /v1/one/webhooks` | **Yes** |
| **B. PSP → Pay** | Stripe / CHIP / Billplz / Xendit / Razorpay / Test POST to Pay | Money. Verify, unique `(org, provider, event_id)`, journal + `RCPT-` in one handler | `POST /v1/webhooks/{provider}/{orgId}` | **Yes** |
| **C. Pay → merchant / second app** | Pay POSTs to a stranger | `payment.completed` and friends | **Does not exist on this SHA** | **No. That is 03.** |

Plane A is **lazuar-one’s** product: closed catalog, `whsec_…`, `X-Lazuar-Signature: v1=<hex>` plus `X-Lazuar-Timestamp`. Plane B is **Pay’s** product: provider SDK or provider HMAC, different headers, different idempotency tuple. Do not share a table, a secret, or a route prefix.

**What this paper will not do.**

- It will not design Plane C. Other apps do **not** call Plane B. They need Plane C. Confusion in docs is a gap named in §7.
- It will not tell Pay to `POST` One `/tenants/{id}/webhooks`. Live code does not. README says it does not. That is an **ops** hole, not a missing C# client method to invent this week without a product decision.
- It will not weaken One’s SSRF. Loopback is blocked on purpose.
- It will not import Hub `OutboundWebhookSignature`. Museum `t=,v1=` in one header is **compat** on Pay’s verifier, not the product One wire.
- It will not project-reference `lazuar-one`. Algorithm is copied, Pay-owned.

**002 on this SHA (the inbound subset).** The hosted-cashier bugs that were inbound-webhook-shaped: 006 (Test unsigned), 007 (Test omit amount), 008 (Test mint EventId), 009 (Stripe `payment_status`), 010 (concurrent fulfill), 011 (One HMAC dialect), 012 (CHIP join), 015 (mismatch consume policy), 024 (Stripe process `whsec_` docs), 025 (`ChargesPausedException` type), 027 (CHIP PEM on PUT), 029 (per-org One `whsec_`), 033 (pause occupies seat), 040 (merchant webhook URL hint), 063 (Plane A garbage JSON 500), 073 (spec `{ok}` vs `{duplicate}`/`{ignored}`). Live code after those fixes is what the tables below quote.

**What “production” means here.** First-party dogfood of One + Pay merchant + Pay checkout, **multiple tenants**, public HTTPS, PSP dashboards that can actually POST, One dispatcher that can actually POST. Not “hermetic green.” Hermetic green is evidence. It is not the lived sentence.

---

## 1. Standing law (do not weaken)

1. **Two inbound planes, two routes, two tables, two secret names.** `/v1/one/webhooks` + `one_webhook_events` + per-org `OrgSettings.OneWebhookCiphertext` (process `Pay:OneWebhookSecret` one-shop fallback). `/v1/webhooks/{provider}/{orgId}` + `psp_webhook_events` + per-org `GatewayCredentialRow.WebhookCiphertext` (Stripe process fallback **Testing-only**). Never `psp_webhook_events.provider = "one"`.
2. **Prefer HMAC push from One.** Pay receives. Pay does not poll `GET /tenants/{id}/events` on this SHA (that pull is **not implemented**; named as missing, not a bug in the receiver).
3. **Do not tail Zitadel.** One’s outbox is the catalog.
4. **`tenant.suspended` is mandatory before live charges.** Staff belt is One membership 403. Buyer belt is `ChargesPaused`. Buyers never send Bearer.
5. **Pay holds the receiver HMAC secret (`whsec_…`, shown once).** One holds the AES-wrapped copy. Pay never holds One’s `Webhooks:SigningSecretEncryptionKey`, Zitadel PAT, or OpenFGA admin.
6. **Pay does not POST One `/tenants/{id}/webhooks`.** Ops registers the URL. Document vs implement is §2.6.
7. **One Pay binary, one Pay database.** The webhook HTTP handler **is** the fulfillment entry for Plane B. No MediatR, no Payments outbox, no `GatewayPaymentCompletedIntegrationEvent`.
8. **Buyers are not One humans.** A late One webhook must not unbook money. Money in Pay is still true if membership chrome lags.
9. **Empty / garbage signed bodies are 400, not 200 and not 500.**
10. **Do not implement from this file.**

---

## 2. Plane A — One → Pay `POST /v1/one/webhooks`

### 2.1 Live map (files actually opened)

| Path | Role on `6d730d15` |
|------|-------------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookEndpoints.cs` | Maps `POST /v1/one/webhooks`, `PUT/GET /v1/orgs/{orgId}/one-webhook`. Verify, pause, reactivate. |
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookSignature.cs` | Product One dialect + combined-header compat. 300s skew. |
| `apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs` | `OrgSettingsRow.OneWebhookCiphertext`, `ChargesPaused`. `OneWebhookEventRow` (no `OrgId` column). |
| `apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs` | `one_webhook_events` unique on `DeliveryId` only. |
| `apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260828093000_OrgOneWebhookCiphertext.cs` | Adds `org_settings.OneWebhookCiphertext`. HEAD commit of this SHA. |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OneWebhookTests.cs` | Dialect, pause, replay, per-org secrets, empty/garbage, writer PUT. |
| `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` | `app.MapOneWebhooks()` next to `app.MapWebhooks()`. |
| `apps/lazuar-pay/.env.example` | Process `Pay__OneWebhookSecret` commented as one-shop fallback. |
| `apps/lazuar-pay/README.md` | “Pay does not POST One `/tenants/{id}/webhooks`.” |
| `apps/lazuar-pay/docker-compose.pay.yml` | Passes `Pay__OneWebhookSecret`. Defaults `Pay__PublicBaseUrl` to `http://localhost:8081`. |
| Sibling `lazuar-one/.../WebhookSigning.cs` | `FormatHeader` = `"v1=" + hex`. |
| Sibling `lazuar-one/.../WebhookDispatcher.cs` | Split headers: `X-Lazuar-Signature` + `X-Lazuar-Timestamp`. |
| Sibling `lazuar-one/.../WebhookUrlValidator.cs` | SSRF: loopback / RFC1918 / link-local blocked unless `UrlHostAllowlist`. |
| Sibling `lazuar-one/.../WebhookEventCatalog.cs` | Seventeen v1 types. |

`Program.cs` maps both planes in the same host. They are adjacent in the file. They are not the same handler:

```74:84:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
app.MapHealth();
app.MapWhoami();
app.MapOrgReady();
app.MapCheckouts();
app.MapPaymentLinks();
app.MapCatalog();
app.MapPublicPay();
app.MapGateways();
app.MapWebhooks();
app.MapPaymentQueries();
app.MapOneWebhooks();
```

### 2.2 The HTTP door

```12:17:apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookEndpoints.cs
    public static void MapOneWebhooks(this WebApplication app)
    {
        app.MapPost("/v1/one/webhooks", Handle);
        app.MapPut("/v1/orgs/{orgId}/one-webhook", Put);
        app.MapGet("/v1/orgs/{orgId}/one-webhook", Get);
    }
```

Three doors, three jobs:

| Method | Path | Auth | Job |
|--------|------|------|-----|
| `POST` | `/v1/one/webhooks` | HMAC. **Never Bearer.** | Receive One catalog events. Pause / unpause. |
| `PUT` | `/v1/orgs/{orgId}/one-webhook` | Bearer + **writer** (`owner`/`admin` via `/me` role overlay) | Store this shop’s `whsec_` in `SecretBox`. |
| `GET` | `/v1/orgs/{orgId}/one-webhook` | Bearer + member | `{ org_id, webhook_configured }` — **never** the secret. |

TypeSpec on this SHA names the same three:

```407:429:packages/pay-spec/main.tsp
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
```

Issue 073 asked the spec to stop requiring `{ ok }` on every 200. Live TypeSpec is a union `OneWebhookResult = WebhookOk | WebhookDuplicate`. Plane B is `PspWebhookResult = ok | duplicate | ignored`. Host duplicate is `{ duplicate: true }` with **no** `ok`. That is honest on this SHA. Generated clients that assume `{ok:true}` on every 200 are still wrong if they were generated from an older dist; `03` / `09` own that. This paper only needs: inbound PSP adapters check **HTTP 2xx**, not JSON `ok`. One’s dispatcher does the same (`httpStatus is >= 200 and < 300`).

### 2.3 Handle: secret first, HMAC second, JSON third

```19:59:apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookEndpoints.cs
    static async Task<IResult> Handle(
        HttpRequest request,
        PayDbContext db,
        IConfiguration config,
        SecretBox box,
        CancellationToken ct)
    {
        using var reader = new StreamReader(request.Body);
        var json = await reader.ReadToEndAsync(ct);
        var secret = await ResolveSecretAsync(json, db, config, box, ct);
        if (string.IsNullOrWhiteSpace(secret))
        {
            return PayErrors.Status(503, "Service Unavailable", "One webhook secret missing");
        }

        var provided = request.Headers["X-Lazuar-Signature"].ToString().Trim();
        var timestamp = request.Headers["X-Lazuar-Timestamp"].ToString().Trim();
        if (!OneWebhookSignature.TryVerify(secret, json, provided, timestamp))
        {
            return PayErrors.Status(401, "Unauthorized", "Invalid HMAC");
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return PayErrors.Status(400, "Bad Request", "invalid event");
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return PayErrors.Status(400, "Bad Request", "invalid event");
        }
```

Order is the money-safety order:

1. **No secret → 503.** One will retry. That is correct: Pay is misconfigured, not “this event is bad.”
2. **Bad / missing HMAC → 401.** One will retry. Forged traffic retries too; that is the cost of 401-not-200. Do not 200 a bad HMAC (would ack a forgery). Do not 400 a bad HMAC (would look like a payload problem).
3. **Empty body after a valid HMAC → 400.** Issue 063. One retries a truncated body; ops noise, not a pause.
4. **Garbage JSON after a valid HMAC → 400 `invalid event`.** Not the unhandled 500 063 extracted.

There is **no** `ILogger` in this class. Invalid HMAC is not logged. The signature header is not logged. That is the right default against leaking HMAC material into request logs (Kestrel may still log the header if operators raise request logging). Named in §6.

### 2.4 Dialect vs product One (issue 011, live)

**Product One** (sibling `WebhookSigning` + `WebhookDispatcher` on `6b78e9d4`):

```10:26:/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Webhooks/WebhookSigning.cs
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

Dispatcher sets **split** headers. Signature is `v1=<hex>` with **no** `t=` inside that header. Timestamp is a second header:

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

One also sends `User-Agent: Lazuar-One-Webhooks/1.0` and `Content-Type: application/json; charset=utf-8`. Pay does not inspect those. Fine.

**Pay verifier** on this SHA documents the product dialect in the file comment (011’s “Judgment stolen from One’s signer” lie is **gone**):

```6:46:apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookSignature.cs
/// <summary>
/// Product One signs <c>X-Lazuar-Signature: v1=&lt;hex&gt;</c> and
/// <c>X-Lazuar-Timestamp</c> over <c>{unix}.{body}</c>. Combined
/// <c>t=&lt;unix&gt;,v1=&lt;hex&gt;</c> in one header is accepted as compat.
/// Raw body hex is rejected.
/// </summary>
internal static class OneWebhookSignature
{
    public static bool TryVerify(
        string secret,
        string body,
        string? signatureHeader,
        string? timestampHeader = null,
        long toleranceSeconds = 300,
        long? nowUnixSeconds = null)
    {
        // ...
        var signedPayload = $"{timestamp}.{body}";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signedPayload));
        var expectedHex = Convert.ToHexString(hash).ToLowerInvariant();
        return FixedTimeEqualsHex(v1Hex, expectedHex);
    }
```

`TryParse` accepts:

- Combined `t=<unix>,v1=<hex>` in `X-Lazuar-Signature` (Hub / Standard Webhooks packaging; **compat**).
- Split `v1=<hex>` in `X-Lazuar-Signature` **plus** integer `X-Lazuar-Timestamp` (product One).

It rejects raw uppercase hex of the body (old Hub `OutboundWebhookSignature` museum). Test `Body_only_uppercase_hex_is_401` locks that reject.

Skew default is **300 seconds**, matching One `Webhooks:ReplaySkewSeconds: 300`. Test `Stale_timestamp_is_401` uses `now - 1000`.

HMAC is UTF-8 of the **full secret string** (including `whsec_` prefix) over `{unix}.{body}` as a **string**. One HMAC’s the unix prefix bytes concatenated with **raw body bytes**. For JSON without a BOM, UTF-8 string == raw bytes. Do not re-serialize the envelope before verify. Pay does not: it hashes the raw `StreamReader` string.

**Hermetic proof of the live One wire** exists on this SHA. It did not in 019:

```204:222:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OneWebhookTests.cs
    public async Task Product_one_split_headers_suspend_charges()
    {
        await using var factory = new PayApiFactory { OneWebhookSecret = Secret };
        var client = factory.CreateClient();
        var body = """{"id":"del_one","type":"tenant.suspended","tenant_id":"t1"}""";
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes($"{t}.{body}"));
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/one/webhooks")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("X-Lazuar-Signature", "v1=" + Convert.ToHexString(mac).ToLowerInvariant());
        req.Headers.TryAddWithoutValidation("X-Lazuar-Timestamp", t.ToString());
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<PayDbContext>().OrgSettings.Single(x => x.OrgId == "t1").ChargesPaused, Is.True);
    }
```

That is the 011 reproduction, inverted: product One packaging **does** pause.

**Honesty leftover on the test helper.** `OneWebhookTests.Sign` / `SignWith` still mint **combined** `t={unix},v1={hex}` and do **not** set `X-Lazuar-Timestamp`. Most tests (suspend, reactivate, replay, empty, garbage, two-orgs) therefore prove the **compat** path. Only `Product_one_split_headers_suspend_charges` proves the dispatcher path. That is enough to close 011 as a **bug**. It is not enough to call dialect “lived against a running One dispatcher.” A laptop replay of a real One `tenant.suspended` still needs a non-loopback URL and a stored `whsec_` (§2.6). Treat 011 as **code-correct, dogfood-unproven**.

Pay does **not** read `X-Lazuar-Tenant-Id`. Org comes from body `org_id` then `tenant_id` (`ReadOrgId`). One’s envelope has `tenant_id`, never `org_id`. Tests send both shapes. `Valid_tenant_id_field_sets_charges_paused` is the One-shaped body. `org_id` is a Pay-invented alias that still works. Fine.

Pay does **not** read `X-Lazuar-Delivery-Id`. Idempotency key is body `id` (One outbox GUID = event id) **else** `X-Lazuar-Event-Id`. That is the right grain: retries of the same event keep the same event id and a **new** delivery id. Using delivery id would double-apply pause/reactivate on retry after a 503. Test `Missing_body_id_uses_event_id_header` locks the header fallback. Test `Signed_json_without_event_id_is_400` refuses Guid-minting (the 008 class of hole, on Plane A).

### 2.5 Apply: only `tenant.suspended` / `tenant.reactivated` change money

```181:226:apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookEndpoints.cs
    static async Task<IResult> ApplyAsync(JsonDocument doc, HttpRequest request, PayDbContext db, CancellationToken ct)
    {
        var type = doc.RootElement.TryGetProperty("type", out var t) ? t.GetString() : null;
        var bodyId = doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        var headerId = request.Headers["X-Lazuar-Event-Id"].ToString().Trim();
        var delivery = !string.IsNullOrWhiteSpace(bodyId) ? bodyId.Trim() : headerId;
        if (string.IsNullOrWhiteSpace(delivery))
        {
            return PayErrors.Status(400, "Bad Request", "event id required");
        }

        var orgId = ReadOrgId(doc.RootElement);
        if (await db.OneWebhookEvents.AnyAsync(x => x.DeliveryId == delivery, ct))
        {
            return Results.Ok(new { duplicate = true });
        }

        db.OneWebhookEvents.Add(new OneWebhookEventRow
        {
            Id = Guid.NewGuid().ToString("N"),
            DeliveryId = delivery,
            EventType = type ?? "unknown",
            ReceivedAt = DateTimeOffset.UtcNow
        });

        if (type == "tenant.suspended" && !string.IsNullOrWhiteSpace(orgId))
        {
            var settings = await db.OrgSettings.FindAsync([orgId], ct);
            if (settings is null)
            {
                db.OrgSettings.Add(new OrgSettingsRow { OrgId = orgId, ChargesPaused = true });
            }
            else
            {
                settings.ChargesPaused = true;
            }
        }

        if (type == "tenant.reactivated" && !string.IsNullOrWhiteSpace(orgId))
        {
            var settings = await db.OrgSettings.FindAsync([orgId], ct);
            if (settings is not null) settings.ChargesPaused = false;
        }

        await db.SaveChangesAsync(ct);
        return Results.Json(new { ok = true }, OneClient.Json);
    }
```

Facts:

- **Every** HMAC-valid catalog type is inserted into `one_webhook_events` (including `member.*`, `api_key.revoked`, `webhook.test`, unknown strings). Insert is not “applied.”
- **Only** `tenant.suspended` sets `ChargesPaused = true`. If the org has never been seen, it **inserts** `OrgSettingsRow` with pause true (and `SstRegistered` left at default null — 016’s “suspend seeds SST” hole is **not** on this SHA; `SstRegistered` is commented unused on the row).
- **Only** `tenant.reactivated` clears pause, and **only if** `OrgSettings` already exists. A reactivate for a never-seen org is a stored event and a no-op on money. That is correct: default `ChargesPaused` is false.
- Duplicate is `{ duplicate: true }` HTTP 200 **before** re-applying pause. Replay of suspend does not flap. Test `Replay_delivery_is_duplicate`.
- Unique is **`DeliveryId` globally**, not `(org, delivery)`. `OneWebhookEventRow` has **no `OrgId` column**:

```181:187:apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs
public sealed class OneWebhookEventRow
{
    public required string Id { get; set; }
    public required string DeliveryId { get; set; }
    public required string EventType { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}
```

`PayDbContext` unique index:

```136:141:apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs
        model.Entity<OneWebhookEventRow>(e =>
        {
            e.ToTable("one_webhook_events");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.DeliveryId).IsUnique();
        });
```

One event ids are outbox GUIDs, globally unique across tenants. Collision of two tenants’ event ids is not a live risk. The missing `OrgId` is an **audit / tenancy** hole: you cannot list “this shop’s One events” without parsing stored `EventType` only. Not a pause bug.

`AnyAsync` then insert is **not** a transaction with `SELECT FOR UPDATE`. Two concurrent first-deliveries of the same id can both miss `AnyAsync` and one will trip the unique index as `DbUpdateException` → unhandled 500. Plane B catches `DbUpdateException` → `{ duplicate: true }`. Plane A does **not**. Named as remaining bug (low: One dispatcher is not concurrent for one delivery; retries are serial).

No explicit `BeginTransaction` on Plane A. Pause flag + event row are one `SaveChanges`. If SaveChanges throws after adding both, neither commits. Fine.

### 2.6 `ChargesPaused` is the buyer belt. It is wired.

Staff mint / start / fulfill all read the flag:

| Door | File | HTTP when paused |
|------|------|------------------|
| `POST /v1/checkouts` | `CheckoutEndpoints.cs` | 403 `"Org charges are paused"` |
| `POST /v1/orgs/{id}/payment-links` | `PaymentLinkEndpoints.cs` | 403 same |
| `POST /v1/pay/{token}/start` | `PublicPayEndpoints.cs` | 403 same (buyers; no Bearer) |
| Plane B after verify | `WebhookEndpoints.cs` | **409** `"Org charges are paused"` — event **not** consumed |
| `FulfillPaidAsync` | `Fulfillment.cs` | throws `ChargesPausedException` (dedicated type, **not** `InvalidOperationException` — 025) |
| `GET /v1/orgs/{id}/ready` | `OrgReadyEndpoints.cs` | `ready: false` if paused |

Issue 033 (pause after mint occupies the seat): **fixed on GET of the parent link.** `GetLink` expires `open` children when `ChargesPaused`:

```68:74:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
            var settings = await db.OrgSettings.FindAsync([link.OrgId], ct);
            if (settings?.ChargesPaused == true)
            {
                await PaymentLinkOccupancy.ExpireOpenAsync(db, link.Id, ct);
            }
```

Test `Pause_expires_open_reservations` asserts `remaining == 1`, `taken_count == 0` after pause. One-off (non-link) `open` checkouts stay `open` and start 403s. That is acceptable: there is no occupancy seat. PSP webhook of an in-flight capture while paused is **409 and not consumed** — PSP retries; if the shop is reactivated inside the PSP retry window, the retry **pays**. That is the 012/09 product choice: in-flight capture may commit after unpause. O16.3 allowed it. This paper does not reopen it as a bug.

`ChargesPausedException` no longer inherits `InvalidOperationException` (025):

```178:178:apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs
public sealed class ChargesPausedException() : Exception("Org charges are paused");
```

Handle catch order cannot shadow it into 500 `fulfill failed`. Live path is 409 + rollback.

Webhook pause test: `WebhookTests.Paused_org_does_not_mint_receipt` — 409, no document, no `psp_webhook_events` row, checkout still `open`; unpause + retry pays. Money-safe. Ops: Stripe retries for days while the shop is paused. Named, not a bug.

### 2.7 Missing event types — needed or refuse for Pay money?

One’s closed catalog (`WebhookEventCatalog.All`) is seventeen types. Pay **stores** all of them after HMAC. Pay **applies** two.

| Type | One produces | Pay applies | Needed for Pay **money**? |
|------|--------------|-------------|---------------------------|
| `tenant.suspended` | `TenantService.SuspendAsync` | `ChargesPaused = true` | **Yes. Mandatory before live charges.** |
| `tenant.reactivated` | `TenantService.ReactivateAsync` | `ChargesPaused = false` if row exists | **Yes. Pair with suspend.** |
| `tenant.deleted` | owner wipe | stored only | **Money-adjacent.** Shop can still take buyer money until someone pauses or deletes Pay rows. Staff `/me` will 403. Buyers will not. **Missing, not refuse.** Ranked below. |
| `tenant.created` | provision | stored only | Refuse for dogfood: Pay does not provision from this event. Ada `POST /tenants` (One) then uses the id. |
| `member.invited` / `accepted` / `removed` / `left` / `role_changed` | membership | stored only | **Refuse for money.** Pay’s staff SoT is `GET /me` + `authz/check` on the request. 012/09: domain auto-join and SSO JIT **do not** emit `member.accepted`. A cache filled only from `member.*` is a lie. Do not build a membership directory from Plane A. |
| `ownership.transferred` | transfer | stored only | Refuse for first charges. “Billing owner” is not a Pay column. |
| `api_key.created` | mint | stored only | Refuse. Pay must not cache other people’s `lzr_sk_`. |
| `api_key.revoked` | revoke | stored only | **Refuse for money on this SHA.** Pay does not hold a cache of One machine keys (kernel M2M is 02; out of 002). Env-held Pay server key is an ops rotate, not this event. If 02 later caches `lzr_sk_` inside Pay, this event becomes **required**. Today it is not. |
| `oidc_app.created` / `revoked` | apps | stored only | Refuse. SPA `client_id` is env. Revoke is detected by OIDC failing, not this webhook. |
| `invite.revoked` / `resent` | invites | stored only | Refuse. |
| `webhook.test` | `POST …/webhooks/{id}/test` | stored only (`type == webhook.test` is not suspend) | **Ops-useful.** HMAC proof. Does not pause. Keep storing. A merchant “test ping” that 200s `{ok:true}` with a row is enough. Do not invent a UI that requires `livemode: false` handling. |

**Verdict on `member.*` and `api_key.revoked`:** do **not** implement apply-handlers for Pay money. Staff chrome that lags One by one `/me` is the cost of the split (`02-one-integration.md` rule). Implementing `member.removed` → drop a Pay session cache that does not exist is cathedral. Implementing `api_key.revoked` → drop a Pay key cache that does not exist is the same.

**Verdict on `tenant.deleted`:** this is the one missing apply that is **money-real**. A deleted One tenant whose Pay `ChargesPaused` stays false will still take public `POST /v1/pay/{token}/start` and Plane B fulfill. Staff cannot mint (membership 403). Buyers can. Rank: **missing P1**, not refuse. Shape of a later fix (not this paper): treat `tenant.deleted` like suspend (pause + maybe expire open children), or tombstone. Do not `DELETE FROM checkouts`.

**Pull fallback.** `GET /tenants/{id}/events` is **not** called anywhere under `apps/lazuar-pay/src`. After `tenant.suspended`, One’s own pull is 403 `"Tenant is suspended."` (012/09). Pull cannot catch the suspend you missed. Fallback for “dispatcher never reached us” is `GET /tenants/{id}` / `GET /me` status, **not** events. Pay does not poll those on a timer either. **Missing as a catch-up path; do not pretend pull replaces push for suspend.**

### 2.8 Per-org secret (issue 029) vs process fallback

`OrgSettingsRow`:

```3:14:apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs
public sealed class OrgSettingsRow
{
    public required string OrgId { get; set; }
    public string Currency { get; set; } = "MYR";
    public bool ChargesPaused { get; set; }
    /// <summary>Unused. Tax is out of this program. Column kept; do not read on the pay path.</summary>
    public bool? SstRegistered { get; set; }
    /// <summary>Unused. Vault save does not pick a default rail. Column kept; do not read on the pay path.</summary>
    public string? ActiveProvider { get; set; }
    /// <summary>Per-org One <c>whsec_</c>. Process <c>Pay:OneWebhookSecret</c> is the one-shop fallback.</summary>
    public string? OneWebhookCiphertext { get; set; }
}
```

Resolve order:

```134:161:apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookEndpoints.cs
    static async Task<string?> ResolveSecretAsync(...)
    {
        var orgId = PeekOrgId(json);
        if (!string.IsNullOrWhiteSpace(orgId))
        {
            var settings = await db.OrgSettings.FindAsync([orgId], ct);
            if (!string.IsNullOrWhiteSpace(settings?.OneWebhookCiphertext))
            {
                try
                {
                    var stored = box.Unprotect(settings.OneWebhookCiphertext);
                    return string.IsNullOrWhiteSpace(stored) ? null : stored;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        var process = config["Pay:OneWebhookSecret"];
        return string.IsNullOrWhiteSpace(process) ? null : process;
    }
```

**PeekOrgId parses unsigned JSON before HMAC.** That is the chicken of per-org secrets on a single URL: you must know which `whsec_` to use before you can verify. One’s product is **per endpoint**, and Pay’s URL is **one** `POST /v1/one/webhooks` for every tenant. The body `tenant_id` is the selector. HMAC still has to match **that** org’s secret (or the process hatch). Tests:

- `Two_orgs_only_matching_secret_pauses` — t1/`whsec_a` pauses t1; `whsec_b` against t1 is 401; steal rows not inserted.
- `Stored_secret_wins_over_process_fallback` — process `one_whsec_test` is 401 once PUT `whsec_stored` exists.

**Remaining holes in this hatch (not 029’s original bug):**

1. **Process fallback is global.** Any org **without** ciphertext shares `Pay:OneWebhookSecret`. Two shops that never PUT can pause each other with that one secret. Documented as “one-shop.” Multi-shop production **must** PUT per org, or leave process unset (then those shops 503).
2. **Decrypt failure returns `null` and does not fall through to process.** WrapKey rotate / corrupt ciphertext → 503 even if process is set. Fail-closed. One retries until ops PUT again. Correct, loud.
3. **Garbage JSON** cannot PeekOrgId → process fallback is used to verify, then 400. If process is unset, garbage is **503** not 401. That is a small oracle: “this host has no process secret and this body didn’t name an org with ciphertext.” Not a forge.
4. **No dual-verify on rotate.** PUT overwrites ciphertext immediately. One `POST …/rotate-secret` also invalidates the previous `whsec_` immediately (012/09). Ops must PUT the new secret **before** or **immediately after** One rotate, or deliveries 401 until they do. Missing dual-window is **refuse to copy Stripe’s two-secret window** unless product asks. Named as ops runbook, not a code bug.
5. **Merchant SPA has no One-webhook screen.** Grep of `apps/lazuar-pay-merchant` for `one-webhook` is **empty**. `GatewayPage.tsx` mentions `whsec_…` as the **Stripe** endpoint signing secret paste. Writer PUT of One’s secret is curl / HTTP only. Missing UX, not a host bug.

PUT itself:

- Writer only (`RequireWriterAsync` = `/me` role `owner`/`admin`, issue 030 overlay — not `authz/check admin`). Test `Member_cannot_put_one_webhook_secret` is 403.
- `webhook_secret` required. Empty `{}` is 400.
- `SecretBox.Protect`; WrapKey missing outside Testing is 503 with the WrapKey message.
- Audit `one.webhook_secret.upsert` with **no secret in the row**.
- GET returns `{ org_id, webhook_configured: true }` and **must not** echo the secret. Test `Put_and_get_does_not_echo_secret` asserts the ciphertext is not plaintext `whsec_shop_a`.

`OneClient` has `me` and `authz/check` only. No `CreateWebhook` method. Grep of `apps/lazuar-pay/src` for `tenants/.*/webhooks` is **empty**. README:

> One pause/reactivate HMAC is per-org `PUT /v1/orgs/{orgId}/one-webhook`; process `Pay__OneWebhookSecret` is the one-shop fallback. Pay does not POST One `/tenants/{id}/webhooks`.

That sentence is true on this SHA.

### 2.9 Ops hole — who registers Pay’s public URL with One?

**Nobody in Pay’s process.** This is the production hole 029 already named and **did not implement** (correct: hatch, not cathedral).

**What One requires to deliver.**

From sibling TypeSpec / `WebhookEndpoints` (012/09, still live):

- `POST /api/v1/tenants/{tenantId}/webhooks` with JWT **admin|owner** or API key `webhooks:write`.
- Body: `url`, optional `events` (omit / `[]` = all catalog types). Unknown types 400.
- Returns **201** including **`secret` once** (`whsec_…`).
- Suspended tenant cannot **create** (403). List still works with `AllowSuspended`.

**What Pay would register (if someone did it):**

```
https://<pay-public-host>/v1/one/webhooks
```

Events at least: `tenant.suspended`, `tenant.reactivated`, `webhook.test`. Adding `tenant.deleted` is the missing apply in §2.7 — do not subscribe until Pay applies it, or subscribe and accept “stored only.”

**Laptop loopback.**

One `WebhookUrlValidator.IsBlockedAddress` returns true for `IPAddress.IsLoopback` and `127.0.0.0/8`. Create of `http://localhost:8081/v1/one/webhooks` is **400** at register (CRUD test `I_CRUD_08_ssrf_url_create_400`) unless the host is on `Webhooks:UrlHostAllowlist`. Default `appsettings.json`:

```134:136:/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/appsettings.json
    "AllowedPorts": [ 443 ],
    "UrlHostAllowlist": [],
    "AutoDisableEnabled": true,
```

`AllowHttpInDevelopment: true` still does **not** allow loopback. HTTP to a public hostname in Development can work. `http://localhost` cannot.

Delivery-time re-validation is the same function (`ValidateForDeliveryAsync` = `ValidateCoreAsync`). DNS rebinding to 127.0.0.1 after register is dead.

**Production HTTPS.**

`EnvironmentRules.IsStrict` (Staging/Production): scheme https, port in `AllowedPorts` (default **443** only). A Pay host on `:8081` publicly is **not** a valid One destination in strict env unless operators add `8081` to One’s `Webhooks:AllowedPorts` (do **not** from Pay) **or** terminate TLS on 443 and reverse-proxy to 8081.

**Document vs implement registration.**

| Option | What it is | This paper’s rank |
|--------------------|-------------------|
| **A. Ops runbook (document)** | Owner opens lazuar-app Settings → Webhooks (or curl `POST /tenants/{id}/webhooks`). Pastes `https://pay.example/v1/one/webhooks`. Copies shown-once `whsec_`. Writer `PUT /v1/orgs/{orgId}/one-webhook { "webhook_secret": "whsec_…" }` on Pay. | **Do this.** Matches README. No PAT. No Pay→One write. |
| **B. Merchant UI that only stores the secret** | Settings field “One webhook secret” calling the existing PUT. Still no URL registration. | Missing UX. Cheap. Does not close the SSRF/URL half. |
| **C. Pay POSTs One `/tenants/{id}/webhooks`** | Needs a Pay machine key with `webhooks:write`, a public HTTPS URL Pay knows (`Pay:PublicBaseUrl`), and a place to store the shown-once secret (already exists). | **Missing kernel.** Out of 002. Do not do it with a human JWT from the SPA (redirect-uri mess). Do not do it with a Zitadel PAT. |
| **D. Weaken One SSRF for laptop** | Put `localhost` on One `UrlHostAllowlist` in One’s dev compose. | One’s hatch, **not Pay’s**. Pay must not ask One to ship allowlist-localhost in production appsettings. |
| **E. Cloudflare tunnel / ngrok** | Public HTTPS to laptop 8081. Register that origin with One. PUT the `whsec_`. | The actual dogfood path. Same as Billplz `Pay:PublicBaseUrl`. Compose default `Pay__PublicBaseUrl: http://localhost:8081` is **wrong for both planes**. |

**This paper’s product call:** **document A**, ship **B** when merchant settings grow, refuse **C** until M2M (02) exists, refuse **D** from the Pay repo, require **E** for laptop dogfood. Implementing C in order to “make pause work on localhost” is how you get a Pay process that holds `webhooks:write` and then someone points it at `http://localhost`.

`.env.example` already says the words:

```27:30:apps/lazuar-pay/.env.example
# One-shop HMAC fallback for POST /v1/one/webhooks. Multi-shop: owner PUT
# /v1/orgs/{orgId}/one-webhook { "webhook_secret" }. Pay does not register the
# URL with One (no PAT). One SSRF blocks loopback.
# Pay__OneWebhookSecret=
```

What it does **not** say: the One URL to register, the event filter, “shown once,” rotate, or “Production needs https:443.” That paragraph belongs in Pay README / a runbook, not in C#. Missing docs, not missing code.

### 2.10 Plane A tests inventory (live)

| Test | Proves |
|------|--------|
| `Valid_tenant_suspended_sets_charges_paused` | Compat header + `org_id` pauses |
| `Valid_tenant_id_field_sets_charges_paused` | One field name `tenant_id` |
| `Product_one_split_headers_suspend_charges` | **Live One packaging** |
| `Body_only_uppercase_hex_is_401` | Museum Hub rejected |
| `Missing_signature_is_401` | |
| `Stale_timestamp_is_401` | 300s skew |
| `Missing_secret_is_503` | process empty and no ciphertext |
| `Replay_delivery_is_duplicate` | |
| `Tenant_reactivated_clears_pause` | |
| `Empty_signed_body_is_400` | 063 |
| `Garbage_signed_body_is_400` | 063 |
| `Missing_body_id_uses_event_id_header` | |
| `Signed_json_without_event_id_is_400` | no Guid mint |
| `Member_cannot_put_one_webhook_secret` | writer overlay |
| `Put_requires_webhook_secret` | |
| `Put_and_get_does_not_echo_secret` | ciphertext + audit |
| `Two_orgs_only_matching_secret_pauses` | 029 |
| `Stored_secret_wins_over_process_fallback` | 029 |

**Not in the suite:** WrapKey-missing PUT 503; decrypt-fail 503 vs process; `tenant.deleted` leaves charges enabled; concurrent same `DeliveryId` unique 500; a test that combined `t=,v1=` is **documented** as compat (it is, in the file comment); live dispatcher integration (needs One process).

---

## 3. Plane B — PSP → Pay `POST /v1/webhooks/{provider}/{orgId}`

### 3.1 The door

```21:24:apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs
    public static void MapWebhooks(this WebApplication app)
    {
        app.MapPost("/v1/webhooks/{provider}/{orgId}", Handle);
    }
```

Allow-list is `PayProviders.TryNormalize`: `stripe|chip|billplz|xendit|razorpay|test`. Anything else (paypal, `ONE`, empty) is 400 `"unknown provider"` **before** body read finishes being used. Test `Unknown_provider_is_400`.

`test` is **not** in `PayProviders.All`. It is appended only when `AllowsTest`:

```21:22:apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs
    public static bool AllowsTest(IHostEnvironment env) =>
        env.IsDevelopment() || env.IsEnvironment("Testing");
```

Staging and Production: Test mint 400, Test webhook 400 `"rail not configured"`. Test `AllowsTest_is_laptop_and_hermetic_only` locks Staging/Production false. Issue 006’s “Staging named anything except Production is world-writable” is **fixed**.

### 3.2 Handle pipeline (the money path)

Quoted in order, because 015 / 010 / 025 live in the order:

1. Normalize provider or 400 unknown.
2. Read raw body. Whitespace/empty → 400 `"empty body"` (G20). **Before** signature. Empty unsigned is 400, not 401. Fine: there is nothing to HMAC.
3. Non-test: load `GatewayCredentials` `(orgId, provider)` or 400 `"rail not configured"`. Test: skip vault; env gate only.
4. Parse/verify. `PspVerifyException` → 400 with the parser message. `InvalidOperationException` containing `"webhook secret"` → **503**.
5. Unique lookup `(orgId, provider, eventId)` → 200 `{ duplicate: true }`.
6. `parsed.Ignored` → insert event, 200 `{ ignored: reason }`. Consumes the grain so retries no-op.
7. Join checkout: `CheckoutId` else `(OrgId, Provider, ProviderSessionId == HostedSessionId)`. Miss → 400 `"checkout not found"` **without** insert (012 class).
8. Provider mismatch (null leftover 026, or wrong rail) → 400 `"provider mismatch"` without insert.
9. `ChargesPaused` → 409 without insert.
10. Currency mismatch if `parsed.Currency` set → 400 `"currency mismatch"` without insert.
11. Amount mismatch if `parsed.AmountMinor` set → 400 `"amount mismatch"` without insert.
12. `BeginTransaction`: insert `psp_webhook_events`, `FulfillPaidAsync`, commit. Unique trip → 200 duplicate. `ChargesPausedException` → 409 rollback. Other `InvalidOperationException` → 500 `"fulfill failed"` rollback (event not consumed; PSP retries).

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

InMemory `BeginTransaction` is a no-op (077). `FillTests.Fulfill_throw_returns_5xx_event_not_committed_retry_pays` uses a probe on InMemory and is **not** a transaction proof. `PostgresTxTests.Fulfill_save_then_throw_rolls_back_event` is the proof: ThrowAfterSave → 5xx, zero documents, zero event rows; retry pays one `RCPT-`.

### 3.3 Unique `(org, provider, event_id)`

```81:85:apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs
        model.Entity<PspWebhookEventRow>(e =>
        {
            e.ToTable("psp_webhook_events");
            e.HasKey(x => new { x.OrgId, x.Provider, x.EventId });
        });
```

Same Stripe `evt_…` on tenant B is a different grain (shared platform `whsec_` was the 014 forge; vault per-org is why the PK includes `org_id`). CHIP `paid:purch_1` for two orgs with the same brand is why `org_id` is in the key. Do not drop it.

Ignored events **consume** the id (`InsertEventAsync`). Amount mismatch **does not**. That is 015 kept on purpose: fail-closed against a hostile payload; do not poison the grain if **our** unit map was wrong.

### 3.4 Per-rail parse (live)

#### Stripe

`StripeWebhook.Parse` uses Stripe.net `EventUtility.ValidateSignature` + `ConstructEvent` (`throwOnApiVersionMismatch: false`). Secret from vault ciphertext, else **Testing-only** process `Pay:StripeWebhookSecret`.

Paid types: `checkout.session.completed` **and** `checkout.session.async_payment_succeeded` (009). Completed is ignored unless `payment_status` is `paid` or `no_payment_required`. Setup / zero amount ignored. Currency required (`TryNormalizeCurrency`). `AmountTotal` is already minor — comment says do not `ToMinor` again. Join: `ClientReferenceId` / metadata `checkout_id`; `HostedSessionId = session.Id` (012 one-liner).

Tests: `WebhookTests.Completed_session_writes_receipt_and_replay_is_noop`, `Unpaid_completed_session_is_ignored`, `Async_payment_succeeded_pays_after_unpaid_completed`, `Setup_mode_is_ignored`, `Zero_amount_session_is_ignored`, `Invalid_signature_is_400`, `Missing_webhook_secret_is_503_when_rail_configured` (ciphertext nulled, process empty). `StripeRailTests.Missing_stripe_signature_header_is_400`. `FillTests` amount/currency mismatch, empty body, rail not configured, never-started provider mismatch.

**No Production-env test** that process `Pay:StripeWebhookSecret` is ignored when ciphertext is empty. Code:

```91:104:apps/lazuar-pay/src/Lazuar.Pay/Rails/Stripe/StripeWebhook.cs
    static string? ResolveSecret(...)
    {
        if (!string.IsNullOrWhiteSpace(cred.WebhookCiphertext))
        {
            return box.Unprotect(cred.WebhookCiphertext);
        }

        if (env.IsEnvironment("Testing"))
        {
            return config["Pay:StripeWebhookSecret"];
        }

        return null;
    }
```

Development with NULL ciphertext is 503, not process env. README is honest. `.env.example` (024) now says Testing-only. Grep `EnvironmentName = "Production"` under tests is **empty**. Hermetic suite never boots Production for Plane B. Remaining **test gap**, not a code lie.

#### CHIP

RSA PKCS1 SHA256 of the **raw body** vs `X-Signature` base64, public PEM from vault. PUT validates PEM (`TryChipPem`, `KeySize >= 2048`) — 027. Parse still `ImportFromPem` and 400 `"invalid signature"` on bad key (defense in depth).

`purchase.preauthorized` / `purchase.payment_failure` / other types ignored with stable ids (`preauth:`, `failed:`, `type:purchaseId`). Paid requires `purchase.paid`. `total` is **sen/cents** (RM10 → 1000). Do not divide by 100. Currency required. `HostedSessionId = purchaseId`. Test `Chip_paid_without_metadata_joins_on_purchase_id` (012). `Chip_amount_mismatch_does_not_consume_event` (total 10 vs RM10). `Chip_preauthorized_is_ignored`. `Chip_put_rejects_non_pem_webhook_secret`.

No `ChipWebhookRegistrar`. IsolationTests / refuse. Staff paste PEM. Merchant textarea exists; host now 400s `"nope"` on PUT.

#### Billplz

Form body, `x_signature` HMAC over `key+value` joined with `|` sorted. Tries with extra fields (`paid_at`, `transaction_id`, `transaction_status`) then without (Billplz two-scheme). `paid=true` or `state=paid`. `paid_amount` is sen. Currency required. Checkout id from query `checkout_id`, else form `checkout_id`, else `reference_1`. `HostedSessionId = billId`.

**Public https at start, not at webhook.** `BillplzHosted.CreateHostedUrlAsync` refuses non-https and loopback/`lazuar-local-dev.com`:

```80:106:apps/lazuar-pay/src/Lazuar.Pay/Rails/Billplz/BillplzHosted.cs
    internal static bool TryPublicBase(string? raw, out string callbackBase, out string error)
    {
        // https absolute, not loopback, not lazuar-local-dev.com
        // error = "callback base not public"
    }
```

Callback registered with Billplz:

```
{publicBase}/v1/webhooks/billplz/{orgId}?checkout_id={checkoutId}
```

Tests: `Billplz_paid_form_and_localhost_blocked` (happy path uses factory `PublicBaseUrl = https://pay.test.example`), `Billplz_localhost_callback_start_is_400_without_psp_http`, `Billplz_unpaid_is_ignored`, `Billplz_amount_mismatch_does_not_consume_event`, `Billplz_empty_body_400`.

Compose default `Pay__PublicBaseUrl: http://localhost:8081` **fails** this check. Laptop Billplz dogfood needs a tunnel origin in env. Same origin One needs for Plane A. One hole, two planes.

#### Xendit

Not HMAC of body. **Token equality** of `x-callback-token` vs vault webhook secret, compared as SHA256 of both sides (Hub 073 judgment: token length not a timing oracle). `PAID` / `invoice.paid` pays. `SETTLED` / `invoice.settled` ignored (`settled:invoiceId`). `paid_amount` is **major** then `ToMinor`. Currency required. Join metadata `checkout_id` else `external_id`. `HostedSessionId = invoiceId`.

Test `Xendit_paid_and_settled` fixture `paid_amount: 10` for RM10. **No dedicated amount-mismatch test** (015 asked per-name; Stripe/CHIP/Billplz/Test have it; Xendit/Razorpay do not). Remaining test gap.

#### Razorpay

HMAC-SHA256 hex of raw body vs `X-Razorpay-Signature`. `payment.failed` ignored. `payment_link.paid` / `expired` / `order.paid` / anything not `payment.captured` ignored. Captured: amount **already minor**, currency required, notes `checkout_id`, `HostedSessionId` from `payload.payment_link.entity.id`. Event id prefers `X-Razorpay-Event-Id` else `captured:{paymentId}`.

Test `Razorpay_captured_without_notes_joins_plink` (012 twin). `Razorpay_payment_failed_is_ignored`. No amount-mismatch test.

#### Test (dogfood rail)

Issue 006/007/008 **fixed**:

```13:34:apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestWebhook.cs
    public static PspParseResult Parse(string json, IHeaderDictionary headers, IConfiguration config)
    {
        var secret = config["Pay:TestWebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("webhook secret missing");
        }
        // X-Pay-Test-Signature HMAC-SHA256 hex of body
        // require string id, checkout_id, amount_total int64, currency
    }
```

Unsigned is 400. Missing amount/currency/id is 400. Wrong amount does not consume. Replay same id is `{ duplicate: true }`. Start-to-pay remains the **intended** Test money door (`TestHosted` redirects to success; `PublicPay` fulfills in-process). The webhook is a second door that now has a **process** HMAC, not a vault row (`PUT` of provider `test` is 400 `"test processor does not take secrets"`).

**`Pay:TestWebhookSecret` is not in `.env.example`, not in `appsettings.json`, not in compose.** Factory defaults `test_whsec_local`. A real Development host without the env var → Test webhook 503 `"webhook secret missing"`. Start-to-pay still works. If someone tunnels 8081 and posts unsigned Test JSON, 400. If they guess they need a header but the secret was never set, 503. Remaining: **document the secret or delete the Test webhook route** (006 option 2). Keeping an undocumented process HMAC is how dogfooders invent `whsec_` in the wrong place.

Production never reaches Parse: `AllowsTest` is false → 400 `"rail not configured"`. Test HMAC in Production is **not a 401**; the rail is closed. That is the right status (unknown-to-this-env rail), not “invalid signature.”

### 3.5 Amount / currency fail-closed

Handle only mismatch-checks when the parser **set** the fields. All six parsers on this SHA **require** currency on the paid path (`PspVerifyException("missing currency")`) except they never return a paid result without it. Test requires both. 007’s “Test omits and still pays” is closed.

Mismatch 400 does **not** insert (015 **kept**). Lived unit map is pinned in comments and fixtures:

| Rail | Lived unit in tests | Mismatch test? |
|------|---------------------|----------------|
| Stripe | `amount_total: 1000` cents | Yes (`FillTests`) |
| CHIP | `total: 1000` sen | Yes |
| Billplz | `paid_amount=1000` sen | Yes |
| Xendit | `paid_amount: 10` major | **No** dedicated mismatch |
| Razorpay | `amount: 1000` paise/sen | **No** dedicated mismatch |
| Test | `amount_total: 1000` | Yes |

016 P0-D “do not call units production-proven until a lived payload is checked in” still holds. Fixtures are **our** JSON, not a captured Stripe/CHIP dashboard body. Remaining: **missing lived golden files**, not a parser skip.

### 3.6 Fulfill TX and leftover after 010

`Fulfillment` in-process `SemaphoreSlim` per `checkoutId` (process-local) plus unique indexes from `20260828001217_FulfillmentUniques`:

- `charges (CheckoutId)` unique
- `documents (CheckoutId)` unique
- `documents (OrgId, Number)` unique

`FulfillPaidCoreAsync` still guards `status != "open"` then mutates. Multi-instance relies on the unique indexes. `Fulfillment` **swallows** `DbUpdateException` on its own `SaveChanges` (detaches all dirty entries) and does **not** rethrow. Handle may then `Commit` an empty change set and return `{ ok: true }` without consuming the event; PSP retry consumes. Money-safe, ops-noisy. `PostgresTxTests.Concurrent_fulfill_same_checkout_one_receipt` (two event ids, one checkout) asserts one document / one charge. That is the 010 proof on Postgres.

`FillTests.Concurrent_fulfill_of_one_checkout_mints_one_receipt` is InMemory + the semaphore, two `IFulfillPaid` calls, not HTTP.

### 3.7 Remaining Plane B bugs after issues/002

002 inbound items that are **code-closed** on this SHA: 006, 007, 008, 009, 010, 012, 015 (policy kept), 024 (docs), 025, 027, 040 (merchant prints **path** + “SPA does not know PublicBaseUrl”), 073 (spec union).

**Still true after 002:**

| # | Class | What |
|---|-------|------|
| B-1 | missing test | No Production-env Stripe process-fallback 503 proof |
| B-2 | missing test | Xendit / Razorpay amount-mismatch methods |
| B-3 | missing | Lived PSP payloads as golden files |
| B-4 | missing | `Pay:TestWebhookSecret` in env example / compose |
| B-5 | missing | Host `webhook_url_hint` built from `Pay:PublicBaseUrl` (040 printed the path only; staff still concatenate wrong origins) |
| B-6 | bug (low) | `Fulfillment` swallows unique `DbUpdateException` → possible `{ok:true}` without event consume under multi-instance race |
| B-7 | kept policy | Mismatch 400 + no consume: if **our** unit map is wrong on a lived payload, PSP retries forever, buyer paid, no `RCPT-` |
| B-8 | kept policy | Pause 409 + no consume: Stripe retries while paused; unpause inside the window pays the in-flight capture |
| B-9 | missing | Plane B has no request log / metric of 400 vs 401 vs 503 vs 409 vs 200 duplicate (and must not log HMAC) |
| B-10 | refuse | CHIP auto-register on PUT (`ChipWebhookRegistrar`). IsolationTests. Staff paste PEM. |
| B-11 | docs | Compose `Pay__PublicBaseUrl` defaults to `http://localhost:8081` which Billplz start 400s |

Issue 040 live merchant copy:

```319:328:apps/lazuar-pay-merchant/src/pages/org/GatewayPage.tsx
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

Lock test: `locks.test.ts` asserts source does **not** contain `{payApi}/v1/webhooks` and does contain `Pay:PublicBaseUrl`. The localhost:8081 **as if it were the dashboard value** is gone. The SPA still cannot print the real origin. Host GET `/gateways` does **not** return `webhook_url_hint`. Remaining missing, not the original lie.

---

## 4. Vault webhook secrets vs process fallbacks

### 4.1 Table (live)

| Secret | Where | When used | Production |
|--------|-------|-----------|------------|
| Stripe `whsec_` | `GatewayCredentialRow.WebhookCiphertext` via writer PUT | Always if present | **Required** |
| Stripe process `Pay:StripeWebhookSecret` | env | Only `Testing` **and** ciphertext empty | **Must be unset.** Dev/Prod empty ciphertext → 503 |
| CHIP PEM | vault `WebhookCiphertext` | Always | Required. PUT rejects non-PEM |
| Billplz X-Signature key | vault | Always | Required. PUT requires non-empty; no format check (Billplz is an opaque string) |
| Xendit callback token | vault | Always | Required. Equality, not HMAC |
| Razorpay webhook secret | vault | Always | Required |
| Test HMAC `Pay:TestWebhookSecret` | env only | Dev/Testing Test route | Unset in Prod (route closed). Undocumented in `.env.example` |
| One `whsec_` | `OrgSettings.OneWebhookCiphertext` via writer PUT | If present for PeekOrgId | **Required per shop** for multi-tenant |
| One process `Pay:OneWebhookSecret` | env | Orgs with **no** ciphertext | One-shop hatch. Multi-shop: unset or every shop PUT |
| `Pay:WrapKey` | env 32-byte base64 | `SecretBox` | Required outside Testing. PUT 503 if missing |

`SecretBox` comment: “Never log plaintext.” The type has no logger. `Unprotect` returns a string the parsers hold on the stack. Do not `LogInformation` it later.

### 4.2 Stripe Testing-only (024)

`.env.example`:

```11:13:apps/lazuar-pay/.env.example
# Testing-only Stripe process fallback when the vault webhook_secret is empty.
# Development and Production use the per-org vault value. Do not set this in Development.
# Pay__StripeWebhookSecret=
```

README line 67 agrees. Code agrees. Operators who paste a platform `whsec_` into Development `.env` and skip PUT will **not** verify Stripe (503). That is the leftover forge path **closed in code**; the example file no longer teaches it. 024 is closed.

### 4.3 CHIP PEM (027)

PUT:

```97:100:apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs
        if (provider == PayProviders.Chip && !TryChipPem(webhookSecret))
        {
            return PayErrors.Status(400, "Bad Request", "webhook_secret must be a CHIP PEM");
        }
```

`TryChipPem`: `ImportFromPem` + `KeySize >= 2048`. Truncated `-----BEGIN PUBLIC KEY-----\nM\n-----END PUBLIC KEY-----` fails import. `"nope"` fails. Test `Chip_put_rejects_non_pem_webhook_secret` asserts body contains `PEM`. Closed.

### 4.4 Billplz public https

Not a vault issue. The **callback URL** Billplz will POST is built at **start**, from `Pay:PublicBaseUrl`, and rejected if not public https. Webhook verify does not re-check the base (the POST already arrived). Laptop without a tunnel cannot start a Billplz bill. That is fail-closed dogfood, not a signature hole.

Merchant copy now says so. Compose default still lies (`http://localhost:8081`). Rank: **docs/compose missing**, host start is honest.

---

## 5. Replay, 401 vs 400 vs 503, empty/garbage signed body

### 5.1 Plane A matrix

| Input | HTTP | Body detail | Event consumed? | Pause applied? |
|-------|------|-------------|-----------------|----------------|
| No process secret, no ciphertext for PeekOrgId | **503** | `One webhook secret missing` | no | no |
| Missing `X-Lazuar-Signature` | **401** | `Invalid HMAC` | no | no |
| Raw uppercase hex of body | **401** | `Invalid HMAC` | no | no |
| Wrong secret (other org) | **401** | | no | no |
| Stale timestamp (>300s) | **401** | | no | no |
| Valid HMAC, empty body | **400** | `invalid event` | no | no |
| Valid HMAC, `not-json` | **400** | `invalid event` | no | no |
| Valid HMAC, JSON without id and without `X-Lazuar-Event-Id` | **400** | `event id required` | no | no |
| Valid HMAC, `tenant.suspended` + tenant_id | **200** | `{ ok: true }` | yes | yes |
| Replay same id | **200** | `{ duplicate: true }` | already | no second apply |
| `webhook.test` / `member.removed` / unknown type | **200** | `{ ok: true }` | yes | no |
| Decrypt fail of stored ciphertext | **503** | missing secret | no | no |

401 vs 400 vs 503 is **intentional**:

- **401** = authenticator failed. One retries. Attacker retries too.
- **400** = authenticator passed, payload unusable. One retries a truncated body (ops). A hostile signed payload with no id also retries; they must have the `whsec_`.
- **503** = Pay cannot authenticate (no key). One retries until ops PUT / set env. Do not 401 here: 401 would look like a bad signature and hide “Ada never stored the secret.”

Do **not** 200 empty signed bodies (063 closed). Do **not** 500 garbage JSON (063 closed).

### 5.2 Plane B matrix

| Input | HTTP | Detail | Event consumed? | Receipt? |
|-------|------|--------|-----------------|----------|
| Unknown provider | **400** | `unknown provider` | no | no |
| Empty body | **400** | `empty body` | no | no |
| Rail not in vault (non-test) | **400** | `rail not configured` | no | no |
| Test in Production/Staging | **400** | `rail not configured` | no | no |
| Vault row, empty webhook ciphertext, not Testing Stripe fallback | **503** | `webhook secret missing` | no | no |
| Test webhook, empty `Pay:TestWebhookSecret` | **503** | same | no | no |
| Bad / missing signature (all rails that HMAC) | **400** | `invalid signature` | no | no |
| Stripe missing `Stripe-Signature` | **400** | | no | no |
| Xendit wrong callback token | **400** | `invalid signature` | no | no |
| CHIP bad PEM at verify (should not survive PUT) | **400** | `invalid signature` | no | no |
| Ignored type (setup, unpaid completed, preauth, settled, unpaid bill, payment.failed) | **200** | `{ ignored: … }` | **yes** | no |
| Amount / currency mismatch | **400** | mismatch | **no** | no |
| Checkout not found / provider mismatch | **400** | | **no** | no |
| Charges paused | **409** | `Org charges are paused` | **no** | no |
| Paid unique first time | **200** | `{ ok: true }` | yes | yes |
| Replay same (org, provider, event_id) | **200** | `{ duplicate: true }` | already | no second |
| Concurrent second grain unique on charges | 200 ok or duplicate; one receipt | | see §3.6 | one |
| Fulfill throw | **5xx** | `fulfill failed` | **no** | no; retry pays (`PostgresTxTests`) |

**Plane B signature failure is 400, not 401.** Historical (Hub 500 → Pay 400). PSP dashboards treat 4xx as “do not retry forever” **or** retry depending on the rail. Stripe retries 4xx. CHIP retries 4xx. A bad HMAC that 400s will retry — same as 401 would. Distinguishing 401 (auth) vs 400 (payload) on Plane B is **not** worth a second status: PSPs do not have an HMAC vs JSON split in their retry policy the way One does. Do not “fix” Stripe 400 → 401. It would not change Stripe’s retry, and it would disagree with every rail test.

Empty unsigned body is 400 before parse. There is no “empty signed body” test per rail except CHIP/Xendit/Razorpay/Billplz `*_empty_body_400` and `FillTests.Empty_webhook_is_400`. Garbage Stripe JSON with a valid signature becomes `PspVerifyException("invalid event")` 400 (`ConstructEvent` catch). Fine.

### 5.3 Idempotency grains (do not mix)

| Plane | Unique key | Retry identity One/PSP sends that we **ignore** |
|-------|------------|--------------------------------------------------|
| A | `one_webhook_events.DeliveryId` = body `id` else `X-Lazuar-Event-Id` (event id) | `X-Lazuar-Delivery-Id` (attempt) |
| B | `(org_id, provider, event_id)` | Stripe request id; CHIP delivery; Razorpay delivery without `X-Razorpay-Event-Id` uses `captured:pay_id` |

Using One’s delivery id as Pay’s unique would double-apply on retry after 503. Pay does not.

---

## 6. Production readiness

### 6.1 Public URL

| Need | Live | Hole |
|------|------|------|
| Plane B Stripe/CHIP/Xendit/Razorpay dashboard callback | Staff paste `{PublicBaseUrl}/v1/webhooks/{provider}/{orgId}` | Host does not emit the hint. SPA prints **path only**. Ops must know PublicBaseUrl. |
| Plane B Billplz | Start **registers** the callback if PublicBaseUrl is public https | Compose default localhost **fails start**. Tunnel required. |
| Plane A One dispatcher | Ops registers `https://<pay>/v1/one/webhooks` in One | Pay does not. Loopback blocked. Strict env port 443. |
| `Pay:PublicBaseUrl` | Used by Billplz + checkout success helpers | Not used by Plane A. Not returned on GET `/gateways`. |

Without a public HTTPS origin, **neither** inbound plane works off-laptop. Hermetic tests use `https://pay.test.example` and never open a port. Production bar is: set PublicBaseUrl, terminate 443, register One, paste PSP URLs. That is a **runbook**, not a green test.

### 6.2 Multiple tenants

Plane B: vault is `(OrgId, Provider)`. Unique event is `(OrgId, Provider, EventId)`. Cross-org checkout is 400 (`WebhookTests.Cross_org_checkout_is_400`). Closed.

Plane A: per-org ciphertext (029) + tests for two secrets. Process fallback is still a **shared** hatch for shops that skipped PUT. Production with N workspaces: **unset** `Pay:OneWebhookSecret` and require PUT, or accept that unset-ciphertext shops share one secret.

`one_webhook_events` has no `OrgId`. Multi-tenant audit of Plane A is “all deliveries in one table keyed by One’s global GUID.” Acceptable for v1. Missing if ops wants “show me Ada’s One events.”

### 6.3 Secret rotation

| Secret | Rotate | Dual-verify window |
|--------|--------|-------------------|
| One endpoint | One `POST …/rotate-secret` invalidates immediately. Pay PUT overwrites immediately. | **None.** Ops race. |
| Stripe `whsec_` | Stripe dashboard + Pay PUT | Stripe may send two signatures during roll. Pay `EventUtility.ValidateSignature` uses **one** secret. Dual `Stripe-Signature` secrets are a Stripe product; Pay does not store two ciphertexts. Missing if we need zero-downtime Stripe rotate. |
| CHIP PEM | CHIP dashboard + Pay PUT | None. Old signatures fail. |
| Others | PUT overwrite | None. |

Refuse a cathedral “secret versions” table unless a lived rotate fails dogfood. Document: PUT the new secret first when the PSP still accepts the old (Stripe), or accept a retry blip (One, CHIP).

### 6.4 Timestamp tolerance

Plane A: 300s, matching One `ReplaySkewSeconds`. Locked by `Stale_timestamp_is_401`.

Stripe.net `EventUtility` default tolerance is 300s (Stripe docs). Pay does not pass a custom tolerance. Fine.

CHIP / Xendit / Razorpay / Billplz / Test: **no timestamp**. Replay of a captured signed body works forever until the event id is consumed. After consume, duplicate 200. Before consume (mismatch 400), the body is a forever-valid cheque. That is why mismatch must stay fail-closed and why Test HMAC + amount are required. Do not add a timestamp to rails that do not send one.

### 6.5 Logging without leaking HMAC

Live: **no inbound webhook logging at all** in `WebhookEndpoints` or `OneWebhookEndpoints`. Kestrel may log the request path and status. Header `X-Lazuar-Signature` / `Stripe-Signature` / `X-Signature` / `X-Razorpay-Signature` / `x-callback-token` / `X-Pay-Test-Signature` must never be at Information.

Missing production obs (not a leak): counters for 401/400/503/409/200-ok/200-duplicate/200-ignored, without payloads. `Pay:WrapKey` / ciphertext / `whsec_` must never appear. `SecretBox` already refuses to log. A later host paper (06) owns the metric names. This paper only ranks the hole: **missing P2 obs**, not a bug that leaks today.

Audit rows: `one.webhook_secret.upsert`, `gateway.credentials.upsert`, `checkout.paid`. No secret fields on `AuditEventRow` (id, org, action, at). Fine.

GET one-webhook / GET gateway never echo secrets (`last4` of the **API key**, `webhook_configured` boolean).

### 6.6 `PayErrors` shape

```12:13:apps/lazuar-pay/src/Lazuar.Pay/Hosting/PayErrors.cs
    public static IResult Status(int status, string title, string detail) =>
        Results.Json(new PayProblem { Status = status, Title = title, Detail = detail }, statusCode: status);
```

Snake_case JSON options apply. Problem `detail` is a short token (`Invalid HMAC`, `empty body`). It is not the signature. Fine.

---

## 7. How other apps relate — they do NOT call Plane B; they need Plane C

**Plane B is not a merchant integration API.** The `{orgId}` in the path is Pay’s shop id so Stripe/CHIP can POST without a Bearer. A second app that POSTs `/v1/webhooks/stripe/{orgId}` is **pretending to be Stripe**. If they have the shop’s `whsec_`, they can fulfill. That is the PSP trust model, not a Bezos door.

**Plane C (Pay → app) does not exist on this SHA.** No `payment.completed` dispatcher, no merchant webhook URL table, no `whsec_` Pay shows once to Ada’s backend. 020 index already said 019 left that kernel door out of 002. File `03-outbound-webhooks.md` owns the design. This paper only names the **confusion**:

| Doc | What it describes | Plane |
|-----|-------------------|-------|
| `plans/006-sample/05-webhook-verify-nextjs.md` | Hub **outbound** `X-Lazuar-Signature: t=,v1=` + `X-Lazuar-Event: payment.completed` from museum `OutboundWebhookSignature` | **C (Hub museum)** |
| One docs `integrations/webhooks.md` + recipe R5 | One **outbound** to any consumer (`v1=` + timestamp) | **A’s sender** (Pay is the consumer) |
| Pay `POST /v1/webhooks/{provider}/{orgId}` | PSP inbound | **B** |
| Pay `POST /v1/one/webhooks` | One inbound | **A** |
| 012/09 table | A / B / C named correctly | law |

A second-app author who reads 006/05 and points their Next.js verify helper at Pay’s Plane B URL will:

1. Expect `t=,v1=` in one header and `X-Lazuar-Event: payment.completed`.
2. Hit either Plane B (Stripe-Signature, not Lazuar) or Plane A (One catalog, not payment.completed).
3. Never learn that a charge happened unless they **poll** Pay `GET /v1/checkouts/{id}` with a staff JWT (02 says that JWT is the wrong door).

**That confusion is a gap in docs/sample (09), not an inbound bug.** Closing it is: say in Pay README and pay-spec that `/v1/webhooks/*` is PSP-only, `/v1/one/webhooks` is One-only, and merchants waiting on `payment.completed` are waiting on Plane C. Do not add a “sample verify” that HMAC-checks Plane B with One’s signer.

**Other first-party apps (not Pay merchant/checkout)** must not:

- Register themselves as a Stripe endpoint on Pay’s path.
- Share Ada’s CHIP PEM with a sibling binary.
- Call Plane A with a homemade `tenant.suspended` to pause a shop (they need the `whsec_`; if they have it, they **are** One from Pay’s point of view). Pause as an internal tool is One’s suspend button, not a Pay admin route.

---

## 8. How to solve remaining inbound holes (analysis)

Do not implement from this list. Rank is **bug** (live lie or money-unsafe) vs **missing** (not built; production still blocked or degraded) vs **refuse** (do not build).

### 8.1 Ranked — bugs

| Rank | Item | Why a bug | How to solve (shape) |
|------|------|-----------|----------------------|
| P1 | `tenant.deleted` does not pause | Buyers still pay a wiped One tenant. Staff cannot. | Apply like suspend. Same handler arm. Test: deleted → start 403. Do not cascade-delete money rows. |
| P2 | Plane A unique is `AnyAsync` then insert; unique trip is unhandled 500 | Plane B maps unique to `{duplicate:true}`. One retries 500. Pause might already be set. | Catch `DbUpdateException` → `{ duplicate: true }` like Plane B. |
| P2 | `Fulfillment` swallows unique `DbUpdateException` | Multi-instance can 200 ok without consuming the PSP event | Rethrow or return a dedicated already-paid; Handle maps to duplicate. Keep unique indexes. |
| P3 | Most Plane A tests still mint combined `t=,v1=` | 011 live path is one test. Regression could break split headers while CI stays green | Make `Sign` set **both** dispatcher headers; keep one named compat test. |

Not bugs (kept policy): mismatch 400 no consume; pause 409 no consume; process One hatch shared across shops that skipped PUT.

### 8.2 Ranked — missing

| Rank | Item | Blocks production? | How to solve |
|------|------|--------------------|--------------|
| P0 ops | **Register Pay’s public URL with One** | Yes for the buyer belt. Staff suspend still 403s membership. Buyers do not. | **Document** the curl / lazuar-app Settings steps. Do **not** implement Pay→One POST until M2M (02). Laptop: tunnel + allowlist hatch on **One**, not a Pay change. |
| P0 ops | **Public HTTPS origin** (`Pay:PublicBaseUrl` real, 443) | Yes for Billplz and for One strict env. Stripe/CHIP/Xendit/Razorpay dashboards too. | Compose/docs: do not default localhost. Host GET `/gateways` may return `webhook_url_hint`. |
| P1 | Merchant UI for One `whsec_` PUT | Multi-shop pause is curl-only | Settings field calling existing PUT. Still no URL registration. |
| P1 | Unset process One secret in multi-shop prod runbook | Shared hatch | README: “N tenants ⇒ N PUTs; leave `Pay__OneWebhookSecret` empty.” |
| P1 | `tenant.deleted` apply | See bugs | Same as P1 bug. |
| P2 | Pull catch-up | No for suspend (One 403s events when suspended) | Do not build pull as the suspend path. Optional catch-up for `webhook.test` / missed member chrome. Refuse as money gate. |
| P2 | Dual-verify on One/Stripe rotate | Blip on rotate | Document order. Stripe two-secret window later if dogfood hurts. |
| P2 | `Pay:TestWebhookSecret` in `.env.example` **or** delete Test webhook | Only if Dev is tunneled | Prefer **delete Test webhook** (006 option 2). Start-to-pay is the Test money door. A second HMAC door that is undocumented will be copy-pasted wrong. |
| P2 | Xendit/Razorpay mismatch tests + lived golden JSON | Honesty of units | Add methods; check in one captured payload per rail when someone actually pays. |
| P2 | Production-env Stripe empty-ciphertext 503 test | Regression of Testing-only fallback | One factory `EnvironmentName = Production` with WrapKey. |
| P2 | Plane A `OrgId` on `one_webhook_events` | Audit | Optional. Not money. |
| P2 | Inbound metrics without HMAC | Ops | 06-host-production. |
| P3 | Host-emitted `webhook_url_hint` | Staff paste errors | GET `/gateways` field from PublicBaseUrl. Never localhost. |

### 8.3 Ranked — refuse

| Item | Why refuse |
|------|------------|
| Pay POSTs One `/tenants/{id}/webhooks` **this program** | Needs M2M `webhooks:write`. 02. No PAT. |
| Weaken One SSRF from the Pay repo | Loopback block is the product. Hatch is One `UrlHostAllowlist`. |
| `member.*` apply-handlers / Pay membership cache | SoT is `/me`. Auto-join does not emit `member.accepted`. |
| `api_key.revoked` apply | Pay does not cache One keys. Revisit when 02 caches them. |
| `ownership.transferred` / `oidc_app.*` / invites | Not money. |
| Plane A under `/v1/webhooks/{provider}` | Different HMAC. 012/09 law. |
| Plane B auth = One HMAC | Different secret. |
| CHIP auto-register webhook on PUT | IsolationTests. Registrar talks to CHIP with the secret. Staff paste PEM. |
| Test rail in Production | `AllowsTest` false. Do not add `Pay:EnableTestProcessor` default true. |
| 200 on bad HMAC | Would ack forgeries. |
| Poison-insert on amount mismatch | Hides a later parser correction (015). |
| Import Hub `OutboundWebhookSignature` / project-reference One | Museum vs product dialect. Copy the algorithm. |
| Plane C on these routes | Other apps need 03, not a new PSP provider name. |
| Pull `GET /tenants/{id}/events` as the suspend gate | One 403s that list after suspend. |
| Tail Zitadel | 012 law. |
| Logging `X-Lazuar-Signature` / `Stripe-Signature` / PEM | Leak. |

### 8.4 Document vs implement (registration) — explicit

**Implement in Pay C# this week:** nothing for registration. The PUT secret hatch exists. The receiver exists. The dialect matches.

**Document (09 / README / runbook):**

1. Deploy Pay behind https:443. Set `Pay:PublicBaseUrl` to that origin. Do not use compose’s localhost default.
2. In **One** (lazuar-app or `POST /api/v1/tenants/{id}/webhooks` as owner), create an endpoint:
   - URL: `https://<Pay:PublicBaseUrl>/v1/one/webhooks`
   - Events: `tenant.suspended`, `tenant.reactivated`, `webhook.test` (and later `tenant.deleted` when Pay applies it)
3. Copy `whsec_…` once. Writer `PUT https://<pay>/v1/orgs/{orgId}/one-webhook` `{ "webhook_secret": "whsec_…" }`. GET until `webhook_configured: true`.
4. One → test ping. Pay stores `webhook.test`. 200 `{ ok: true }`.
5. Suspend the tenant in One. Buyer `POST /v1/pay/{token}/start` is 403 `"Org charges are paused"`. Staff mint 403.
6. For each PSP: PUT vault including `webhook_secret`; paste `{PublicBaseUrl}/v1/webhooks/{provider}/{orgId}` in the dashboard (Billplz is auto-registered at start if PublicBaseUrl is public).
7. Multi-shop: repeat 2–5 per tenant. Leave `Pay__OneWebhookSecret` empty.
8. Laptop: Cloudflare tunnel to 8081; use that https origin for **both** One registration and `Pay:PublicBaseUrl`. Do not put `localhost` on One allowlist in production appsettings.

**Implement later (not inbound C#):** merchant field for step 3; GET `/gateways` hint for step 6; M2M for step 2 (02 + this slice’s option C).

---

## 9. Evidence index (files actually opened)

**Pay host**

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookSignature.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Webhooks/PspParseResult.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/{Stripe,Chip,Billplz,Xendit,Razorpay,Test}/*Webhook.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Billplz/BillplzHosted.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Secrets/SecretBox.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260828001217_FulfillmentUniques.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260828093000_OrgOneWebhookCiphertext.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Program.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneClient.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Hosting/PayErrors.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/.env.example`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/README.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/docker-compose.pay.yml`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/appsettings.json`

**Tests**

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OneWebhookTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Webhooks/{WebhookTests,FillTests,PostgresTxTests}.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/{Stripe,Chip,Billplz,Xendit,Razorpay,Test}/*RailTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Credentials/GatewayTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs`

**Merchant**

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/pages/org/GatewayPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/locks.test.ts`

**Spec**

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/pay-spec/main.tsp`

**Sibling One**

- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Webhooks/WebhookSigning.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Webhooks/WebhookDispatcher.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Webhooks/WebhookUrlValidator.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/Webhooks/WebhookEventCatalog.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/appsettings.json`

**Issues / papers (historical; live files win)**

- `issues/002/{006,007,008,009,010,011,012,015,024,025,027,029,033,040,063,073}-*.md`
- `issues/002/README.md`
- `plans/012-one-to-pay/09-webhooks-events.md`
- `plans/006-sample/05-webhook-verify-nextjs.md` (Plane C museum; confusion named in §7)

---

## 10. Verdict (this slice only)

On `6d730d15`, **the inbound C# doors exist and the 002 money bugs that lived on them are closed in source:**

- Plane A speaks product One’s split headers; suspend sets `ChargesPaused`; buyers 403; per-org `whsec_` is wrapped next to the shop; process env is a one-shop fallback; empty/garbage signed bodies are 400; member cannot PUT the secret; two orgs cannot steal each other’s pause.
- Plane B allow-lists six names; unique `(org, provider, event_id)`; amount/currency fail-closed when the parser sets them; Test HMAC + required amount/id; Test closed in Staging/Production; Stripe honors `payment_status` and `async_payment_succeeded`; CHIP joins purchase id; Billplz refuses localhost callbacks at start; CHIP PEM is rejected on PUT; fulfill unique indexes + Postgres TX proof.

**They are not production-ready as a system** because inbound webhooks are not only handlers:

1. **Nobody registers Pay’s URL with One.** Loopback is blocked. Strict One wants https:443. Pause as a **buyer** belt is unwired in any environment where that POST never happens. Hermetic tests are not that POST.
2. **Nobody prints a public Plane B URL the PSP can reach.** The SPA prints a path. Compose prints localhost. Billplz start is the only rail that refuses to lie; the others will accept a dashboard pointed at 127.0.0.1 and then never get a callback.
3. **`tenant.deleted` is stored and ignored.** Wipe in One, buyers still pay.
4. **`member.*` and `api_key.revoked` are correctly not applied for money.** Refuse. Do not “complete the catalog” as a substitute for `/me`.
5. **Other apps must not call Plane B.** They need Plane C (03). 006-sample’s Hub outbound verify recipe will teach the wrong door until 09 says so.

How to solve: runbook the registration (do not implement Pay→One POST), put a real PublicBaseUrl in front of 8081, apply `tenant.deleted` like suspend, delete or document Test’s process HMAC, keep mismatch/pause fail-closed, keep planes split. That is the inbound production bar. Kernel outbound (`payment.completed`) is a different plane and a different file.
