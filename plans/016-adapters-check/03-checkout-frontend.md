# 03 — Checkout Vite (`lazuar-pay-checkout` `:5179`) vs `PublicPayEndpoints`

**Date:** 24 August 2026  
**Program:** [016-adapters-check](./README.md) — double-check new Pay gateway adapters, frontends, Hub HTTP, and tests  
**Slice:** `apps/lazuar-pay-checkout` (Vite **`:5179`**) versus focused Pay public doors `GET /v1/pay/{token}` and `POST /v1/pay/{token}/start`. Start body, `email_required`, verifying poll, no OIDC, no wallet tiles, 503/400 mapping.  
**Kind:** uncondensed evaluation. **Not** an implementation. **Not** a product-code change. **Not** a flip of 011/11 or 015 A99.1 lived-loop cells.  
**Audience:** the parent 016 judgment (`00-evaluation.md`) and anyone about to treat 015 K10–K17 checkboxes as equivalent to a production cash register.

Live code is authority. 015 Track K checklists are a map, not proof. Several K boxes still contain a “live today” paragraph written against the 014 Stripe-only SPA (GET once, no query read) that is **false** on this SHA, next to a “Change” paragraph that **is** live. This paper records the disagreement instead of flattening it.

014 [03-checkout-frontend.md](../014-evals/03-checkout-frontend.md) described `:5179` as a Stripe hop-2 pixel: GET once, Pay form even after `?status=verifying`, 503 → `rail not configured`, no 400 special-case, email optional. That paper is historical for this slice. Re-read this file against `c621ceba`, not against `ee2db8e5`.

---

## Repos and SHAs (as read)

| Repo | Path | Branch | Full SHA | Short | Tip |
|------|------|--------|----------|-------|-----|
| **Lazuar Pay** (this tree) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` | `feat/015-four-adapters` | `c621ceba7fc7b79f16954d0819200cb21db6f22b` | `c621ceba` | `docs(015): check off implemented T–Q phases` |

Recorded from `.git/HEAD` → `refs/heads/feat/015-four-adapters` and the 016 index pin. The 016 README named the same short SHA at analysis start.

**Checkout Vite:** `vite ^8.2.0`, React `^19.2.8`, port **5179** `strictPort`, preview **4179**. No `react-router`, no OIDC packages, no `@repo/api-types-ts`.  
**Focused Pay host:** `apps/lazuar-pay` on **http://localhost:8081**. Public buyer doors only.  
**Merchant Vite:** `apps/lazuar-pay-merchant` on **5178** (OIDC). Mints `http://localhost:5179/c/{public_token}`. Not this SPA, except as the shareable URL source.  
**Old Hub portal:** `apps/lazuar-portal` **:3004**. Do not retarget.  
**Identity:** One API **8080**, product login **:5175**. Checkout must not call those.

If a sentence does not say **focused Pay** vs **old Hub** vs **One**, assume it is wrong.

---

## Locked (do not bargain from this origin)

| Lock | ID | Meaning for `:5179` |
|------|----|---------------------|
| Buyers are not One/Zitadel humans | NP-XX-013, NP-CHK-007, 015 K15 | Fail if this page asks for Zitadel / `:5175` login. |
| No Pay password form | NP-XX-007 | No `/login`, no email+password against One. |
| Hosted cash register | NP-CHK-005 | Buyer page on Pay’s origin, not Hub `:3004`. |
| Shareable pay link | NP-CHK-006 | `http://localhost:5179/c/{token}`. |
| Success/cancel URLs are not fulfillment | NP-CHK-002, 015 K14 | Query `status=verifying` is not paid. Webhook writes `RCPT-`. |
| Wrap-rails pixel | NP-GW-007, 015 K12 / K17 | Start a hosted PSP session. Do not collect PAN. Do not draw GrabPay / TnG / FPX tiles. |
| One active rail | 015 K10 | Buyer does not pick a PSP. |
| Email when the rail needs it | NP-BUY-001, 015 K11 / P19 / P20 | CHIP / Billplz / Xendit / Razorpay refuse blank and `customer@example.com`. Stripe may stay optional. |
| 503 honesty | 015 K16 / P24.2 | Missing org rail on **start** is 503 `rail not configured` (buyer-facing). Webhook missing rail stays 400 (PSP-facing). Billplz localhost callback is **400** `callback base not public`. |

---

## 1. Method / files opened

Nothing was implemented. The following were opened in full or in the cited ranges.

### 1.1 Checkout SPA (live)

| Path | Why |
|------|-----|
| `apps/lazuar-pay-checkout/src/App.tsx` | Entire runtime: path token, GET, poll, start, pixels. |
| `apps/lazuar-pay-checkout/src/locks.test.ts` | OIDC / wallet / PAN string locks. |
| `apps/lazuar-pay-checkout/package.json` | Port 5179, React 19 only, test script. |
| `apps/lazuar-pay-checkout/src/main.tsx` | StrictMode mount. No router. |
| `apps/lazuar-pay-checkout/src/App.css`, `src/index.css` | Layout only. No wallet CSS. |
| `apps/lazuar-pay-checkout/index.html` | Title “Lazuar Pay — checkout”. |
| `apps/lazuar-pay-checkout/vite.config.ts` | Dual-pin 5179; preview 4179. |
| `apps/lazuar-pay-checkout/vitest.config.ts` | Node env; `src/**/*.test.ts`. |
| `apps/lazuar-pay-checkout/README.md` | “Buyers have no One account.” |
| `apps/lazuar-pay-checkout/.env.example` | `VITE_PAY_API_URL=http://localhost:8081` only. |
| `apps/lazuar-pay-checkout/tsconfig.json`, `tsconfig.app.json`, `tsconfig.node.json` | Bundler mode. `noUnusedLocals`. |
| `apps/lazuar-pay-checkout/dist/index.html` | Built asset names. Dist JS does **not** contain current source strings. |

### 1.2 Public host (live)

| Path | Why |
|------|-----|
| `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` | Entire GET + Start. |
| `apps/lazuar-pay/src/Lazuar.Pay/Gateways/BuyerEmail.cs` | Placeholder + `IsUsable`. |
| `apps/lazuar-pay/src/Lazuar.Pay/Gateways/PayProviders.cs` | `RequiresEmail` = not Stripe. |
| `apps/lazuar-pay/src/Lazuar.Pay/Gateways/StripeHosted.cs` | Success URL `?status=verifying`. |
| `apps/lazuar-pay/src/Lazuar.Pay/Gateways/ChipHosted.cs` | Same success URL; email throw; brand_id. |
| `apps/lazuar-pay/src/Lazuar.Pay/Gateways/BillplzHosted.cs` | Same success URL; `TryPublicBase` → `callback base not public`. |
| `apps/lazuar-pay/src/Lazuar.Pay/Gateways/XenditHosted.cs` | Same success URL; email throw. |
| `apps/lazuar-pay/src/Lazuar.Pay/Gateways/RazorpayHosted.cs` | Same callback URL; email throw. |
| `apps/lazuar-pay/src/Lazuar.Pay/One/PayErrors.cs` | `{ status, title, detail }` JSON. |
| `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` | CORS 5178/5179; snake_case JSON; `MapPublicPay`. |
| `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs` | Merchant mint; `Status = "open"`; no provider at create. |
| `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutStore.cs` | Public token lookup. |
| `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutSession.cs` | Includes `PayerName` / `PayerEmail`. |
| `apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs` | `CheckoutRow.Provider`, `OrgSettingsRow.ActiveProvider`. |
| `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs` | Only writer of `status = "paid"`. Never `"expired"`. |
| `apps/lazuar-pay/src/Lazuar.Pay/Gateways/IHostedRail.cs` | Two-method hosted rail. |
| `apps/lazuar-pay/src/Lazuar.Pay/appsettings.json` | No `Pay:PublicBaseUrl` default. |

### 1.3 Tests and spec

| Path | Why |
|------|-----|
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPayTests.cs` | GET no Bearer; missing 404. Empty webhook 400 wandered in. |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/RailTests.cs` | Chip start without email 400; start with email for four rails. Misnamed Billplz localhost test. |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PayApiFactory.cs` | Sets `Pay:PublicBaseUrl=https://pay.test.example`. |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/CorsTests.cs` | 5179 allowed; 3003/3004 denied. |
| `packages/pay-spec/main.tsp` | `PublicPay.email_required?`; start has no request body. |
| `packages/pay-spec/dist/openapi.yaml` | Stale: `PublicPay` **omits** `email_required`; service description still says fixture. |

### 1.4 015 / 016 maps

015 Track K opened in full: `k10-no-provider-picker.md`, `k11-email-required-by-rail.md`, `k12-no-wallet-tiles.md`, `k13-verifying-poll.md`, `k14-success-url-not-paid.md`, `k15-no-oidc.md`, `k16-503-rail.md`, `k17-no-pan.md`. Also P17–P20, P24, B15, C30, B26, X22, R24, X20, A99, `00-what-must-be-done.md` §6.2. 016 README. Merchant `WorkspacePage.tsx` only for the copied pay URL and wrap copy (not a second SPA review). Hub `GatewayCommon.cs` only for the email **decision** (`IsUsableBuyerEmail` / placeholder), not as a type to copy.

---

## 2. What `:5179` is on this SHA

`lazuar-pay-checkout` is a single-file React 19 SPA. `main.tsx` mounts `<App />` under `StrictMode`. There is no router: the cash-register URL is a **path convention** parsed from `window.location.pathname`.

```14:21:apps/lazuar-pay-checkout/src/App.tsx
function tokenFromPath(): string | null {
  const m = window.location.pathname.match(/^\/c\/([^/]+)/)
  return m ? decodeURIComponent(m[1]) : null
}

function verifyingQuery(): boolean {
  return new URLSearchParams(window.location.search).get('status') === 'verifying'
}
```

The regex is anchored: `/c/{token}` matches; `/c/{token}/extra` does not; `/pay/{token}` does not; `/` does not. Merchant minting writes exactly `http://localhost:5179/c/${body.public_token}` (`WorkspacePage.tsx` around the `public_token` branch). Hosted rails default success URLs to the same path plus `?status=verifying`.

`package.json` pins the process:

```6:13:apps/lazuar-pay-checkout/package.json
    "dev": "vite --port=5179 --host=0.0.0.0 --strictPort",
    "build": "tsc -b && vite build",
    "lint": "oxlint",
    "test": "vitest run",
    "check-types": "tsc -b",
    "preview": "vite preview --port=4179 --strictPort",
```

`vite.config.ts` dual-pins 5179 with `strictPort: true` so a busy port **fails** rather than silently stealing merchant `:5178`. Preview is 4179. CORS on 8081 allow-lists `http://localhost:5179` and `http://127.0.0.1:5179` only. **Preview origin `http://localhost:4179` is not in the CORS policy.** A `pnpm preview` dogfood against 8081 will look like a hung GET (see §6 Loading graveyard).

API base:

```4:4:apps/lazuar-pay-checkout/src/App.tsx
const payApi = import.meta.env.VITE_PAY_API_URL ?? 'http://localhost:8081'
```

`.env.example` is that one line. Never Hub `:8080`. Never `VITE_*` secrets. The SPA talks to two URLs only: `GET ${payApi}/v1/pay/{token}` and `POST ${payApi}/v1/pay/{token}/start`. No `/v1/whoami`, no `/v1/checkouts/{id}`, no `/v1/orgs/.../gateway`, no Stripe.js, no CHIP SDK.

`PayView` is a local structural type, not generated from `pay-spec`:

```6:12:apps/lazuar-pay-checkout/src/App.tsx
type PayView = {
  token: string
  amount: number
  currency: string
  status: string
  email_required?: boolean
}
```

The host GET also returns `payer_name` and `payer_email`. The SPA type omits them and never prefills the inputs. That matters after cancel / retry (§8).

Dependencies in `package.json` are `react` and `react-dom` only. `locks.test.ts` forbids `oidc-client-ts`, `react-oidc-context`, and `@repo/api-types-ts` by reading the package file as text. There is no hidden OIDC via a workspace package: checkout does not depend on `@repo/*`.

---

## 3. Host public doors (the only 8081 surface this SPA uses)

`Program.cs` maps `MapPublicPay()` with no auth middleware on those routes. JSON is snake_case globally:

```14:18:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
});
```

GET and Start both go through `CheckoutStore.GetByPublicTokenAsync`. Missing token → `PayErrors.Status(404, "Not Found", "Checkout not found")`. The error body is `{ status, title, detail }`. The SPA **never reads `detail`**. It branches on HTTP status (start) or on `r.status === 404` / `r.ok` (GET).

### 3.1 GET `/v1/pay/{token}`

```17:38:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
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
        return Results.Json(new
        {
            token,
            amount = session.Amount,
            currency = session.Currency,
            status = session.Status,
            payer_name = session.PayerName,
            payer_email = session.PayerEmail,
            email_required = emailRequired
        }, OneClient.Json);
    }
```

Facts that the SPA depends on:

1. **No Bearer.** `PublicPayTests.Public_get_does_not_need_bearer` asserts a second GET does not increment One `SendCount`. That is the NP-CHK-007 host half.
2. **`email_required` is computed, not stored.** Source is `checkout.Provider` if already set (after a successful start), else `org_settings.ActiveProvider`. `PayProviders.TryNormalize` must succeed **and** `RequiresEmail` must be true.
3. **`RequiresEmail` is “not Stripe”**, not an explicit CHIP/Billplz/Xendit list:

```24:25:apps/lazuar-pay/src/Lazuar.Pay/Gateways/PayProviders.cs
    public static bool RequiresEmail(string provider) =>
        provider is not Stripe;
```

   After `TryNormalize`, the allowed names are `stripe | chip | billplz | xendit | razorpay`. So: Stripe → `email_required: false`. The other four → `true`. Unknown / missing provider → `TryNormalize` fails → `email_required: false` (boolean false, still present in JSON because the anonymous object always includes the property).
4. **Status is the checkout row status.** Create writes `"open"`. `Fulfillment.FulfillPaidAsync` writes `"paid"` in the same SaveChanges as `RCPT-` and the two-line journal. **Nothing in `apps/lazuar-pay` writes `"expired"`.** The SPA expired pixel is dead on this SHA unless a future job exists outside these files.
5. GET is not a charge. It does not mint a PSP session. Polling GET is free of Stripe/CHIP HTTP.

`pay-spec` `main.tsp` model `PublicPay` includes `email_required?: boolean` and omits `payer_name` / `payer_email`. Compiled `packages/pay-spec/dist/openapi.yaml` `PublicPay` **omits `email_required`**. The generated OpenAPI is stale relative to both `main.tsp` and live GET. The SPA does not import the spec; this is a contract-doc hole, not a runtime hole.

### 3.2 POST `/v1/pay/{token}/start`

Entire Start handler, because every status the SPA maps is produced here:

```41:120:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
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

        IHostedRail rail = name switch
        {
            PayProviders.Stripe => stripe,
            PayProviders.Chip => chip,
            PayProviders.Billplz => billplz,
            PayProviders.Xendit => xendit,
            PayProviders.Razorpay => razorpay,
            _ => stripe
        };

        try
        {
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
```

`StartPayRequest` is `{ Name?, Email? }`. Snake_case + case-insensitive JSON maps the SPA body `{ name, email }`. Empty strings are whitespace → **not written** onto the row. A previous start that persisted `PayerEmail` therefore survives a second start with blank email **on the host**. The SPA still blocks that click when `email_required` and the **input** is empty (§8).

015 P17.2 said unknown `active_provider` → **400**. Live unknown / null provider after `TryNormalize` is **503** `"rail not configured"`. The SPA 503 map therefore fires. The checkbox is wrong; the buyer-facing code is consistent with K16 / P24.2 “keep 503 on public start.”

015 P17.1 “Start takes `StripeHosted` and always calls it” is stale. Start injects five rails and switches. Default `_ => stripe` is unreachable after `TryNormalize` (only the five names pass). There is no Stripe-then-CHIP fallback.

P18 is live: on success, `Provider`, `PspRedirectUrl`, `ProviderSessionId` persist before `{ redirect_url }` returns. Second start while still `open` mints a **new** PSP session (no reuse of `PspRedirectUrl`). The SPA does not send `provider`. The buyer cannot override the rail (K10).

---

## 4. Fetch sequence (what the browser actually does)

Two effects and one click handler. No router, no SWR, no AbortController.

### 4.1 Boot, no query (merchant paste of the pay link)

Merchant copies `http://localhost:5179/c/{token}` with **no** `status` query (`WorkspacePage.tsx`).

1. `tokenFromPath()` returns the path segment (hex token from two GUIDs concatenated in `CheckoutEndpoints.Create`). `verifyingQuery()` is false. `verifying` state is false for the SPA lifetime (`setVerifying` is never called after `useState` init).
2. First `useEffect` (`[token]`): `GET http://localhost:8081/v1/pay/{token}` with default fetch mode (CORS, **no** `Authorization`, **no** `credentials: "include"`).
3. Until that returns, `error` is null and `pay` is null → pixel **Loading…**.
4. 404 → `error = 'missing'` → missing pixel (“This payment link is not valid. No sign-in.”).
5. Other non-OK → `error = 'status {code}'` but `pay` stays null. Render order checks `error === 'missing'` first, then `if (!pay) return <p>Loading…</p>`. **A dead 8081, a CORS failure on 4179, or GET 500 paints Loading… forever.** Same graveyard 014 recorded. 015 did not add an error pixel.
6. 200 → `setPay(body)`. `status === 'open'` (normal) and `verifying === false` → **Pay form**. `email_required` from the body drives the Pay button disable. `status === 'paid'` → Paid pixel without ever showing Pay. `status === 'expired'` → Expired pixel (unreached on this SHA).
7. Second `useEffect` (`[token, verifying, pay?.status]`): `verifying` is false → **return immediately**. No poll.

`StrictMode` in dev runs the GET effect twice. The `stop` flag drops the first response. Two GETs on a fresh tab in development is expected, not a double-charge (GET does not start a rail).

### 4.2 Click Pay (hop-2)

`startPay`:

1. Guard: no token → return.
2. Guard: `pay.email_required && !email.trim()` → `setError('email is required')`, **no HTTP**. This is the only client-side use of the exact host email detail string.
3. `setBusy(true)` disables the button (`disabled={busy || emailBlocked}`).
4. `POST ${payApi}/v1/pay/${token}/start` with `Content-Type: application/json` and `JSON.stringify({ name, email })`. Always both keys. Empty strings are sent. No Bearer.
5. Status map (see §9). 503 / 400 / other non-OK set `error` and return inside `try`; `finally` still `setBusy(false)`.
6. 200 JSON `{ redirect_url }` → `window.location.assign(body.redirect_url)`. The SPA **leaves** `:5179` for the processor origin (`checkout.stripe.com`, CHIP `checkout_url`, Billplz bill URL, Xendit `invoice_url`, Razorpay `short_url`).
7. 200 without `redirect_url`: function ends. Busy clears. Form remains. No alert. Host always sets `redirect_url` on the success path; a malformed proxy could still hit this.

`fetch` throw (network / CORS) is **not** caught. `finally` still clears `busy`. The rejection is unhandled. No `role="alert"`.

There is no start idempotency key. Double-click is UI-debounced by `busy`, not by the host. Two tabs can mint two PSP sessions for one open checkout; the last `ProviderSessionId` wins on the row. Webhook fulfillment still keys off checkout id / metadata, not the abandoned session.

### 4.3 Return from processor with `?status=verifying`

Default success URLs (when merchant create omitted `success_url`, which the merchant SPA does):

| Rail | Field | Default |
|------|-------|---------|
| Stripe | `SuccessUrl` | `http://localhost:5179/c/{token}?status=verifying` |
| CHIP | `success_redirect` | same |
| Billplz | `redirect_url` | same |
| Xendit | `success_redirect_url` | same |
| Razorpay | `callback_url` | same (`callback_method=get`) |

CHIP / Stripe / Xendit cancel/failure URLs are `http://localhost:5179/c/{token}` **without** the query. Billplz and Razorpay have no separate cancel URL in the hosted payload (Billplz only `redirect_url`; Razorpay only `callback_url`). A Billplz/Razorpay cancel that still hits the redirect URL will look like success-return to this SPA.

Browser lands on `/c/{token}?status=verifying`. Full document load (not SPA client nav). Fresh React tree.

1. `verifyingQuery()` true. `verifying` state true.
2. First effect: same GET as boot.
3. While `pay` is null: **Loading…** (not yet Verifying). The verifying branch requires `pay`.
4. GET 200 `status: "paid"`: Paid pixel. Poll effect sees `pay?.status === 'paid'` and does not start an interval. Honest: webhook already won the race.
5. GET 200 `status: "expired"`: Expired pixel. Poll does not start.
6. GET 200 `status: "open"` (webhook not in yet, which is the design): render hits `if (verifying && pay.status !== 'paid')` → **Verifying…**. Pay form is **not mounted**.
7. Poll effect starts because token && verifying && status is not paid/expired.

Cancel return without query: `verifying` false. GET `open` → **Pay form again**. That is honest: cancel is not paid, and it is not “waiting for webhook.” The buyer can click Pay and mint another hosted session.

### 4.4 Sequence diagram (open checkout, CHIP, webhook slower than the redirect)

```
buyer            :5179 SPA              :8081 PublicPay           CHIP / webhook
 |                  |                        |                        |
 |  GET /c/tok      |                        |                        |
 |----------------->| GET /v1/pay/tok        |                        |
 |                  |----------------------->| 200 open, email_required true
 |  Pay form        |<-----------------------|                        |
 |  type email, Pay |                        |                        |
 |----------------->| POST /v1/pay/tok/start {name,email}             |
 |                  |----------------------->| ChipHosted purchases/  |
 |                  |                        |----------------------->|
 |                  | 200 {redirect_url}     |                        |
 |  location.assign |<-----------------------|                        |
 |==============================================================> CHIP hosted page
 |  pay on CHIP     |                        |                        |
 |  success_redirect|                        |  POST /v1/webhooks/chip/{orgId}
 |<-----------------|                        |<-----------------------|
 |  GET /c/tok?status=verifying              |  (fulfill may still be in flight)
 |----------------->| GET /v1/pay/tok        |                        |
 |  Loading…        |----------------------->| 200 open, email_required true
 |  Verifying…      |<-----------------------|                        |
 |                  |  wait 2000ms                                    |
 |                  | GET /v1/pay/tok        |  (webhook committed paid)
 |                  |----------------------->| 200 paid               |
 |  Paid pixel      |<-----------------------|                        |
```

If the webhook is already committed before the first GET, the Verifying pixel never paints. Paid is checked **before** the verifying branch.

---

## 5. Poll interval — exact arithmetic

```56:69:apps/lazuar-pay-checkout/src/App.tsx
  useEffect(() => {
    if (!token || !verifying || pay?.status === 'paid' || pay?.status === 'expired') return
    let n = 0
    const id = window.setInterval(() => {
      n += 1
      void fetch(`${payApi}/v1/pay/${token}`)
        .then((r) => (r.ok ? r.json() : null))
        .then((body: PayView | null) => {
          if (body) setPay(body)
        })
      if (n >= 15) window.clearInterval(id)
    }, 2000)
    return () => window.clearInterval(id)
  }, [token, verifying, pay?.status])
```


| Claim in 015 K13.2 | Live |
|--------------------|------|
| Poll `GET /v1/pay/{token}` every ~2s | **Yes.** `setInterval(..., 2000)`. |
| Cap ~30s | **Yes, if you count ticks.** `n` increments **inside** the interval callback. First tick at t=2s (`n=1`). Fifteenth tick at t=30s (`n=15`) then `clearInterval`. There is **no immediate poll** on effect start; the initial GET effect is the t=0 sample. Total **interval** GET count is 15, plus 1 initial GET, plus StrictMode extras in dev. |
| Stop on paid/expired/missing | **Paid / expired: yes** (effect guard + Paid/Expired pixels earlier in render). **Missing: no.** Poll treats non-OK as `null` and ignores it. A 404 after the checkout row is deleted leaves Verifying… up and keeps ticking until n=15. The missing pixel is only from the **initial** GET 404 path (`error === 'missing'`), which also does **not** prevent the poll effect: poll deps do not include `error`. If initial GET 404s, `pay` stays null, `error` is `missing`, missing pixel shows, **and** the poll effect still starts (token set, verifying true, `pay?.status` undefined). Fifteen more 404s against a dead token. |
| Do not treat query as paid | **Yes.** Query only sets `verifying`. Paid pixel is `pay.status === 'paid'` from JSON. |
| Do not add OIDC to poll | **Yes.** Same unauthenticated GET. |

Other poll honesty:

- **No catch.** A thrown `r.json()` or network error is an unhandled rejection. `pay` is unchanged. Verifying stays.
- **Non-OK including 409/500:** ignored. No error string.
- **Re-subscribe:** deps include `pay?.status`. Every successful poll that **keeps** `open` does **not** restart the interval (`status` still `'open'`). A transition to `paid` re-runs the effect, hits the guard, cleanup clears the old interval. Good.
- **Cap is not a UX transition.** After 15 ticks the interval dies and the Verifying pixel remains forever if still `open`. There is no “still waiting — return to Pay” and no way to click Pay without dropping `?status=verifying` (the buyer would have to edit the URL). K13’s goal was “not the Pay form again” after success return; the cost is a **stuck Verifying** when the webhook is late or never arrives (wrong `Pay:PublicBaseUrl` for Billplz, Stripe `whsec` mismatch, CHIP PEM wrong). 015 K16 said “Do not spin forever.” Poll spinning stops at 30s; the **pixel** does not.
- **`setVerifying` is unused** after init. Query is sticky for the document lifetime. `popstate` / manual query edit without reload does not update React state. Fine for `location.assign` returns; not a router app.

015 K13.1 still says, checked:

> `App.tsx` never reads the query and GETs once

That sentence is **false** on `c621ceba`. The file reads `status=verifying` and polls. The checkbox was left as a historical “live today” under a later “Change” that also got `[x]`. Do not cite K13.1 as a description of this SHA.

---

## 6. When the verifying UI shows vs when the Pay form hides

Render order in `App` is a total order. Later branches are unreachable if an earlier one returns.

| Order | Condition | Pixel | Pay form | Poll |
|------|-----------|-------|----------|------|
| 1 | `error === 'missing'` **or** `!token` | Missing. Copy: “This payment link is not valid. No sign-in.” | Hidden | Poll may still run if token existed and verifying (see §5) |
| 2 | `!pay` | `Loading…` | Hidden | May run |
| 3 | `pay.status === 'paid'` | Paid. Official Receipt, not e-invoice, not membership | Hidden | Effect guard stops |
| 4 | `pay.status === 'expired'` | Expired | Hidden | Effect guard stops |
| 5 | `verifying && pay.status !== 'paid'` | Verifying… “The processor success URL is not paid. Waiting for the webhook.” | **Hidden** | Runs while open |
| 6 | else (open, not verifying) | Checkout form: amount/currency, name, email, Pay | **Shown** | Off |

The verifying branch’s `pay.status !== 'paid'` is redundant with order 3. It would matter only if paid were not already returned. Expired is already returned at order 4, so verifying + expired never paints Verifying; it paints Expired. There is no verifying+paid overlap.

**Hiding the Pay form on verifying is the whole K13 product sentence:** “Buyer returning from Stripe/CHIP/Billplz sees verifying → paid, not the Pay form again.” Live code does that whenever the success URL kept `?status=verifying`.

**Showing the Pay form again** happens when:

- First visit (no query).
- Cancel/failure URL without query (Stripe/CHIP/Xendit).
- Someone strips the query and reloads.
- `verifying` is false even if `pay.status === 'open'` after a successful start that never redirected (missing `redirect_url`).

K14 copy on the form itself remains:

```156:159:apps/lazuar-pay-checkout/src/App.tsx
        {pay.amount} {pay.currency}. Buyers have no One account. Completing
        payment on the processor is not the same as a success URL.
```

Paid copy (015 tax-out honesty; 014 lacked “not an e-invoice”):

```119:127:apps/lazuar-pay-checkout/src/App.tsx
  if (pay.status === 'paid') {
    return (
      <main>
        <h1>Paid</h1>
        <p>
          Thank you. This page is not a membership login. The merchant will see
          an Official Receipt, not an e-invoice.
        </p>
      </main>
    )
  }
```

The buyer still cannot see the `RCPT-` number. That is Bar C / NP-BUY-005, not this slice. The Paid pixel is honest **only** because it keys off host `status === 'paid'`, which `Fulfillment` writes with the document. Query-alone cannot reach this branch.

Loading graveyard (unchanged from 014): GET network failure sets `error` to `Failed to fetch` or similar, which is **not** `'missing'`, `pay` is null → Loading… forever. GET `status 500` same. There is no “Pay is down” pixel.

---

## 7. Email required — host truth vs SPA block

### 7.1 Host `BuyerEmail`

```1:25:apps/lazuar-pay/src/Lazuar.Pay/Gateways/BuyerEmail.cs
namespace Lazuar.Pay.Gateways;

public static class BuyerEmail
{
    public const string Placeholder = "customer@example.com";

    public static bool IsUsable(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && !string.Equals(email.Trim(), Placeholder, StringComparison.OrdinalIgnoreCase);

    public static string NameFrom(string? email, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Trim();
        }
        // ... local-part or "Customer"
    }
}
```

This is the Hub `GatewayCommon.IsUsableBuyerEmail` **decision** (same placeholder, trim, ordinal-ignore-case). It is **not** RFC-5322 validation. `"foo"` is usable. Hub’s `TryResolveEmail` error string was `"Customer email is required."` 015 P20 allowed that **or** “same as missing.” Live Start detail is `"email is required"` (P19), not Hub’s sentence. CHIP/Billplz/Xendit/Razorpay `CreateHostedUrlAsync` also throw `InvalidOperationException("email is required")` if called with an unusable mailbox — defense in depth. That throw **does not contain `"callback base"`**, so if it reached Start’s catch it would become **503**, not 400. In practice Start’s explicit `RequiresEmail` check returns 400 first for the four non-Stripe names, so the rail throw is unreachable on the public door unless `RequiresEmail` and the rail disagree. They do not, today: every non-Stripe allow-listed name requires email, and Stripe’s `StripeHosted` does not call `BuyerEmail`.

### 7.2 When GET says `email_required: true`

`row.Provider ?? settings.ActiveProvider` then `TryNormalize` && `RequiresEmail`.

| Org / checkout | GET `email_required` | Pay button |
|----------------|----------------------|------------|
| No `ActiveProvider`, never started | `false` | Enabled with empty email. Start → 503 `rail not configured` (no provider). |
| `ActiveProvider=stripe`, never started | `false` | Enabled. Stripe hosted session, email optional. |
| `ActiveProvider=chip` (or billplz/xendit/razorpay) | `true` | Disabled until `email.trim()` non-empty. |
| Started on chip (`row.Provider=chip`) even if merchant later PUT stripe | `true` | Start uses **checkout.Provider** first (P17 retry-start). Email still required. Rail will not switch mid-flight. |
| Started on stripe, merchant later PUT chip | `false` on GET (provider already stripe on the row) | Start stays Stripe. Email stays optional. |

PUT gateway always sets `ActiveProvider` to the pasted rail. Checkout create does **not** stamp `Provider` (P18.2). First start freezes the rail on the row.

### 7.3 SPA `emailBlocked` vs K11 “and not placeholder”

```150:150:apps/lazuar-pay-checkout/src/App.tsx
  const emailBlocked = Boolean(pay.email_required && !email.trim())
```

and the click guard:

```73:76:apps/lazuar-pay-checkout/src/App.tsx
    if (pay?.email_required && !email.trim()) {
      setError('email is required')
      return
    }
```

K11.1, checked:

> If email required, disable Pay until email non-empty **(and not placeholder)**

Live: **non-empty only.** `customer@example.com` enables Pay. Click goes to HTTP. Host `IsUsable` fails → **400** `"email is required"`. SPA maps **every** 400 to `'callback base not public or email required'` (§9). The buyer who typed Hub’s placeholder is told a Billplz callback-base story.

There is no lock test for `customer@example.com`. There is no `type="email"` / `required` HTML. Inputs have no `autocomplete`. Name is never required; CHIP/Billplz/Razorpay derive `NameFrom` (local-part or `"Customer"`). No TIN field (K11).

### 7.4 Prefill hole after hop-2

GET returns `payer_email` once Start has persisted it. The SPA does not put that into `email` state. After Stripe/CHIP **cancel** (form shown again), the box is empty. If `email_required`:

- Button disabled (`emailBlocked`).
- Host would have accepted a blank body email because the row already has a usable mailbox.
- Buyer must retype.

Honesty: the UI is stricter than the host on retry, and looser than the host on placeholder. K11’s goal (“do not 400 after the buyer clicked Pay with an empty email if the UI could have blocked”) **holds for empty**. It **fails for placeholder**. It **over-blocks** cancel retry.

### 7.5 Tests that exist vs the SPA

`RailTests.Chip_start_without_email_is_400` POSTs `{"name":"Ada"}` with chip keys, expects 400. Does not assert `detail`. Does not cover placeholder. Does not cover Billplz/Xendit/Razorpay missing email (those starts in the same file send `ada@acme.test`). P20 “Hermetic 400” for placeholder is **not** in `Lazuar.Pay.Tests` as a named case (grep `customer@example` under `apps/lazuar-pay/tests` is empty). C30/B26/X22/R24 checkboxes claim placeholder 400; host code would 400; tests do not lock it. Frontend tests do not mount `App`.

---

## 8. No OIDC, no wallet tiles, no PAN, no provider picker

### 8.1 K15 / locks — no Zitadel on 5179

`locks.test.ts`:

```8:20:apps/lazuar-pay-checkout/src/locks.test.ts
describe('checkout honesty', () => {
  it('has no OIDC dependency', () => {
    const pkg = readFileSync(join(root, 'package.json'), 'utf8')
    expect(pkg).not.toContain('oidc-client-ts')
    expect(pkg).not.toContain('react-oidc-context')
    expect(pkg).not.toContain('@repo/api-types-ts')
  })

  it('does not render wallet tiles or card PAN', () => {
    const src = readFileSync(join(root, 'src', 'App.tsx'), 'utf8')
    expect(src.toLowerCase()).not.toMatch(/grabpay|tng|touchngo|boost|duitnow|fpx|shopee/)
    expect(src).not.toContain('autocomplete="cc-number"')
  })
})
```

Vitest environment is **node**, not jsdom. These are file greps, not rendered trees. They are the right shape for “do not add a dependency / do not paste a tile string.”

App.tsx grep on this SHA: no `oidc`, no `zitadel`, no `5175`, no `Bearer`, no `Authorization`. Fetch calls are two public URLs. Missing copy says “No sign-in.” Form copy says “Buyers have no One account.” Merchant SPA (`react-oidc-context`) is a **different** package.

K15.1 “Fail the program if login `:5175` appears on checkout” is **not** encoded in `locks.test.ts`. A future `<a href="http://localhost:5175">` would not fail the existing test. The lock is package-name-only.

### 8.2 K12 / K17 / X20 — no tiles, no PAN

`App.tsx` inputs: Name, Email, button “Pay”. No `type="password"`, no `autocomplete="cc-number"` / `cc-csc` / `cc-exp`. No Stripe Element. No iframe. No GrabPay/TnG/Boost/DuitNow/FPX/Shopee strings. CSS has no logo wall. X20’s “wallets appear on Xendit’s invoice page” is merchant wrap copy (`WorkspacePage.tsx` `copy.xendit`), not a checkout pixel. Good.

The wallet regex uses `tng` as a substring. Current `App.tsx` would fail the test if someone wrote “starting” in copy (`tng` inside `starting`). That is a brittle lock, not a current false positive (the word is not in the file).

K17 “Name + email only (plus Pay button)” is live.

### 8.3 K10 — buyer does not pick a PSP

No `<select>`. Start body is `{ name, email }` only. GET does not return `provider`. Amount/currency are shown; the rail name is not. Merchant picker is on `:5178`. Start dispatch is host-side `row.Provider ?? ActiveProvider`. Matches 015 §6.2 “No provider picker.”

K10.1 “Buyer copy may say you will continue on the processor’s page without naming five logos” — live copy says “processor” and “success URL,” not five names. Good.

---

## 9. Error strings vs host statuses (callback base 400 vs 503)

This is the mapping the SPA actually implements. Host `detail` is discarded.

```84:94:apps/lazuar-pay-checkout/src/App.tsx
      if (response.status === 503) {
        setError('rail not configured')
        return
      }
      if (response.status === 400) {
        setError('callback base not public or email required')
        return
      }
      if (!response.ok) {
        setError(`start ${response.status}`)
        return
      }
```

`PayErrors.Status` always sends JSON `{ status, title, detail }`. Example: 400 `{ "status": 400, "title": "Bad Request", "detail": "email is required" }`. The SPA does not parse it.

### 9.1 Host Start status table (live)

| Condition | HTTP | `detail` | SPA alert |
|-----------|------|----------|-----------|
| Unknown public token | 404 | Checkout not found | `start 404` |
| `status` is `paid` or `expired` | 409 | Checkout is not open | `start 409` |
| `OrgSettings.ChargesPaused` | 403 | Org charges are paused | `start 403` |
| Provider missing / not in allow-list | 503 | rail not configured | `rail not configured` |
| Non-Stripe rail and `!BuyerEmail.IsUsable(row.PayerEmail)` (blank, whitespace, or `customer@example.com`) | 400 | email is required | **`callback base not public or email required`** |
| `InvalidOperationException` whose message **contains** `"callback base"` (ordinal) | 400 | exception message (`callback base not public`) | **`callback base not public or email required`** |
| Any other `InvalidOperationException` | 503 | exception message | **`rail not configured`** (detail thrown away) |
| `Stripe.StripeException` | 503 | Stripe rejected the org key | `rail not configured` |
| Success | 200 | `{ redirect_url }` | navigate away |

### 9.2 What actually throws `InvalidOperationException` on hosted create

| Throw | Rail | Contains `"callback base"`? | HTTP | SPA string |
|-------|------|------------------------------|------|------------|
| `rail not configured` (no cred row; CHIP/Billplz missing `PublicMerchantId`; Razorpay secret not `key_id:key_secret`) | all | no | 503 | `rail not configured` — **matches detail** |
| `email is required` | chip/billplz/xendit/razorpay | no | would be 503 **if catch reached**; preempted by 400 | n/a |
| `callback base not public` | **Billplz only** (`TryPublicBase`) | **yes** | **400** | mashed 400 string — **matches the Billplz half** |
| `CHIP rejected the org key` / `CHIP returned no URL` | chip | no | 503 | `rail not configured` — **lies** |
| `Billplz rejected the org key` / `Billplz returned no URL` | billplz | no | 503 | `rail not configured` — **lies** |
| `Xendit rejected the org key` / `Xendit returned no URL` | xendit | no | 503 | `rail not configured` — **lies** |
| `Razorpay rejected the org key` / `Razorpay returned no URL` | razorpay | no | 503 | `rail not configured` — **lies** |
| `Currency is required.` (`MoneyMath.TryNormalizeCurrency` fail) | xendit, razorpay | no | 503 | `rail not configured` — **lies** |
| `Stripe returned no URL` | stripe | no | 503 | `rail not configured` — **lies** |

Billplz `TryPublicBase` fails closed when `Pay:PublicBaseUrl` is missing, not absolute, not `https`, or host is loopback / `localhost` / `127.0.0.1` / `::1` / contains `lazuar-local-dev.com`. Message is always `"callback base not public"`. `appsettings.json` does **not** set `Pay:PublicBaseUrl`. Local dogfood of Billplz start without an env override is **400**, not a Billplz HTTP call. That is B15.

`PayApiFactory` sets `Pay:PublicBaseUrl=https://pay.test.example`, so hermetic tests **never** exercise the 400 callback-base path. `RailTests.Billplz_paid_form_and_localhost_blocked` **succeeds** at start (sandbox host assertion) and never asserts 400. The method name is leftover. K16.1 “Billplz localhost may be 400 — map that too if B15 uses 400” is implemented **in the SPA as a blanket 400 map**, not as a `detail` parse.

### 9.3 Catch predicate is a substring, not a code

```113:114:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
            var status = ex.Message.Contains("callback base", StringComparison.Ordinal) ? 400 : 503;
            return PayErrors.Status(status, status == 400 ? "Bad Request" : "Service Unavailable", ex.Message);
```

Only Billplz currently emits that substring. A future throw “callback base timeout” would also be 400. `"Callback Base"` would be 503 (ordinal, case-sensitive). Hub’s old code was `CALLBACK_BASE_NOT_PUBLIC`; new Pay does not send that token. SPA does not look at `detail` anyway.

### 9.4 K16 vs live honesty

K16 goal: “Keep 503 mapping for missing CHIP Brand ID / Billplz localhost / Stripe bad key.”

Split:

- **Missing CHIP Brand ID** (`PublicMerchantId` empty) → `rail not configured` throw → **503** → SPA `rail not configured`. Honest.
- **Billplz localhost** → **400** `callback base not public` → SPA mashed string. Honest enough **if** the 400 was that. Same mashed string for **email 400**.
- **Stripe bad key** → `StripeException` → 503 `"Stripe rejected the org key"` → SPA `rail not configured`. Fail-closed, **slightly dishonest** (014 already said this). Same for CHIP/Billplz/Xendit/Razorpay “rejected the org key.”

K16.1 “Start 503 shows a human sentence (existing `rail not configured`)” — yes, one sentence for every 503. “Do not spin forever” — start 503 does not spin; the button re-enables (`busy` false) and the alert sits on the form. Verifying-poll spin is a different path (no start).

**403 on the public door** is charges-paused, not “please log in.” SPA shows `start 403`. A buyer could read that as auth. The missing pixel is the only place that says “No sign-in.” K15 wanted no 401/403 that imply login; Start 403 is not login, but the pixel does not say “merchant paused charges.”

**409** `start 409`: paid/expired. If the buyer still has the form (no verifying query) and the webhook paid in another tab, Pay click becomes 409. Paid pixel would have shown if they had refreshed GET. No auto-refresh on the form except the verifying poll.

Client-only `'email is required'` (empty box, `email_required`) is the one case where SPA detail matches host P19 **without** HTTP. Placeholder 400 does **not** use that string.

### 9.5 Why the mashed 400 string exists

015 K16 + B15: map Billplz localhost 400. 015 K11 + P19: email 400. One `if (response.status === 400)` cannot distinguish them without reading `detail`. The implementer concatenated both stories. Consequence: every email 400 looks like a callback-base outage, and every callback-base 400 looks like a missing email. A CHIP start 400 is **only** email on this SHA (CHIP does not call `TryPublicBase`). Showing “callback base not public or email required” on CHIP is a **false alternative**.

The cheap fix (not done; this paper does not implement) is `await response.json()` and display `detail`. Host details are already English and buyer-safe (`email is required`, `callback base not public`).

---

## 10. Success / cancel URLs — five rails, one query convention

Stripe:

```30:31:apps/lazuar-pay/src/Lazuar.Pay/Gateways/StripeHosted.cs
            SuccessUrl = checkout.SuccessUrl ?? "http://localhost:5179/c/" + checkout.PublicToken + "?status=verifying",
            CancelUrl = checkout.CancelUrl ?? "http://localhost:5179/c/" + checkout.PublicToken,
```

CHIP:

```51:53:apps/lazuar-pay/src/Lazuar.Pay/Gateways/ChipHosted.cs
            ["success_redirect"] = checkout.SuccessUrl ?? "http://localhost:5179/c/" + checkout.PublicToken + "?status=verifying",
            ["failure_redirect"] = checkout.CancelUrl ?? "http://localhost:5179/c/" + checkout.PublicToken,
            ["cancel_redirect"] = checkout.CancelUrl ?? "http://localhost:5179/c/" + checkout.PublicToken
```

Billplz:

```47:47:apps/lazuar-pay/src/Lazuar.Pay/Gateways/BillplzHosted.cs
            ["redirect_url"] = checkout.SuccessUrl ?? "http://localhost:5179/c/" + checkout.PublicToken + "?status=verifying",
```

Xendit:

```44:45:apps/lazuar-pay/src/Lazuar.Pay/Gateways/XenditHosted.cs
            ["success_redirect_url"] = checkout.SuccessUrl ?? "http://localhost:5179/c/" + checkout.PublicToken + "?status=verifying",
            ["failure_redirect_url"] = checkout.CancelUrl ?? "http://localhost:5179/c/" + checkout.PublicToken,
```

Razorpay:

```53:54:apps/lazuar-pay/src/Lazuar.Pay/Gateways/RazorpayHosted.cs
            ["callback_url"] = checkout.SuccessUrl ?? "http://localhost:5179/c/" + checkout.PublicToken + "?status=verifying",
            ["callback_method"] = "get"
```

Merchant create body from `:5178` is `{ org_id, amount, currency: 'MYR' }` — **no** `success_url` / `cancel_url`. All five defaults apply. They are **localhost:5179**, not `window.location.origin`. A checkout SPA served from another host still sends the buyer back to 5179 after pay. Fine for the 015 dogfood loop; not a portable success URL.

K14: query is not paid. Live verifying copy states the law in the pixel. Hosted rails do not fulfill on redirect. `Fulfillment` is webhook-only.

Stripe `mode = "payment"` only (not setup). Amount 0 is refused at checkout create (`amount must be greater than 0`) and `Fulfillment` no-ops `Amount <= 0`. The verifying pixel cannot mint a false paid from a setup session on this SHA because Start never creates one.

---

## 11. 015 K10–K17 checklists versus live (this SHA)

All eight files are fully `[x]` including Exit. A99.2 “Buyer page has no PSP dropdown” is `[x]`. A99.1 lived loop (human `:5179/c/{token}` with no One account) is still `[ ]`.

| ID | Checklist claim | Live `c621ceba` | Verdict |
|----|-----------------|-----------------|---------|
| **K10** | No provider `<select>`; start POST has no `provider` | True. Body `{ name, email }`. | **Holds.** |
| **K11** | GET may return `email_required`; disable Pay until email non-empty **and not placeholder**; Stripe optional; no TIN | GET returns the flag. Disable on **trim-empty only**. Stripe optional. No TIN. Placeholder is **not** blocked in UI. | **Partial.** Empty-email 400 after click is prevented. Placeholder 400 after click is **not**. Checkbox over-claims. |
| **K12** | Grep grab/tng/touch/boost/duitnow/fpx/shopee none as buttons; locks may forbid those strings | True in `App.tsx` + `locks.test.ts`. | **Holds.** File grep, not a rendered test. |
| **K13.1** | “`App.tsx` never reads the query and GETs once” | False. Reads query; polls. | **Stale text left checked.** |
| **K13.2** | Poll ~2s, cap ~30s; states loading/open/verifying/paid/expired/missing/error; stop on paid/expired/missing | Interval 2s, 15 ticks, ~30s. States exist as pixels except **error** (Loading graveyard) and **expired** (no writer). Missing does not stop poll. After cap, Verifying stuck. | **Mostly holds; stop-on-missing and error pixel do not.** |
| **K13.3** | Do not treat query as paid; no OIDC on poll | True. | **Holds.** |
| **K14** | `status=verifying` → verifying UI, not paid; paid only from GET `status=paid`; keep processor ≠ success URL copy | True. Render order + copy. | **Holds.** |
| **K15** | Keep locks forbidding OIDC packages; no Bearer on GET/start; fail if `:5175` on checkout | Locks exist for packages. No Bearer. `:5175` is **not** grepped. | **Holds on packages/Bearer. `:5175` lock is a comment, not a test.** |
| **K16** | 503 → human `rail not configured`; do not spin; map Billplz 400 if B15 uses 400 | 503 map yes. 400 **blanket** mashup. Start does not spin. Poll/Verifying can stick. | **Holds as a 503 sentence. 400 mashup is coarser than B15/P19. Verifying stick is the remaining spin.** |
| **K17** | No `autocomplete="cc-number"` / cvc; no Stripe.js; name+email+Pay | True. | **Holds.** |

P-track collisions that leak into this SPA:

| ID | Claim | Live | SPA effect |
|----|-------|------|------------|
| P17.1 | Start always Stripe | False (five-way switch) | Buyer cannot tell. |
| P17.2 | Unknown provider → 400 | Live **503** `rail not configured` | SPA 503 path. Better for K16 than a 400 mashup. |
| P19 | Missing email → 400 `"email is required"` | True for non-Stripe | SPA shows mashed 400 unless client empty-guard fired. |
| P20 | Placeholder → 400; “or same as missing” | Host 400 `"email is required"` (same as missing). **No hermetic test.** | SPA mashed 400; UI does not disable. |
| P24.2 | Keep 503 on public start; webhook 400 | True | SPA 503 map. |
| B15 | Localhost callback 400/503 `"callback base not public"` | **400**, Billplz only | SPA mashed 400. Hermetic test does not hit it (`PublicBaseUrl` overridden). |
| C30/B26/X22/R24 | Start email 400 | Host yes. Test only Chip missing-email. | UI email_required for all four. |

---

## 12. CORS, ports, dist, spec — adjacent facts the SPA depends on

`Program.cs` default CORS: 5178 and 5179, localhost and 127.0.0.1, any header, any method. **Not** 4179 preview. **Not** 3003 ops. **Not** 3004 portal. `CorsTests` lock 5178/5179 allow and 3003/3004 deny on `/health`. Checkout GET/start are the same policy (default policy, `UseCors()`).

Vite history fallback: `/c/{token}` in `pnpm dev` serves `index.html`. There is no React Router; `tokenFromPath` reads the real pathname. Static `dist/` hosting without a fallback would 404 the path; `dist/index.html` is a module bundle, not a server. This program dogfoods `task pay:checkout` (Vite dev), not nginx.

`dist/assets/index-CGhBg7uT.js` does **not** contain `rail not configured`, `email_required`, `Verifying`, or “not an e-invoice.” Those strings live only in `src/App.tsx` on this SHA. The checked-in dist is **stale** relative to the 015 checkout edits. Anyone running `vite preview` from an old dist without rebuild is not running K13/K16. `task pay:checkout` uses source via Vite, so dogfood is the live `App.tsx`.

`packages/pay-spec/dist/openapi.yaml` `PublicPay` schema still lacks `email_required`; file header still says “Checkout is a fixture (open session), not a charge.” `main.tsp` was updated (Q12.1). Dist was not regenerated or not committed. SPA does not import it.

`PublicPayApi.start` in the spec has **no request body**. Live body `{ name, email }` is unspecified. Frontend and host agree with each other, not with OpenAPI.

---

## 13. Host GET/Start fields the SPA ignores

GET JSON includes `payer_name`, `payer_email`. SPA ignores both (no prefill, type omits them).

GET does not include `provider`, `org_id`, `id` (checkout id), `psp_redirect_url`. The SPA cannot deep-link to an already minted hosted URL without calling start again. P18 “prefer mint new” matches that.

Start 200 `{ redirect_url }` is the only success field. `ProviderSessionId` stays on the row.

Expired: Start 409 if status expired; GET would return `status: "expired"`; SPA has a pixel; **no writer** sets it. Dead branch.

Amount display is `{pay.amount} {pay.currency}` with no money formatting (10 vs 10.00). Create uses decimal. Fine for MYR dogfood.

---

## 14. What 014 said that 015 changed on this SPA

014 `App.tsx` (as quoted in 014/03): GET once; no poll; no `email_required`; 503 map only; non-503 non-OK → `start ${status}`; email optional; paid copy without “e-invoice”; no Verifying pixel. Returning from Stripe with `?status=verifying` while still `open` **re-showed the Pay form**.

015 changed, live on `c621ceba`:

1. `verifyingQuery` + `verifying` state.
2. Poll effect 2s × 15.
3. Verifying pixel; form hidden.
4. `PayView.email_required`.
5. Empty-email client guard + `emailBlocked`.
6. 400 → mashed callback-base/email string.
7. Paid copy “not an e-invoice.”

015 did **not** change: Loading graveyard; ignore `detail`; all 503 → one sentence; no placeholder UI lock; no Bearer; no tiles; no OIDC; no provider picker; no PAN; `window.location.assign` of server URL; missing pixel “No sign-in.”

---

## 15. Tests that lock this slice (and do not)

**Checkout package**

- `locks.test.ts` only. Two greps. `pnpm --filter lazuar-pay-checkout test` does not render `App`, does not fake `fetch`, does not assert poll timing, does not assert 503/400 strings, does not assert `emailBlocked`.
- No `@testing-library/react`. Vitest node env cannot mount the component without extra config.

**Host**

- `PublicPayTests.Public_get_does_not_need_bearer` — GET twice, One not called on the public path. **Does not assert** `email_required` or snake_case field set.
- `PublicPayTests.Public_missing_is_404`.
- `PublicPayTests.Empty_webhook_is_400` — not a public-pay test; webhook route.
- `RailTests.Chip_start_without_email_is_400` — 400 status only.
- Chip/Xendit/Razorpay/Billplz starts **with** email expect 200 under mocked PSP HTTP.
- **No** `Public_get_email_required_true_for_chip`.
- **No** start 503 missing keys assertion on the public door.
- **No** start 400 `callback base not public` (factory injects a public https base).
- **No** placeholder email 400.
- `CorsTests` 5179 on `/health`.

The SPA mapping table in §9 is therefore **untested as a unit**. A regression that maps 400 to `start 400` again would not fail `locks.test.ts` or `Chip_start_without_email_is_400`.

---

## 16. Ranked mismatches (checkout ↔ host only)

These are judgment for `10-honesty-frontend-risks.md`, not work items.

1. **400 mashup.** Host distinguishes `email is required` (P19) from `callback base not public` (B15) as two details, both 400. SPA prints one sentence that is false for CHIP/Xendit/Razorpay email 400 (no callback base involved) and noisy for Billplz (buyer did not forget email). Root cause: status-only map, `detail` ignored.
2. **K11 placeholder.** Checklist required disable until not placeholder. UI allows `customer@example.com`. Host 400. Then mismatch (1) fires.
3. **All 503 → `rail not configured`.** Host detail may be `Stripe rejected the org key`, `CHIP rejected the org key`, `Currency is required.`, `CHIP returned no URL`. Buyer-facing lie is fail-closed. Same as 014; now four extra rails feed the same bucket.
4. **Verifying pixel after poll cap.** 30s of GET then stuck “Waiting for the webhook” with Pay hidden. K16 “do not spin forever” is only half-done. Late Billplz webhook (tunnel down) is the realistic stuck path.
5. **Loading graveyard** for non-404 GET failure. Unchanged.
6. **No prefill** of `payer_email` after cancel. Email-required rails disable Pay even though the row is already usable.
7. **K13.1 / P17.1 stale checked text.** Maps that still describe GET-once / Stripe-only Start. Live code moved. Parent 016 must not quote those lines as evidence.
8. **Expired pixel + Start 409 expired** with no writer of `expired`. Dead honesty, not a buyer bug today.
9. **`:5175` not grepped** in checkout locks. OIDC packages are.
10. **Preview 4179 not in CORS.** Easy to mistake for a Pay outage (Loading…).
11. **Checked-in `dist/` stale.** Source is authority for `task pay:checkout`.
12. **`pay-spec` dist OpenAPI** missing `email_required`; start body unspecified.

What holds and should not be bargained:

- No OIDC dependency, no Bearer on GET/start, missing pixel says “No sign-in.”
- No wallet tiles, no PAN, no provider picker.
- Query `status=verifying` is not paid; Paid pixel is host `status`.
- Poll exists; form hides on verifying; success URLs on all five rails append the query.
- GET `email_required` tracks `RequiresEmail` (not Stripe) from `Provider ?? ActiveProvider`.
- Empty email cannot click Pay when the flag is true.
- Start 503 still shows a human sentence; missing Brand ID / missing keys stay 503.
- Billplz non-public base is 400 on the host, not a fiction DNS rewrite.
- Redirect is `location.assign` of a **server-minted** URL. PAN stays on the processor.

---

## 17. Files this paper did not reopen as authority

Hub adapter HTTP, webhook HMAC/RSA, merchant PUT field sets, IsolationTests, SST strip — sibling 016 files. `QuoteView.tsx` on portal is Hub commerce, not this origin. `TODO.md` still mentioning Hub `422 CALLBACK_BASE_NOT_PUBLIC` is historical Hub copy; live Pay detail is `callback base not public` at 400.

---

## 18. One-paragraph verdict for the parent file

On `feat/015-four-adapters` (`c621ceba`), `:5179` is still a public, no-OIDC, no-tile, no-PAN cash-register pixel that calls only `GET/POST /v1/pay/{token}`. 015 landed the verifying poll (2s, 15 ticks, form hidden, query not paid) and `email_required` from `PayProviders.RequiresEmail`, and it mapped start **503 → `rail not configured`** and **400 → `callback base not public or email required`**. The 503 bucket matches K16’s missing-rail sentence and swallows every other hosted `InvalidOperationException` / `StripeException`. The 400 bucket is the honest Billplz localhost path **and** the P19 email path glued together, so a CHIP buyer with a blank-or-placeholder mailbox is told a callback-base story the rail does not have. K11’s placeholder disable is not live; K13.1’s “never reads the query” is stale; poll does not stop on missing and does not leave Verifying after 30s. Locks tests cover packages and forbidden tile/PAN strings only. Live code, not the checked K boxes, is the evidence.
