# 03 — Checkout Vite (:5179) after aura-ui chrome

**Date:** 26 August 2026  
**Program:** [019-evals](./README.md) — evaluate newest Lazuar Pay after 018 merchant-shell / aura-ui work  
**Slice:** current state of `apps/lazuar-pay-checkout` (Vite **`:5179`**) versus live focused-Pay public doors. Hosted buyer pay page. Buyers **MUST NOT** need a One / Zitadel account. Occupancy, Test rail, verifying poll, success≠paid, and the 018 restyle are in scope.  
**Kind:** uncondensed evaluation. **Not** an implementation. **Not** a product-code change. **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md) Status cells.  
**Audience:** the parent 019 judgment (`00-evaluation.md`) and anyone about to treat the Card chrome as equivalent to a production cash register.

Live files on this SHA are authority. [014-evals/03-checkout-frontend.md](../014-evals/03-checkout-frontend.md) described a Stripe-only GET-once SPA. [016-adapters-check/03-checkout-frontend.md](../016-adapters-check/03-checkout-frontend.md) described the four-adapter SPA with a mashed 400 string, no `slot_key`, no Card chrome, and a stuck verifying pixel. Both are background. If they disagree with live files, live files win.

This paper is about **the buyer plane in the browser** plus the **public** 8081 doors it actually calls (`GET /v1/pay/{token}`, `POST /v1/pay/{token}/start`) and the occupancy / Test / success-URL behaviour those doors expose. It does not re-derive PSP webhook crypto, journal balance, `RCPT-` numbering, BYOK encryption, or merchant OIDC. Those live in sibling 019 reports. It does pin the **fail locks** those papers must not violate from this origin: no Zitadel on checkout, no Pay password form, wrap-rails honesty, success/cancel URLs are not fulfillment, never treat the query string as paid, never render wallet / FPX tiles, never collect PAN.

---

## Coordinates

| | |
|---|---|
| Repo | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` |
| Branch | `feat/018-merchant-shell` |
| HEAD (task pin) | `9f04ad58` — `fix(pay-ui): match receipts table to pay-link chrome` |
| Date | 26 August 2026 |
| Checkout origin | `apps/lazuar-pay-checkout` → **http://localhost:5179** (`strictPort`) |
| Preview origin | same package, **http://localhost:4179** (`strictPort`) |
| Pay host | `apps/lazuar-pay` (`Lazuar.Pay`) → **http://localhost:8081** |
| Postgres | **5435**, database `lazuar_pay` |
| Merchant origin (contrast only) | `apps/lazuar-pay-merchant` → **http://localhost:5178** (OIDC). Mints `http://localhost:5179/c/{public_token}`. Not this SPA. |
| Old Hub portal | `apps/lazuar-portal` **:3004**. Do not retarget. |
| Identity | One API **8080**, product login **:5175**. Checkout must not call those. |
| Vite | `^8.2.0`, React `^19.2.8`, Tailwind 4, copied aura-ui primitives under `src/ui/` |
| pnpm | workspace `apps/*`; CI uses `pnpm@11.5.2` |

If a sentence does not say **focused Pay** vs **old Hub** vs **One**, assume it is wrong.

**What “Pay” means here**

- The **new focused host** is `apps/lazuar-pay` on **http://localhost:8081**. Public buyer doors: `GET /v1/pay/{token}`, `POST /v1/pay/{token}/start`. Merchant doors stay member-gated. Six hosted rails including local **Test**. Payment-link occupancy. Plane B fulfillment writes `paid` + Official Receipt.
- The **new buyer origin** is `apps/lazuar-pay-checkout` on **http://localhost:5179**. Path convention `/c/{token}`. No router package. No OIDC. After 018: Card chrome copied from aura-ui, `slot_key` occupancy, Test copy, verifying timeout footer, `readDetail` of host `{ detail }`.
- The **shareable URL** the merchant SPA copies is `{(VITE_CHECKOUT_ORIGIN ?? http://localhost:5179)}/c/{public_token}` with **no** query. Hosted success defaults add `?status=verifying`. Cancel defaults omit the query.

**Locked (do not bargain from this origin)**

| Lock | Meaning for `:5179` |
|------|---------------------|
| Buyers are not One/Zitadel humans | Fail if this page asks for Zitadel / `:5175` login or sends `Authorization`. |
| No Pay password form | No `/login`, no email+password against One. |
| Hosted cash register | Buyer page on Pay’s origin, not Hub `:3004`. |
| Shareable pay link | `http://localhost:5179/c/{token}` (or `VITE_CHECKOUT_ORIGIN` equivalent). |
| Success/cancel URLs are not fulfillment | Query `status=verifying` is not paid. Webhook / Test `FulfillPaidAsync` writes `RCPT-`. |
| Wrap-rails pixel | Start a hosted PSP session. Do not collect PAN. Do not draw GrabPay / TnG / FPX tiles. |
| One rail per link | Buyer does not pick a PSP. Merchant binds `provider` at mint. |
| Email when the rail needs it | CHIP / Billplz / Xendit / Razorpay refuse blank and `customer@example.com`. Stripe and Test may stay optional. |
| Test rail | No secrets. Local complete. Not a wallet tile. |
| Occupancy | A shared URL is not unlimited unless minted that way. `slot_key` is one browser ≈ one seat. |

---

## Files opened

Nothing was implemented. The following were opened in full or in the cited ranges. Live files first.

### Checkout SPA (entire package that is source)

| Path | Why |
|------|-----|
| `apps/lazuar-pay-checkout/package.json` | Port 5179, preview 4179, React 19, **new** radix/cva/lucide/tailwind-merge runtime deps, `vitest run`, no OIDC. |
| `apps/lazuar-pay-checkout/README.md` | “Buyers have **no** One account. Fail if this page asks for Zitadel login.” |
| `apps/lazuar-pay-checkout/vite.config.ts` | Dual-pin 5179 `strictPort`; preview 4179; Tailwind plugin. |
| `apps/lazuar-pay-checkout/vitest.config.ts` | Node env; `src/**/*.test.ts`. No jsdom. |
| `apps/lazuar-pay-checkout/index.html` | `lang="en"`, title “Lazuar Pay — checkout”, viewport, `#root`. |
| `apps/lazuar-pay-checkout/.env.example` | `VITE_PAY_API_URL=http://localhost:8081` only. |
| `apps/lazuar-pay-checkout/tsconfig.json` | Project references only. |
| `apps/lazuar-pay-checkout/tsconfig.app.json` | Bundler mode; `include: ["src"]`; `noUnusedLocals`. |
| `apps/lazuar-pay-checkout/tsconfig.node.json` | `include: ["vite.config.ts"]` only (not `vitest.config.ts`). |
| `apps/lazuar-pay-checkout/src/main.tsx` | StrictMode mount. No router. |
| `apps/lazuar-pay-checkout/src/App.tsx` | **Entire runtime:** path token, slot_key, GET, poll, start, pixels. |
| `apps/lazuar-pay-checkout/src/index.css` | Tailwind 4 tokens; `body` `min-h-dvh bg-slate-100/80`. |
| `apps/lazuar-pay-checkout/src/locks.test.ts` | Nine filesystem greps. |
| `apps/lazuar-pay-checkout/src/ui/components/button.tsx` | Copied aura-ui Button (cva + Slot). |
| `apps/lazuar-pay-checkout/src/ui/components/card.tsx` | Copied Card / Header / Title / Description / Content / Footer. `CardTitle` is a **div**. |
| `apps/lazuar-pay-checkout/src/ui/components/input.tsx` | Copied Input; `md:text-sm` / mobile `text-base`. |
| `apps/lazuar-pay-checkout/src/ui/components/label.tsx` | Copied Label. |
| `apps/lazuar-pay-checkout/src/ui/lib/utils.ts` | `cn` = `twMerge(clsx)`. |
| `apps/lazuar-pay-checkout/public/favicon.svg` | Same purple mark as merchant. |
| `apps/lazuar-pay-checkout/dist/index.html` | Built asset names. |
| `apps/lazuar-pay-checkout/dist/assets/` | Grep for `slot_key` / `Payment received` / `Link is full` / `verifyTimedOut` → **no matches**. Dist is stale vs `src/App.tsx`. |

**Not present** in this package (opened as absence): `Dockerfile`, `components.json`, `src/App.css` (014/016 leftover, deleted by restyle), `eslint.config.*`, `react-router-dom`, `oidc-client-ts`, `react-oidc-context`, `@repo/api-types-ts`, `@repo/aura-ui`, `@stripe/stripe-js`.

### Host public doors, occupancy, Test, URLs

| Path | Why |
|------|-----|
| `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` | CORS allow-list; snake_case JSON; `MapPublicPay`. |
| `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` | Entire GET + Start + `MintOrResume` + views + `StartPayRequest`. |
| `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/BuyerEmail.cs` | Placeholder `customer@example.com`; `IsUsable`; `NameFrom`. |
| `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/CheckoutUrls.cs` | `Pay:CheckoutBaseUrl`; success adds `?status=verifying`; cancel does not. |
| `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkOccupancy.cs` | `open`/`paid` count; `IsFull`; `Remaining`. |
| `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs` | Merchant mint of shared URL; `max_payers` default 1; `unlimited` → null max. |
| `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/CreatePaymentLinkRequest.cs` | `MaxPayers`, `Unlimited`. No `success_url` on the link itself. |
| `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs` | Merchant/kernel `POST /v1/checkouts` still accepts `SuccessUrl` / `CancelUrl`. |
| `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CreateCheckoutRequest.cs` | Optional success/cancel. |
| `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutSession.cs` | Session DTO including `PayerName` / `PayerEmail` / `SlotKey`. |
| `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutStore.cs` | Postgres create + public-token lookup. |
| `apps/lazuar-pay/src/Lazuar.Pay/Hosting/PayErrors.cs` | `{ status, title, detail }`. |
| `apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs` | `RequiresEmail` = not Stripe **and not Test**. |
| `apps/lazuar-pay/src/Lazuar.Pay/Rails/IHostedRail.cs` | `CreateHostedUrlAsync` only. |
| `apps/lazuar-pay/src/Lazuar.Pay/Rails/HostedSession.cs` | `(RedirectUrl, ProviderSessionId)`. |
| `apps/lazuar-pay/src/Lazuar.Pay/Rails/Stripe/StripeHosted.cs` | `CheckoutUrls.Success` / `Cancel`; `mode=payment`. |
| `apps/lazuar-pay/src/Lazuar.Pay/Rails/Chip/ChipHosted.cs` | `success_redirect` / `failure_redirect` / `cancel_redirect`; email throw. |
| `apps/lazuar-pay/src/Lazuar.Pay/Rails/Billplz/BillplzHosted.cs` | `redirect_url` = success only; `callback base not public`. |
| `apps/lazuar-pay/src/Lazuar.Pay/Rails/Xendit/XenditHosted.cs` | success + failure redirect. |
| `apps/lazuar-pay/src/Lazuar.Pay/Rails/Razorpay/RazorpayHosted.cs` | `callback_url` = success only; `callback_method=get`. |
| `apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestHosted.cs` | Returns `CheckoutUrls.Success`; no keys; `AllowsTest`. |
| `apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestWebhook.cs` | Buyer-invisible parse; recorded because Test also has a webhook door. |
| `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs` | Only writer of `status = "paid"`. Never `"expired"`, never `"failed"`. |
| `apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs` | `CheckoutRow`, `PaymentLinkRow`; `ActiveProvider` marked unused. |
| `apps/lazuar-pay/src/Lazuar.Pay/appsettings.json` | No `Pay:CheckoutBaseUrl` default (prod must set). |
| `apps/lazuar-pay/src/Lazuar.Pay/appsettings.Development.json` | `Pay:CheckoutBaseUrl` = `http://localhost:5179`. |
| `apps/lazuar-pay/.env.example` | `Pay__CheckoutBaseUrl=http://localhost:5179`; PublicBaseUrl separate. |
| `apps/lazuar-pay/README.md` | “Success URL is not paid; `:5179` polls `?status=verifying`.” Second start returns stored URL. |

### Host tests that pin buyer-visible contracts

| Path | Why |
|------|-----|
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPay/PublicPayTests.cs` | GET no Bearer; 404; start replay same URL; GET `started`/`redirect_url`; start paid 409; paused 403; `email_required` chip/stripe; start 503. |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs` | Capacity, unlimited, two-of-two, same-slot replay, one-person paid without slot, start without `slot_key` 400, public GET no Bearer. |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Test/TestRailTests.cs` | Mint+start pays without keys; redirect contains `status=verifying`; GET `paid` + Official Receipt; webhook path. |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Chip/ChipRailTests.cs` | Placeholder email 400 (status only). |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Hosting/CorsTests.cs` | 5178/5179/4179 allow; 3003/3004 deny; all on `/health`. |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs` | Vite packages must not contain `@repo/api-types-ts`. |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayTest.cs` | `SeedCheckout`, `SeedPaymentLink`, `StartPay` injects `slot_key`. |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs` | `Pay:CheckoutBaseUrl=http://pay-checkout.test.example`. |

### Merchant mint (only as the URL that lands here)

| Path | Why |
|------|-----|
| `apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx` | Copies `buyerUrl(public_token)`; POSTs `/v1/payment-links` with `max_payers` / `unlimited`; **does not** send `success_url`. |
| `apps/lazuar-pay-merchant/src/locks.test.ts` | Forbids `@repo/aura-ui` as a package — chrome is **copied**. Contrast with checkout’s “copied aura-ui” lock. |
| `apps/lazuar-pay-merchant/src/index.css` | Same `:root` tokens and `body` `bg-slate-100/80` as checkout. |
| `apps/lazuar-pay-merchant/src/ui/components/card.tsx` | Same Card primitive the checkout copied. |
| `apps/lazuar-pay-merchant/package.json` | **Has** `oidc-client-ts` / `react-oidc-context` / `react-router-dom`. Checkout must not grow these. |

### Spec, CI, topology (as they affect the buyer page)

| Path | Why |
|------|-----|
| `packages/pay-spec/main.tsp` | `PublicPay` / `StartPayRequest` / `PublicPayApi`. Stale vs live GET/start. |
| `packages/pay-spec/dist/` | Grep `email_required` → **no matches**. Compiled OpenAPI behind `main.tsp`. |
| `packages/ui/package.json` | Workspace `@repo/ui` exists; **checkout does not depend on it**. |
| `.github/workflows/ci.yml` | `pay` job: `dotnet test` host, `pnpm --filter lazuar-pay-checkout build`. **Does not** run `vitest`. |
| `Taskfile.yml` | `pay:checkout` → `pnpm --filter lazuar-pay-checkout dev`. |
| `turbo.json` | Generic `dev` / `build` / `test`. No checkout-specific task. |
| `pnpm-workspace.yaml` | `apps/*`. |
| `mprocs-dev.yaml` | Hub frontends + Caddy. **Does not** start `:5179`. |
| `deploy/dev/Caddyfile` | Hub path map on `:9080`. **No** `/c/` and **no** 5179. |
| `deploy/prod/Caddyfile` | Hub `hub.lazuar.com` only. **No** checkout SPA fallback. |
| `apps/lazuar-pay/docker-compose.pay.yml` | Postgres 5435 only. No checkout image. |
| `plans/019-evals/README.md` | This slice’s charter: “Buyer checkout restyled with aura-ui chrome.” |

### Background papers (not authority when they disagree)

| Path | Why |
|------|-----|
| `plans/014-evals/03-checkout-frontend.md` | GET-once, no poll, no `email_required`, 503-only map, health-probe leftovers. |
| `plans/016-adapters-check/03-checkout-frontend.md` | Verifying poll, mashed 400, no `slot_key`, no Card, 4179 missing from CORS, stuck verifying after 30s. |
| `plans/018-evals/001-evals.md` | Product thesis. Not a file inventory. |

Grep-only (not full open): `Bearer|oidc|Authorization|zitadel|whoami` under checkout `src` (no hits except `locks.test.ts` package names); `Malay|Bahasa|ms-MY` under checkout (no hits); `status = "expired"` / `"failed"` writers under `apps/lazuar-pay` `*.cs` (refuse-on-start only; no writer); `5179` under `deploy/` (no hits).

---

## What exists (routes, GET/start/poll, UI)

### Package and listen

`apps/lazuar-pay-checkout/package.json`:

```6:13:apps/lazuar-pay-checkout/package.json
    "dev": "vite --port=5179 --host=0.0.0.0 --strictPort",
    "build": "tsc -b && vite build",
    "lint": "oxlint",
    "test": "vitest run",
    "check-types": "tsc -b",
    "preview": "vite preview --port=4179 --strictPort",
    "clean": "rm -rf dist"
```

Runtime deps are no longer “react + react-dom only” (014/016). Live:

```15:22:apps/lazuar-pay-checkout/package.json
  "dependencies": {
    "@radix-ui/react-slot": "^1.3.3",
    "class-variance-authority": "^0.7.1",
    "clsx": "^2.1.1",
    "lucide-react": "^1.33.0",
    "react": "^19.2.8",
    "react-dom": "^19.2.8",
    "tailwind-merge": "^3.6.0"
  },
```

Still **absent**: `oidc-client-ts`, `react-oidc-context`, `react-router-dom`, `openapi-fetch`, `@repo/api-types-ts`, `@repo/aura-ui`, `@stripe/stripe-js`, CHIP.js, Billplz JS. Merchant `:5178` **has** OIDC + router. That split is the plane lock.

`vite.config.ts` dual-pins 5179 and comments the footgun:

```5:18:apps/lazuar-pay-checkout/vite.config.ts
// Dual-pinned with package.json `vite --port=5179`.
// strictPort: fail loud if 5179 is busy — never silently steal merchant :5178.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    host: true,
    port: 5179,
    strictPort: true,
  },
  preview: {
    host: true,
    port: 4179,
    strictPort: true,
  },
})
```

Vite’s SPA fallback serves `index.html` for `/c/{token}` in `dev` / `preview`. There is **no** `react-router-dom`. `tokenFromPath` reads `window.location.pathname`. Production static hosting without the same fallback 404s `/c/…`. Hub `deploy/dev/Caddyfile` and `deploy/prod/Caddyfile` do **not** mention 5179 or `/c/`. There is **no** checkout Dockerfile.

`task pay:checkout` runs the Vite dev server (source, not `dist/`). CI `pay` job builds checkout (`tsc -b && vite build`) and does **not** run `vitest`.

README:

```1:15:apps/lazuar-pay-checkout/README.md
# Lazuar Pay — checkout

Hosted buyer pay page for focused Pay. Not `lazuar-portal` (`:3004`).

| | |
|---|---|
| Origin | `http://localhost:5179` (`strictPort`) |
| API | focused Pay `http://localhost:8081` (`VITE_PAY_API_URL`) |

Buyers have **no** One account. Fail if this page asks for Zitadel login. Receipts / update-payment can share this origin later (magic link to the payer mailbox), not the merchant shell.
```

`.env.example` is still that one public origin. Never Hub `:8080`. Never `VITE_*` secrets. Never `VITE_ONE_AUTHORITY`. Keep it that way.

### The only route: `/c/:token`

There is no router. The cash-register URL is a path convention:

```38:45:apps/lazuar-pay-checkout/src/App.tsx
function tokenFromPath(): string | null {
  const m = window.location.pathname.match(/^\/c\/([^/]+)/)
  return m ? decodeURIComponent(m[1]) : null
}

function verifyingQuery(): boolean {
  return new URLSearchParams(window.location.search).get('status') === 'verifying'
}
```

| Path | `tokenFromPath` | Pixel if that is the only input |
|------|-----------------|----------------------------------|
| `/c/{hex}` | token | GET `/v1/pay/{token}` |
| `/c/{hex}/` (trailing slash) | token (`[^/]+` stops at `/`; no `$`) | Same GET. Trailing slash is ignored, not “missing.” |
| `/c/{hex}/extra` | token (prefix match; 016 said this fails — live regex has no `$`) | Same GET as `/c/{hex}`. Extra segments are not rejected. |
| `/pay/{token}` | null | “Link not found” |
| `/` | null | “Link not found” |
| `/c/{hex}?status=verifying` | token | GET + poll; verifying UI until paid/expired/full/timeout |
| `/c/{hex}?status=paid` | token | Query **ignored**. Paid pixel only if GET `status === 'paid'`. |
| `/callback` | null | “Link not found”. There is no OIDC callback route. |

Merchant mint writes exactly `http://localhost:5179/c/${public_token}` (or `VITE_CHECKOUT_ORIGIN`) with **no** query (`CheckoutsPage.tsx` `buyerUrl`). Hosted success URLs append `?status=verifying` against **the payment-link token** (not the child checkout’s own `PublicToken`). Cancel URLs omit the query.

`index.html` `lang="en"`. One `#root`. `main.tsx` mounts `<App />` under `StrictMode`. Dev StrictMode double-invokes the GET effect; the `stop` flag drops the first response. GET is not a charge.

### API base (hardcoded fallback)

```8:8:apps/lazuar-pay-checkout/src/App.tsx
const payApi = import.meta.env.VITE_PAY_API_URL ?? 'http://localhost:8081'
```

Vite **inlines** `VITE_*` at build time. A production bundle built without `VITE_PAY_API_URL` talks to `http://localhost:8081` from the buyer’s browser. There is no runtime `/config.json`. Trailing slash on the env var would produce `http://host:8081//v1/pay/...` (merchant `oneApi.ts` strips; checkout does not).

The SPA talks to two URLs only:

1. `GET ${payApi}/v1/pay/${token}?slot_key=${encodeURIComponent(slotKey(token))}`
2. `POST ${payApi}/v1/pay/${token}/start` with JSON `{ name, email, slot_key }`

No `/v1/whoami`, no `/v1/checkouts/{id}`, no `/v1/orgs/.../gateway`, no `/health` (014 health-probe is gone), no Stripe.js, no CHIP SDK, no `Authorization` header, no `credentials: "include"`.

`PayView` is a local structural type, **not** generated from `pay-spec`:

```10:19:apps/lazuar-pay-checkout/src/App.tsx
type PayView = {
  token: string
  amount: number
  currency: string
  status: string
  email_required?: boolean
  started?: boolean
  provider?: string | null
  redirect_url?: string | null
}
```

Live GET also returns `payer_name`, `payer_email`, `remaining`, `max_payers`, `paid_count`, `taken_count`. The SPA type omits them. It never prefills name/email. It never shows remaining seats.

### `slot_key` — one browser, one seat

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

`crypto.randomUUID()` is 36 characters. Host `NormalizeSlotKey` accepts 8–128 trimmed chars. A UUID is valid.

The `catch` path **does not persist**. Every `slotKey()` call mints a new UUID if `localStorage` throws (Safari private mode is the realistic case). GET, start, and poll then disagree about who “mine” is. That is a bug; see Bugs.

Two tabs in the same browser share one key → one seat. That is the occupancy intent. Two browsers → two keys → two seats (until `max_payers`).

### Host GET `/v1/pay/{token}`

`Program.cs` maps `MapPublicPay()` with no auth middleware on those routes. JSON is snake_case globally:

```25:28:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
});
```

```21:48:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
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
        CancellationToken ct)
    {
        var link = await db.PaymentLinks.AsNoTracking().FirstOrDefaultAsync(x => x.PublicToken == token, ct);
        if (link is not null)
        {
            return await GetLink(link, slot_key, db, ct);
        }

        var session = await store.GetByPublicTokenAsync(token, ct);
        if (session is null)
        {
            return PayErrors.Status(404, "Not Found", "Checkout not found");
        }

        var row = await db.Checkouts.AsNoTracking().FirstAsync(x => x.Id == session.Id, ct);
        return CheckoutView(token, row);
    }
```

Lookup order: **payment link first**, then standalone checkout. Merchant 018 mints payment links, so the WhatsApp URL is almost always a link token. Standalone `POST /v1/checkouts` still exists (kernel / README curl). Child checkouts minted under a link have their **own** `PublicToken`; the buyer is never given that token. GET of a child token would skip occupancy wrapping. Merchant copy uses the **link** token.

`GetLink` occupancy (buyer-visible):

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

`PaymentLinkOccupancy`:

```1:13:apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkOccupancy.cs
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

Unlimited (`MaxPayers` null) is never full. `remaining` is JSON `null`.

`CheckoutView` / `LinkView` always include `email_required`, `started`, `provider`, `redirect_url`, and the occupancy counters. `email_required` is computed from **the row’s / link’s `Provider`**, not from `OrgSettings.ActiveProvider` (that column is marked unused on this SHA):

```35:36:apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs
    public static bool RequiresEmail(string provider) =>
        provider is not Stripe and not Test;
```

| Provider on the link/row | GET `email_required` | SPA Pay button |
|--------------------------|----------------------|----------------|
| `stripe` | `false` | Enabled with empty email. |
| `test` | `false` | Enabled with empty email. Test copy on the form. |
| `chip` / `billplz` / `xendit` / `razorpay` | `true` | Disabled until `usableEmail`. |
| missing / garbage (standalone never-started with null provider) | `false` (`TryNormalize` fails) | Enabled; start → 503 `rail not configured`. |

016 computed `row.Provider ?? settings.ActiveProvider` and `RequiresEmail` was “not Stripe” only. Live bind-at-mint + Test exception are the 018/017 delta. `PublicPayTests.Email_required_true_when_active_chip` still has “active” in the method name; the body seeds a **chip checkout**, which now stamps `Provider` at mint.

`redirect_url` is returned only when `started && row.Status == "open"`. Paid/expired hide it.

`PublicPayTests.Public_get_does_not_need_bearer` and `PaymentLinkTests.Public_get_does_not_need_bearer` both assert a second GET does not increment One `SendCount`. That is the NP-CHK-007 host half.

### Host POST `/v1/pay/{token}/start`

Body is `StartPayRequest { Name?, Email?, SlotKey? }` with snake_case JSON. SPA always sends all three.

For a **payment-link token**, Start goes through `MintOrResume`:

- Charges paused → 403 `"Org charges are paused"`.
- Missing/short/long `slot_key` → 400 `"slot_key is required"`. Locked by `PaymentLinkTests.Start_link_without_slot_key_is_400`.
- Existing child for that slot, status `paid` or `expired` → 409 `"Checkout is not open"`.
- Existing child still `open` → **resume** that row (no second seat). Locked by `Same_slot_start_twice_does_not_take_two_seats` (CHIP mocked: `Psp.SendCount == 1`).
- No child and `IsFull` → 409 `"This pay link is full"`. Locked by `Two_people_can_pay_a_link_of_two`.
- Else mint a child checkout with:
  - new `PublicToken` (unused by the buyer URL)
  - `SlotKey = slot`
  - `SuccessUrl = {CheckoutBaseUrl}/c/{link.PublicToken}?status=verifying`
  - `CancelUrl = {CheckoutBaseUrl}/c/{link.PublicToken}`
  - `Status = "open"`, amount/currency/provider copied from the link

For a **standalone checkout token**, Start uses `CheckoutStore.GetByPublicTokenAsync`. Missing → 404. `paid`/`expired` → 409 `"Checkout is not open"`. Paused → 403. `slot_key` is ignored.

Then, for either path:

1. Persist non-whitespace `name` / `email` onto the row.
2. `TryNormalize(row.Provider ?? link.Provider)` else 503 `"rail not configured"`.
3. `RequiresEmail && !BuyerEmail.IsUsable` → 400 `"email is required"`.
4. **If `PspRedirectUrl` already set**, save name/email and return that URL. **No second PSP HTTP.** This is the host half of start replay. `PublicPayTests.Start_twice_returns_same_url_without_second_psp_http` locks it for CHIP. SPA also short-circuits client-side when `pay.started && pay.redirect_url`.
5. Else `CreateHostedUrlAsync`, persist `Provider` / `PspRedirectUrl` / `ProviderSessionId`.
6. **Test special case:** insert a `PspWebhookEvents` row and call `fulfillment.FulfillPaidAsync` **in the same start**. Redirect URL is still `CheckoutUrls.Success` (verifying query). Status is already `paid` before the browser follows the URL.
7. `InvalidOperationException` containing `"callback base"` (ordinal) → 400 with the exception message. Anything else → 503 with the exception message.
8. `Stripe.StripeException` → 503 `"Stripe rejected the org key"`.

`PayErrors.Status` body is always `{ status, title, detail }`.

### SPA GET on boot

```74:97:apps/lazuar-pay-checkout/src/App.tsx
  useEffect(() => {
    if (!token) {
      setError('missing')
      return
    }
    const path = payPath(token)
    let stop = false
    async function load() {
      const r = await fetch(path)
      if (r.status === 404) throw new Error('missing')
      if (!r.ok) throw new Error(`status ${r.status}`)
      return (await r.json()) as PayView
    }
    void load()
      .then((body) => {
        if (!stop) setPay(body)
      })
      .catch((err: unknown) => {
        if (!stop) setError(err instanceof Error ? err.message : 'error')
      })
    return () => {
      stop = true
    }
  }, [token])
```

No Bearer. No AbortController (StrictMode `stop` flag only). 404 → `'missing'`. Other non-OK → `'status {code}'`. Network/CORS → browser `Failed to fetch` (or similar). Render order (below) turns every non-`missing` GET failure into **Loading… forever** unless a verifying poll later `setPay`s.

### SPA poll while `?status=verifying`

```99:115:apps/lazuar-pay-checkout/src/App.tsx
  useEffect(() => {
    if (!token || !verifying || pay?.status === 'paid' || pay?.status === 'expired' || pay?.status === 'full') return
    let n = 0
    const id = window.setInterval(() => {
      n += 1
      void fetch(payPath(token))
        .then((r) => (r.ok ? r.json() : null))
        .then((body: PayView | null) => {
          if (body) setPay(body)
        })
      if (n >= 15) {
        window.clearInterval(id)
        setVerifyTimedOut(true)
      }
    }, 2000)
    return () => window.clearInterval(id)
  }, [token, verifying, pay?.status])
```

| Claim | Live |
|-------|------|
| Interval | 2000 ms. First tick at t=2s. No immediate poll; boot GET is t=0. |
| Cap | `n >= 15` → 15 interval GETs, last at t=30s, then clear. |
| Stop on paid / expired / full | Effect guard. 018 added **full**. |
| Stop on missing | **No.** Non-OK → `null`, ignored. Initial 404 still starts the poll (`pay?.status` undefined). |
| Treat query as paid | **No.** Query only sets `verifying`. Paid pixel is `pay.status === 'paid'`. |
| OIDC on poll | **No.** Same public GET + `slot_key`. |
| After cap | 016 left Verifying stuck forever. 018 sets `verifyTimedOut` and paints a **Refresh status** button. Refresh is **one** GET. It does **not** restart the interval. |
| Errors | No `.catch`. Network throw is an unhandled rejection. `pay` unchanged. |

`verifying` is `useState(verifyingQuery())` with **no** later `setVerifying`. Query is sticky for the document lifetime. `popstate` without reload does not update React. Fine for `location.assign` returns.

### SPA start

```117:160:apps/lazuar-pay-checkout/src/App.tsx
  async function startPay() {
    if (!token) return
    if (pay?.email_required && !usableEmail(email)) {
      setError('email is required')
      return
    }
    if (pay?.started && pay.redirect_url) {
      window.location.assign(pay.redirect_url)
      return
    }
    setBusy(true)
    try {
      const response = await fetch(`${payApi}/v1/pay/${token}/start`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, email, slot_key: slotKey(token) }),
      })
      const detail = await readDetail(response)
      if (response.status === 409) {
        const again = await fetch(payPath(token))
        if (again.ok) setPay((await again.json()) as PayView)
        else setError(detail ?? 'This pay link is full')
        return
      }
      if (response.status === 503) {
        setError(detail ?? 'rail not configured')
        return
      }
      if (response.status === 400) {
        setError(detail ?? `start ${response.status}`)
        return
      }
      if (!response.ok) {
        setError(detail ?? `start ${response.status}`)
        return
      }
      const body = (await response.json()) as { redirect_url?: string }
      if (body.redirect_url) {
        window.location.assign(body.redirect_url)
      }
    } finally {
      setBusy(false)
    }
  }
```

```339:352:apps/lazuar-pay-checkout/src/App.tsx
function usableEmail(value: string): boolean {
  const trimmed = value.trim()
  return trimmed.length > 0 && trimmed.toLowerCase() !== 'customer@example.com'
}

async function readDetail(response: Response): Promise<string | null> {
  try {
    const clone = response.clone()
    const body = (await clone.json()) as { detail?: string }
    return body.detail?.trim() || null
  } catch {
    return null
  }
}
```

016’s mashed 400 string `'callback base not public or email required'` is **gone**. `locks.test.ts` forbids that sentence. 400/503/403 now surface host `detail` when JSON parses.

`fetch` throw (network / CORS) is **not** in `try/catch` around `fetch` — it is inside `try`, so it rejects out of `startPay`; `finally` still clears `busy`. Unhandled rejection. No `role="alert"` for that path.

200 without `redirect_url`: function ends. Busy clears. Form remains. Host success path always sets `redirect_url`; a broken proxy could still hit this.

### Render order (total order; later branches unreachable)

`Shell` is a centered column, kicker “Lazuar Pay”, `max-w-md`, `min-h-dvh`, `px-4 py-10`. Every pixel is a `Card`.

| Order | Condition | Pixel | Pay form | Poll |
|------|-----------|-------|----------|------|
| 1 | `error === 'missing'` **or** `!token` | “Link not found” / “This payment link is not valid. **No sign-in.**” | Hidden | May still run if token existed and verifying |
| 2 | `!pay` | “Loading…” | Hidden | May run |
| 3 | `pay.status === 'paid'` | “Payment received” + `formatMoney` + Official Receipt / not e-invoice / not membership | Hidden | Guard stops |
| 4 | `pay.status === 'expired'` | “Link expired” / “This payment link is no longer open.” | Hidden | Guard stops |
| 5 | `pay.status === 'full'` | “Link is full” / “This pay link has no remaining payments.” | Hidden | Guard stops (018) |
| 6 | `verifying && pay.status !== 'paid'` | “Confirming payment” / “The processor success URL is **not paid**. Waiting for the webhook.” After 15 ticks: “Not paid yet. The success URL is not paid.” + Refresh | **Hidden** | Runs while open |
| 7 | else (open, not verifying) | Amount due + name + email + Pay / Continue | **Shown** | Off |

Paid is checked **before** verifying. Returning from Test (already fulfilled in Start) paints Paid on the first GET and never shows Confirming, unless the first GET loses the race with the same-process write (it should not: Start returns only after `FulfillPaidAsync`).

`formatMoney` uses `Intl.NumberFormat('en-MY', { style: 'currency', currency })` with a `${amount} ${currency}` fallback. 014 showed raw `10 MYR`. Restyle shows `RM 10.00` for MYR. Locale is **en-MY**, not `ms-MY`.

Form copy:

```289:294:apps/lazuar-pay-checkout/src/App.tsx
          <p className="text-sm text-slate-500">
            Buyers have no One account.
            {pay.provider === 'test'
              ? ' Test processor: Pay marks this paid. No card, no secret.'
              : ' Completing payment on the processor is not the same as a success URL.'}
          </p>
```

Button: `started ? 'Continue to processor' : 'Pay'`. Disabled when `busy || emailBlocked`. `emailBlocked = Boolean(pay.email_required && !usableEmail(email))`.

There is **no** `<select>` of providers. There is **no** card number field. Inputs are `id="payer_name"` / `id="payer_email"` with `autoComplete="name"` / `autoComplete="email"`. Email is `type="email"` (016 lacked this). Name is never required. No TIN.

### CORS (what the browser needs)

```59:72:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p =>
        p.WithOrigins(
                "http://localhost:5178",
                "http://127.0.0.1:5178",
                "http://localhost:5179",
                "http://127.0.0.1:5179",
                "http://localhost:4178",
                "http://127.0.0.1:4178",
                "http://localhost:4179",
                "http://127.0.0.1:4179")
            .AllowAnyHeader()
            .AllowAnyMethod());
});
```

016 said preview `:4179` was missing. Live `CorsTests.Health_allows_preview_checkout_origin` locks 4179. 3003 ops and 3004 portal still denied.

There is **no** production checkout origin in this list. A deployed SPA on `https://pay.example` fetching `https://api.example` (or even `http://localhost:8081`) will fail CORS. The SPA then paints Loading… (GET non-OK / `Failed to fetch`). This is a laptop-only CORS policy.

`AllowCredentials` is **not** set. SPA `fetch` defaults to `credentials: "same-origin"` omitted for cross-origin → no cookies. Compatible.

### Success / cancel URLs that land here

`CheckoutUrls`:

```8:32:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/CheckoutUrls.cs
    public static string Success(CheckoutRow checkout, IConfiguration config, IHostEnvironment env) =>
        string.IsNullOrWhiteSpace(checkout.SuccessUrl)
            ? Base(config, env) + "/c/" + checkout.PublicToken + "?status=verifying"
            : checkout.SuccessUrl;

    public static string Cancel(CheckoutRow checkout, IConfiguration config, IHostEnvironment env) =>
        string.IsNullOrWhiteSpace(checkout.CancelUrl)
            ? Base(config, env) + "/c/" + checkout.PublicToken
            : checkout.CancelUrl;

    public static string Base(IConfiguration config, IHostEnvironment env)
    {
        var raw = config["Pay:CheckoutBaseUrl"]?.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(raw))
        {
            return raw;
        }

        if (env.IsEnvironment("Testing"))
        {
            return "http://localhost:5179";
        }

        throw new InvalidOperationException("Pay:CheckoutBaseUrl is required");
    }
```

016 quoted Stripe/CHIP/Billplz as `checkout.SuccessUrl ?? "http://localhost:5179/c/" + ...`. Live rails call `CheckoutUrls.Success` / `Cancel`. Development sets `Pay:CheckoutBaseUrl` to `http://localhost:5179`. Tests set `http://pay-checkout.test.example`. Production **must** set the env; missing throws at **start**, which the SPA maps as 503 `Pay:CheckoutBaseUrl is required` (exception message, no `"callback base"` substring).

Merchant `:5178` **does not send** `success_url` on payment-link create. `MintOrResume` always stamps the child with `{CheckoutBaseUrl}/c/{linkToken}?status=verifying`. Those returns **land here**.

Kernel `POST /v1/checkouts` still accepts `success_url` / `cancel_url`. The host README curl uses `https://example.test/ok`. That buyer **never** returns to `:5179`. Out of this SPA’s runtime; in scope as “merchant-minted URLs that land here” — the merchant Vite path **does** land here; the kernel curl path may not.

| Rail | Success field | Cancel / failure field | Lands on `:5179` when URLs are defaults |
|------|---------------|------------------------|------------------------------------------|
| Stripe | `SuccessUrl` + `?status=verifying` | `CancelUrl` without query | Yes |
| CHIP | `success_redirect` | `failure_redirect` **and** `cancel_redirect` without query | Yes |
| Billplz | `redirect_url` (success) | **none** — unpaid Billplz still uses `redirect_url` | Yes, and an **unpaid** return looks like verifying |
| Xendit | `success_redirect_url` | `failure_redirect_url` without query | Yes |
| Razorpay | `callback_url` GET | **none** | Yes; cancel may stay on Razorpay or hit success |
| Test | `CheckoutUrls.Success` | n/a (no hosted page) | Yes; already `paid` before navigation |

Billplz **callback** (Plane B) stays `Pay:PublicBaseUrl`. Buyer return stays `Pay:CheckoutBaseUrl`. Do not mix them. SPA never sees the callback.

### Test processor buyer path

`TestHosted`:

```11:21:apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestHosted.cs
    public Task<HostedSession> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct)
    {
        if (!PayProviders.AllowsTest(env))
        {
            throw new InvalidOperationException("rail not configured");
        }

        return Task.FromResult(new HostedSession(
            CheckoutUrls.Success(checkout, config, env),
            "test:" + checkout.Id));
    }
```

`AllowsTest` is `!env.IsProduction()`. No credential row. No PSP HTTP (`TestRailTests` asserts `factory.Psp.SendCount == 0`). Start then fulfills in-process and returns the verifying URL. SPA `location.assign`s it. Reload GET is `paid`. Paid pixel. Copy on the **form** (not the paid pixel) says “Test processor: Pay marks this paid. No card, no secret.”

SPA never asks for a secret. SPA never names Test as a wallet. `locks.test.ts` greps `pay.provider === 'test'` and `No card, no secret`.

### No PAN, no OIDC (must stay true)

Grep of `apps/lazuar-pay-checkout/src` for `Bearer`, `Authorization`, `whoami`, `zitadel`, `oidc`, `/login`: hits only `locks.test.ts` package-name assertions.

Inputs: name + email. No `type="password"`. No `autocomplete="cc-number"` / `cc-csc` / `cc-exp`. No iframe. No Stripe Element. Wallet regex in locks: `grabpay|tng|touchngo|boost|duitnow|fpx|shopee` (case-insensitive). Current `App.tsx` does not contain those strings. The `tng` substring is still a brittle lock (`starting` would fail it; that word is not in the file).

**Verdict on the fail lock:** the SPA does **not** send Bearer and does **not** open OIDC. That lock holds on this SHA.

---

## 018 restyle delta (re-verified)

016 (`c621ceba`) described a single-file SPA with `App.css` health-probe leftovers, raw `{amount} {currency}`, `<h1>Paid</h1>`, mashed 400, no `slot_key`, no `started` resume, no Test copy, no full pixel, verifying stuck after 30s, CORS without 4179.

Live vs that paper, re-read against `App.tsx` / `package.json` / `Program.cs` / host tests:

| 016 live | 018 live (this SHA) | Broke anything? |
|----------|---------------------|-----------------|
| Runtime deps: `react`, `react-dom` only | + `@radix-ui/react-slot`, `cva`, `clsx`, `lucide-react`, `tailwind-merge` | **No OIDC added.** Chrome only. |
| `App.css` dl/dt grid leftover | **Deleted.** `index.css` is Tailwind 4 + copied tokens matching merchant `index.css` | Cleanup. |
| `<main>` / `<h1>` / `<p className="kicker">` | `Shell` + `Card` + lucide icons in a 12px circle | **Heading semantics lost.** `CardTitle` is a `div`. No `h1`. |
| Amount `10 MYR` | `formatMoney` → `RM 10.00` (`en-MY`) | Better for MY; still not Malay (`ms-MY`). |
| `PayView` had `email_required` only | + `started`, `provider`, `redirect_url` | Needed for Test copy + resume. Still omits occupancy counters and payer fields. |
| Start body `{ name, email }` | + `slot_key` | Occupancy. Host 400s payment-link start without it. |
| GET `/v1/pay/{token}` | GET with `?slot_key=` | Occupancy “mine”. |
| 400 → `'callback base not public or email required'` | `readDetail` → host `detail`, fallback `start 400` | **Honesty fix.** `locks.test.ts` forbids the mashed sentence. |
| 503 → always `'rail not configured'` | `detail ?? 'rail not configured'` | **Honesty fix.** Buyer can see `Stripe rejected the org key` / `CHIP rejected the org key`. |
| 409 → `'start 409'` | Re-GET; setPay; fallback `'This pay link is full'` | Occupancy + paid-in-another-tab. |
| 403 → `'start 403'` | `detail` → `"Org charges are paused"` | No longer looks like an auth wall. |
| No placeholder UI lock | `usableEmail` rejects `customer@example.com` | 016 K11 hole **closed** in UI. Button disable has **no** helper text (new hole). |
| No `type="email"` / `autocomplete` | Both present | a11y/mobile win. |
| Verifying copy “Verifying…” | “Confirming payment” + timeout footer + Refresh | 016 stuck-pixel **partially** fixed. Refresh does not restart poll. |
| Poll stop: paid / expired | + `full` | Occupancy. |
| No full pixel | “Link is full” | Occupancy. |
| No Test copy | `pay.provider === 'test'` sentence | Not a wallet tile. |
| No started resume | “You already started” / “Continue to processor” + client-side `location.assign(redirect_url)` | Matches host idempotent URL. |
| CORS 5178/5179 only | + 4178/4179 | 016 preview graveyard **fixed for laptop**. Production origins still missing. |
| `Pay:CheckoutBaseUrl` missing (hardcoded 5179 in rails) | `CheckoutUrls` + Development appsettings | Phone/deployed return URL is configurable. SPA still defaults API to 8081. |
| Merchant minted `POST /v1/checkouts` | Merchant mints `POST /v1/payment-links` | Shared URL + occupancy. SPA must send `slot_key`. |
| Dist stale vs source | Dist **still** stale: no `slot_key` / `Payment received` / `Link is full` in `dist/assets/*.js` | `task pay:checkout` uses Vite source. `vite preview` of committed dist is the wrong app. |

What the restyle **did not** change (still live, still wrong or still right):

- Loading graveyard for non-404 GET.
- Poll does not stop on missing; `error === 'missing'` wins even if a later poll `setPay`s.
- No prefill of `payer_name` / `payer_email`.
- No remaining/max_payers copy on the buyer page.
- No expired writer on the host; expired pixel is still a costume.
- No `failed` status anywhere.
- No Malay copy.
- `locks.test.ts` is still a filesystem grep, not a render.
- CI still does not run `vitest`.
- No Bearer, no OIDC, no PAN, no wallet tiles, query is not paid. **Holds.**

Copied chrome is **files under `src/ui/`**, not `@repo/aura-ui` (merchant locks forbid that package; checkout never depended on it). Tokens in `index.css` match merchant `index.css` (`--radius: 0.625rem`, oklch palette, `bg-slate-100/80`). That is the 018 “aura-ui restyle” in this package: copy the primitives, do not import Aura ops nav, do not import Hub portal.

---

## Bugs (evidence, impact, how to solve)

Each item is live on this SHA. “How to solve” is judgment, not work.

### B1. Non-404 GET failure paints Loading… forever

**Evidence.** Render order: `error === 'missing'` first, then `if (!pay) return … Loading…`. Boot GET sets `error` to `'status 500'` / `'Failed to fetch'` / `'error'` and leaves `pay` null.

```162:186:apps/lazuar-pay-checkout/src/App.tsx
  if (error === 'missing' || !token) {
    return (
      <Shell>
        <Card>
          …
            <CardTitle className="text-xl">Link not found</CardTitle>
            <CardDescription>This payment link is not valid. No sign-in.</CardDescription>
```

```178:186:apps/lazuar-pay-checkout/src/App.tsx
  if (!pay) {
    return (
      <Shell>
        <Card>
          <CardContent className="py-10 text-center text-sm text-slate-500">Loading…</CardContent>
        </Card>
      </Shell>
    )
  }
```

014 and 016 recorded this. 018 restyle wrapped it in a Card and did not add an error pixel.

**Impact.** Dead 8081, CORS rejection (production origin not in `WithOrigins`, or `pnpm preview` against a host that is not 4179), GET 500 — the buyer stares at “Loading…”. Looks like a hung phone, not “Pay is down.” First visit has **no** poll, so there is no recovery. Verifying return *can* recover if a later poll `setPay`s, because `error !== 'missing'` and `pay` becomes set.

**How to solve.** After boot GET fails with anything other than 404, paint a Card: title “Can’t reach Pay”, host `detail` if any, Retry button that re-runs `load()`. Do not use the word “sign in”. Do not send Bearer. Keep 404 as “Link not found”.

### B2. CORS allow-list is laptop-only; production checkout cannot call 8081

**Evidence.** `Program.cs` `WithOrigins` is eight localhost/127.0.0.1 URLs on 5178/5179/4178/4179. `CorsTests` lock those and deny 3003/3004. There is no `Pay:CorsOrigins` config. Checkout `fetch` is cross-origin to `VITE_PAY_API_URL`.

**Impact.** The first production (or phone-via-LAN, or `https://pay-local.lazuar.dev`) buyer GET is a CORS failure → B1. 018 adding 4179 fixed the **preview** dogfood 016 called out. It did not make CORS configurable.

**How to solve.** Config list `Pay:CorsOrigins` (comma-separated). Development default = the eight laptop URLs. Production must include the checkout origin(s). Keep denying 3003/3004. Add a CorsTest that a non-listed origin is denied and a configured extra origin is allowed. Never `AllowAnyOrigin` with credentials.

### B3. Hardcoded API fallback `http://localhost:8081`

**Evidence.** `const payApi = import.meta.env.VITE_PAY_API_URL ?? 'http://localhost:8081'`. Vite inlines at build. `.env.example` is the laptop URL. No Dockerfile / CI production env for checkout.

**Impact.** A `pnpm build` without the env, then any static host, sends every buyer browser to the developer’s laptop. Combined with B2, even a correct API host fails CORS if the SPA origin is not localhost.

**How to solve.** Fail the production build if `VITE_PAY_API_URL` is unset (do not default). Keep the laptop default only in `.env.example` / Vite `envDir` for `dev`. Strip trailing slashes. Document that this value is public (8081 origin), never a secret.

### B4. `localStorage` failure mints a new `slot_key` on every call

**Evidence.** `slotKey` `catch { return crypto.randomUUID() }` with no memory fallback. `payPath` (GET/poll) and start POST each call `slotKey(token)`.

**Impact.** Private-mode Safari (or blocked storage): GET uses slot A, start uses slot B (new child or 400/409), poll uses slot C. Occupancy can **double-take** seats on an unlimited or remaining>0 link, or 409 full while the buyer already started under A and cannot resume. Host `Same_slot_start_twice_does_not_take_two_seats` does not cover this; it reuses one slot string.

**How to solve.** Module-level `Map<token, string>` fallback when `localStorage` throws. Same in-memory UUID for the document lifetime. Optionally persist to `sessionStorage` first. Do not mint inside `payPath` without caching. Host already treats slot as the occupancy identity — the SPA must not rotate it.

### B5. One-person paid link shows “Payment received” to strangers

**Evidence.** `GetLink`: if not mine and `MaxPayers == 1 && paid >= 1`, return `CheckoutView` of the **paid row**. SPA order 3 paints “Payment received” / “Thank you.” `PaymentLinkTests.One_person_link_shows_paid_without_slot_after_pay` locks GET-without-slot as `paid` — that is the original payer returning without a key, **and** every other browser.

**Impact.** A forwarded WhatsApp link after Ada paid looks like **this** visitor paid. Max>1 full correctly paints “Link is full”. Max=1 is the dishonest special case.

**How to solve.** Host: if `mine` is null and paid, return `LinkView` with `status: "paid"` **without** implying this browser is the payer, **or** a distinct `status: "already_paid"`. SPA: if GET says paid but `started` is false **and** `slot_key` did not match (host can add `mine: false`), paint “This link is already paid” / not “Thank you.” Keep the original payer’s slot → “Payment received.” Test both: slot of payer vs a fresh slot.

### B6. Abandoned `open` children fill the link forever; expired pixel is a costume

**Evidence.** `CountsTowardCapacity` is `open` or `paid`. There is **no** writer of `expired` in `apps/lazuar-pay/**/*.cs` except Start **refusing** that status. `Fulfillment` writes `paid` only. SPA still has an expired Card.

**Impact.** Buyer A clicks Pay, hops to CHIP, closes the tab. Seat stays `open`. Remaining drops. Buyer B sees “Link is full” / 409 while nobody has paid. SPA copy “no remaining payments” is true as occupancy and **false** as money. The expired Card never appears.

**How to solve.** Host job: expire `open` children with `PspRedirectUrl` older than N minutes (or never-started open with no URL). Then `GetLink` remaining recovers. SPA expired pixel starts working. Do **not** expire `paid`. Do not have the SPA invent expiry.

### B7. Email-required Pay is disabled with no explanation

**Evidence.** `emailBlocked` disables the button. `startPay`’s `'email is required'` only runs if they click, which a disabled button cannot. Placeholder `customer@example.com` is now blocked (016 hole closed) but the Label is still “Email” with no `*`, no `required`, no helper “CHIP needs an email (not customer@example.com).”

**Impact.** Buyer types the Hub placeholder or leaves the box empty and stares at a grey Pay. Looks broken. Host 400 is never reached for those cases (good) but the UI is mute.

**How to solve.** When `email_required`, mark the Label, `aria-required`, `required` on the input, helper text under the field. If value is the placeholder, `role="alert"` “Use your real email.” Keep `usableEmail` matching `BuyerEmail.IsUsable` (trim, ordinal-ignore-case placeholder). Do not RFC-5322-theatre beyond `type="email"` unless the rails do.

### B8. No prefill after cancel / resume

**Evidence.** GET returns `payer_name` / `payer_email` after Start persists them. `PayView` omits both. Inputs are `useState('')`. Cancel URL (Stripe/CHIP/Xendit) re-shows the form. `email_required` disables Pay until they retype, even though the **row** already has a usable mailbox and a second start with blank email would keep the stored value (host only writes non-whitespace).

**Impact.** Extra friction on the honest cancel path. Host is looser than the UI on retry.

**How to solve.** On GET, if `payer_email` / `payer_name` present, prefill unless the user has already typed. Keep `usableEmail` on the prefilled value (placeholder should not prefill-enable). Document that this is not a login.

### B9. Verifying timeout does not restart; Pay form stays unreachable

**Evidence.** After `n >= 15`, interval dies, `verifyTimedOut` true, Refresh does one GET and does not `setInterval` again. There is no “Back to pay” that strips `?status=verifying`. Query is sticky.

**Impact.** Late webhook (Billplz tunnel down, wrong `whsec`, CHIP PEM): 30s of Confirming, then “Not paid yet” forever unless they Refresh at the right moment. If they never actually paid (Billplz/Razorpay cancel that still hit the success URL — see mismatches), they cannot click Pay without editing the URL.

**How to solve.** Refresh should reset `n` and restart the 15-tick loop. After timeout, if still `open`, offer a secondary button “Return to pay” that `history.replaceState` / `location.assign` without the query (cancel semantics: not paid). Keep the primary copy “success URL is not paid.” Do not auto-show the Pay form on timeout while the query is present if K13’s sentence is “returning from success must not look like first visit.” A labeled escape is honest.

### B10. Poll ignores missing and can disagree with the missing pixel

**Evidence.** Poll deps do not include `error`. Initial GET 404 → `error='missing'`, missing Card, **and** poll starts. Poll 404s are ignored. If a later poll somehow 200s, `setPay` runs but order 1 still shows missing (`error === 'missing'` is first and never cleared).

**Impact.** Fifteen useless 404s on a dead token. A resurrected row would never show. Low probability; still a contract hole.

**How to solve.** If boot GET is 404, do not start the poll. If a poll GET is 404, set `error='missing'` and clear the interval. If boot GET succeeds, clear `error`.

### B11. `startPay` network throw is unhandled; no alert

**Evidence.** `try/finally` around `fetch` without `catch`. `finally` clears `busy`. Rejection is unhandled.

**Impact.** CORS/network on **click** (not just GET): button re-enables, form unchanged, console error. Buyer clicks again. Double-tab can still mint two seats on remaining>1 before `started` is in React state (host occupancy is the real lock; SPA `busy` is UI debounce).

**How to solve.** `catch` → `setError` a human sentence (“Can’t reach Pay”) using the same non-login language. Keep `finally` busy-clear.

### B12. Path regex has no `$` — `/c/{token}/extra` is still the pay page

**Evidence.** Live:

```38:41:apps/lazuar-pay-checkout/src/App.tsx
function tokenFromPath(): string | null {
  const m = window.location.pathname.match(/^\/c\/([^/]+)/)
  return m ? decodeURIComponent(m[1]) : null
}
```

016 claimed the regex was anchored so `/c/{token}/extra` would not match. The pattern is `^/c/([^/]+)` **without** `$`. `String#match` with a non-global regex succeeds on a prefix: `/c/tok/extra` captures `tok`; `/c/tok/` captures `tok`; `/c/` fails (`[^/]+` empty); `/pay/tok` fails. Query strings are not in `pathname`.

**Impact.** Merchant and host mint `/c/{token}` with no extra segment, so WhatsApp paste is fine. `/c/{token}/anything` still loads that checkout instead of “Link not found.” Low severity unless someone later mounts receipts on `/c/{token}/receipt` and this regex steals the page.

**How to solve.** `^/c/([^/]+)/?$` (optional trailing slash, reject extra segments). Do not introduce `react-router` to fix this.

### B13. Checked-in `dist/` is not this SPA

**Evidence.** `dist/index.html` references `/assets/index-CGhBg7uT.js`. Grep of `dist/` for `slot_key`, `Payment received`, `Link is full`, `verifyTimedOut` → no matches. 016 already said that hashed JS lacked then-current strings.

**Impact.** `vite preview` without rebuild, or any deploy that ships git `dist/`, runs a **pre-occupancy / pre-restyle** pixel. `task pay:checkout` is fine (Vite source). CI `pnpm --filter lazuar-pay-checkout build` produces a fresh dist on the runner and does not commit it.

**How to solve.** Either gitignore `dist/` or rebuild in the same commit as `App.tsx`. Prefer gitignore. Production image (when it exists) must `pnpm build` with `VITE_PAY_API_URL`.

### B14. Card titles are not headings; confirming has no live region

**Evidence.** `card.tsx` `CardTitle` is a `div`. `index.html` `lang="en"`. Verifying spinner is a lucide icon with `animate-spin` and no `aria-live`. Loading is a `div`. Only start errors use `role="alert"`.

**Impact.** Screen readers get no `h1` “Payment received” / “Confirming payment”. Confirming updates are silent. Restyle **regressed** 014/016 `<h1>`.

**How to solve.** `CardTitle` as `h1` on these pages (or `as="h1"`). `aria-live="polite"` on confirming / loading. Keep `role="alert"` for errors. Focus the title on status change.

### B15. Start 200 with no `redirect_url` is silent

**Evidence.** `if (body.redirect_url) window.location.assign(...)` else return. Host success always includes it (`StartPayResponse` in spec is `redirect_url: string`). Test always has it.

**Impact.** Malformed proxy / spec drift: button re-enables, form stays, no alert.

**How to solve.** `else setError('Processor did not return a pay URL')`. Do not invent a URL. Do not treat as paid.

---

## Gaps

Things the product needs that this SPA / public door does not have. Not all are bugs in the existing pixels.

### G1. No remaining / capacity copy on the buyer page

Host GET returns `remaining`, `max_payers`, `paid_count`, `taken_count`. Merchant table shows `taken / max` or “Unlimited”. SPA `PayView` omits them. Buyer on a 5-seat link sees “Amount due” with no “3 of 5 paid.” Sold-out is only the `full` Card after the fact.

**How to solve.** Extend `PayView`. If `max_payers` is 1, say nothing extra (one-person link). If limited, “N payments left.” If unlimited, omit. Do not show other payers’ emails.

### G2. No Malay copy

Honesty: **not present.** `index.html` `lang="en"`. `formatMoney` `en-MY`. Grep `Malay|Bahasa|ms-MY|lang="ms"` under the checkout package → none. 018 restyle did not add i18n. SME WhatsApp buyers in MY often want Malay; this page is English-only including “Link is full” / “Confirming payment.”

**How to solve.** When you add it, `lang` + real strings, not a toggle that still English-defaults every error `detail` (host details are English). Do not claim Malay in README until the pixels exist.

### G3. No buyer receipt number / download

Paid copy: “The merchant will see an Official Receipt, not an e-invoice.” Buyer cannot see `RCPT-…`. README: “Receipts / update-payment can share this origin later (magic link to the payer mailbox).” Bar C. Not a restyle bug.

**How to solve.** Later: magic link to this origin, still no One account. Do not put receipts on `:5178`.

### G4. No production hosting for `/c/{token}`

No Dockerfile, not in Hub Caddy, not in `mprocs-dev.yaml`, not in `docker-compose.pay.yml`. Vite history fallback exists only for `pnpm dev` / `preview`. Object storage / nginx without `try_files /index.html` 404s the shareable URL.

**How to solve.** A checkout static host with SPA fallback, `VITE_PAY_API_URL` at build, CORS origin on 8081. Do not put checkout on the merchant OIDC app. Do not retarget `lazuar-portal`.

### G5. `pay-spec` is behind the live public door

`PublicPay` in `main.tsp`: `token, amount, currency, status, email_required?, started?, redirect_url?`. Missing `provider`, occupancy counters, `payer_*`. `StartPayRequest` is `name?, email?` — **no `slot_key`**. `PublicPayApi.get` has no `slot_key` query. Compiled `packages/pay-spec/dist` grep `email_required` → no matches (dist behind even `main.tsp`). SPA does not import the spec; this is a contract-doc hole. TypeSpec full paper is out of scope (Refuse); the mismatch still burns this slice.

**How to solve.** Sibling 08. Do not generate checkout types from the stale OpenAPI until it matches GET/start.

### G6. CI does not run checkout `vitest`

`.github/workflows/ci.yml` `pay` job: `dotnet test` host, `pnpm --filter lazuar-pay-checkout build`, compile pay-spec. `locks.test.ts` is a local `pnpm --filter lazuar-pay-checkout test` pin. A PR can delete the OIDC lock and stay green if `tsc` still passes.

**How to solve.** Add `pnpm --filter lazuar-pay-checkout test` (and merchant) to the `pay` job. Still greps, but at least the greps run.

### G7. Locks do not grep `src` for Bearer / `:5175` / `whoami`

`locks.test.ts` OIDC lock reads **package.json** only. Merchant locks walk `src`. A future `<a href="http://localhost:5175">` or `headers: { Authorization }` would not fail the existing checkout test.

**How to solve.** Walk `src/**/*.ts{,x}` like merchant. Forbid `oidc-client-ts`, `Authorization`, `Bearer `, `whoami`, `zitadel`, `:5175`, `lazuar_auth`, `autocomplete="cc-number"`. Keep the wallet regex.

### G8. No jsdom / Playwright “buyer has no login” e2e

Vitest environment is **node**. Tests never mount `App`, never fake `fetch`, never assert poll timing. 019/09 should list the missing methods. Lived loop A99.1 (human opens `:5179/c/{token}` without One) is still a human checkbox.

**How to solve.** One Playwright spec: no redirect to `:5175`, no `Authorization` in the request log, GET `/v1/pay/{token}` unauthenticated, Pay click POSTs start without Bearer. Do not add OIDC to make that easier.

### G9. Merchant display name / product label absent on the buyer page

GET does not return org name or product name. Merchant list has `label` from the product. Buyer sees “Lazuar Pay” kicker and an amount. Fine for dogfood; thin for WhatsApp trust.

**How to solve.** Optional public `label` / `merchant_name` on GET (not `org_id`). SPA title. Do not require One.

### G10. `setVerifying` is unused; query edits without reload do nothing

Documented limitation of a no-router app. Fine for `location.assign`. Not a bug until someone client-navigates.

### G11. Name is never required; `foo` is a usable email

Host `IsUsable` is non-whitespace and not the placeholder. `"foo"` passes. CHIP/Billplz may reject later (503 `… rejected the org key` via `readDetail` now). SPA `type="email"` is browser-dependent.

**How to solve.** Keep host as the source of truth. If a rail 400s a bad mailbox, show `detail`. Do not invent RFC-5322 on the SPA unless both sides share one function.

---

## Buyer vs host contract mismatches

### `email_required` (active/started rail)

**Host.** Computed from `PayProviders.TryNormalize(provider) && RequiresEmail(provider)` where `provider` is `row.Provider` (CheckoutView) or `link.Provider` (LinkView). `RequiresEmail` is **not Stripe and not Test**. Bind-at-mint: the payment link has a provider before the first start. 016’s `ActiveProvider` fallback is **dead** (`Rows.cs` says unused; Start uses `row.Provider ?? link?.Provider`).

**SPA.** `emailBlocked` uses GET `email_required` and `usableEmail` (trim + reject placeholder). Button disable. Click guard is dead code while disabled (B7).

**Match.** Stripe optional, Test optional, four rails required, placeholder rejected on **both** sides. 016 UI hole (placeholder enabled) is **closed**. Prefill hole (B8) remains. Test name `Email_required_true_when_active_chip` is a leftover name, not ActiveProvider behaviour.

**Mismatch.** SPA does not surface `email_required` as copy. Host 400 `"email is required"` is now shown via `readDetail` if the click ever happens (e.g. race: flag true after they already clicked — it cannot, button is disabled). `"foo"` is usable on both; not a mismatch, a shared weakness.

### Start body

**Host.** `{ name?, email?, slot_key? }`. Payment-link start **requires** `slot_key` 8–128. Standalone checkout ignores it. Empty name/email are not written (previous values survive).

**SPA.** Always `{ name, email, slot_key }`. Empty strings are sent. Client resume: if `started && redirect_url`, **no POST**, `location.assign`. Host replay: second POST with same slot returns stored `PspRedirectUrl` without PSP HTTP.

**Spec.** `StartPayRequest` has no `slot_key`. Live SPA/host agree with each other, not with `main.tsp`.

**Match** on replay if `slot_key` is stable (B4 is the exception).

### Verifying poll

**Host.** Success URL query is a string. GET status is the row/link status. Webhook / Test fulfillment writes `paid`. Nothing writes `failed`.

**SPA.** `status=verifying` → hide form, poll 2s × 15, timeout footer. Paid pixel **only** from GET `paid`. Query `status=paid` does **not** paint Paid.

**Match** on the law “success URL is not paid.” 018 timeout footer is new. Remaining holes: B9, B10, Billplz/Razorpay unpaid success URL (below).

### Occupancy / capacity

**Host.** Default `max_payers=1`. `unlimited` → null max. Taken = count of child `open`+`paid`. Same slot resume. Third person on max=2 → 409 `"This pay link is full"`; GET with a fresh slot → `status: "full"`, `remaining: 0`. Max=1 paid → GET without matching slot → **paid view** (B5). Abandoned open seats count (B6).

**SPA.** Sends `slot_key`. Full Card. 409 re-GETs. Does **not** show remaining. Does **not** explain “someone is paying now” vs “already paid.”

**Error copy.** Host 409 detail `"This pay link is full"` or `"Checkout is not open"`. SPA full Card: “This pay link has no remaining payments.” 409 GET-fail fallback: `detail ?? 'This pay link is full'`. Close enough; remaining still hidden.

### Test rail pay

**Host.** No keys. `CreateHostedUrlAsync` returns Success URL. Start fulfills + dummy webhook event. GET then `paid` + Official Receipt. Production `AllowsTest` false → throw `"rail not configured"` → 503.

**SPA.** No secrets UI. Form copy names Test without logos. Redirect to verifying; first GET should already be paid → Paid pixel, not Confirming.

**Match.** Buyer path is local complete. Do not add a “mark paid” button on `:5179` — Start is that button.

### Success ≠ paid

**Host.** Rails set success to `CheckoutUrls.Success` (query verifying) when the row’s `SuccessUrl` is null **or** (payment-link children) already that URL. `Fulfillment.FulfillPaidAsync` is the paid writer. Test calls it inside Start **before** returning the URL — the URL is still not what makes it paid; the fulfill call is.

**SPA.** Verifying copy states the law twice (CardDescription + timeout line). Paid branch keys off GET. `locks.test.ts` asserts `=== 'verifying'` and `pay.status === 'paid'` both exist.

**Match.** The restyle did **not** treat return as paid. It made the law more visible (timeout sentence).

### Start status map (016 vs now)

| Condition | HTTP | Host `detail` | 016 SPA | Live SPA |
|-----------|------|---------------|---------|----------|
| Unknown token | 404 | Checkout not found | `start 404` | `detail` or `start 404` |
| Paid/expired standalone | 409 | Checkout is not open | `start 409` | Re-GET → paid/expired pixels |
| Link full | 409 | This pay link is full | (no occupancy) | Re-GET → full pixel |
| Charges paused | 403 | Org charges are paused | `start 403` | `Org charges are paused` |
| No rail | 503 | rail not configured | `rail not configured` | same (`detail` matches) |
| Stripe bad key | 503 | Stripe rejected the org key | **`rail not configured` (lie)** | **`Stripe rejected the org key`** |
| CHIP/Billplz/Xendit/Razorpay rejected | 503 | `{PSP} rejected the org key` | mashed 503 lie | **host detail** |
| Email unusable | 400 | email is required | mashed 400 | **`email is required`** |
| Billplz localhost callback | 400 | callback base not public | mashed 400 | **`callback base not public`** |
| Link start, no slot | 400 | slot_key is required | n/a | **`slot_key is required`** |
| `Pay:CheckoutBaseUrl` missing (non-Testing, non-Dev) | 503 | Pay:CheckoutBaseUrl is required | n/a | host detail |

**016 #1 ranked mismatch (400 mashup) is fixed in the SPA.** `locks.test.ts` forbids the mashed sentence. All start 400s are **not** collapsed to one sentence; they show `detail`. Fallback `start 400` only if JSON parse fails.

### Billplz / Razorpay cancel looks like success-return

No separate cancel URL in those payloads. Unpaid redirect still has `?status=verifying`. SPA hides Pay and waits for a webhook that will never come → timeout “not paid.” Honest eventual copy; 30s of fake confirming is not.

**How to solve.** Prefer a cancel URL where the rail allows it. For Billplz, document that `redirect_url` is not paid (already). After timeout, B9 escape. Do not parse Billplz query `billplz[paid]=false` in the SPA if Plane B is the source of truth — optional fast-path to cancel pixel only.

### CHIP / Xendit / Stripe cancel

Cancel URL **without** query → form again. If already `started`, “Continue to processor.” Honest. Prefill missing (B8).

### Kernel `success_url` that is not this origin

`POST /v1/checkouts` with `success_url: https://example.test/ok` (README). Test/Stripe send the buyer there. SPA on 5179 is only hop-1. Payment-link mint **overrides** to CheckoutBaseUrl. Merchant Vite path lands here. Kernel path may not. Do not teach `:5178` to paste a kernel success URL onto a WhatsApp link.

---

## Tests that lock vs missing

### Checkout package — what `locks.test.ts` actually locks

Nine `readFileSync` greps. Vitest **node**. No `App` mount. No `fetch` mock. No poll clock.

| Test title | What it actually asserts |
|------------|--------------------------|
| `has no OIDC dependency` | `package.json` text does not contain `oidc-client-ts`, `react-oidc-context`, `@repo/api-types-ts`. **Not** `src`. |
| `does not render wallet tiles or card PAN` | `App.tsx` lowercased does not match wallet regex; does not contain `autocomplete="cc-number"`. Does **not** forbid `cc-csc`. |
| `verifying query is not paid` | `App.tsx` contains `=== 'verifying'` and `pay.status === 'paid'`. Does not execute render order. |
| `polls public GET while verifying` | contains `/v1/pay/` and `setInterval`. No `2000`, no `15`. |
| `does not treat customer@example.com as satisfying email_required` | contains that string and `usableEmail`. Does not run the function (`" customer@example.com ".trim()`). |
| `test processor copy is not a wallet tile` | contains `pay.provider === 'test'` and `No card, no secret`. |
| `uses copied aura-ui card chrome not a Hub portal` | contains `Card`, `Payment received`, `Link expired`, `Link is full`; not `lazuar-portal`. Does not assert `h1`. |
| `sends a local slot_key so one browser is one payer on a shared link` | contains `lazuar-pay-slot:`, `localStorage`, `slot_key`, `pay.status === 'full'`. Does not catch B4. |
| `maps start 400 without calling it paid` | contains `response.status === 400`; does **not** contain `status: 'paid'` or the mashed 400 sentence. Does **not** assert `readDetail`. |

Missing in this file (and nowhere else in the package): Bearer grep on `src`; `:5175`; `whoami`; poll cap; Loading graveyard; `readDetail`; 409 re-GET; `verifyTimedOut`; `formatMoney`; CORS; remaining; Malay; `usableEmail` unit cases.

CI does not run these greps.

### Host tests that pin what the SPA depends on

**Locked**

- GET unauthenticated, One not called on the public path (`PublicPayTests.Public_get_does_not_need_bearer`, `PaymentLinkTests.Public_get_does_not_need_bearer`).
- GET missing 404.
- GET `email_required` true for chip checkout, false for stripe checkout.
- GET `started` + `redirect_url` after start (CHIP).
- Start twice same CHIP URL, one PSP HTTP.
- Start paid → 409.
- Start paused → 403 even with stored URL.
- Start no rail → 503 `rail not configured`.
- CHIP placeholder email → 400 (status; **not** `detail` string). Same pattern in Billplz/Xendit/Razorpay rail tests.
- Test mint+start → `paid`, Official Receipt, `status=verifying` in redirect, no PSP HTTP.
- Payment link default max=1; unlimited null remaining; max 0 → 400; two-of-two; third 409 `full`; same slot no second seat; start without `slot_key` → 400; one-person paid without slot → GET `paid` (this last one **locks B5**).
- CORS 5179 and **4179** on `/health`; 3003/3004 denied.
- Isolation: checkout `package.json` has no `@repo/api-types-ts`.

**Missing (one method per hole; do not implement here)**

- `Public_get_email_required_false_for_test` (RequiresEmail Test exception).
- `Public_get_email_required_true_for_chip_payment_link` (LinkView provider, not checkout row).
- GET occupancy counters JSON on an open limited link (`remaining` / `max_payers`) — host has this on list; public GET shape unasserted as a named SPA-contract test.
- Start 400 `detail == "email is required"` (placeholder tests assert status only).
- Start 400 `detail == "callback base not public"` with `PublicBaseUrl` loopback (factory default is `https://pay.test.example`).
- Start 400 `detail == "slot_key is required"` already exists as contain `"slot_key"`.
- SPA-side: none of the host tests can fail if `App.tsx` maps 400 to the mashed sentence again — **except** `locks.test.ts`, which CI does not run.
- No test that GET `failed` / `expired` is ever produced.
- No CorsTest on `GET /v1/pay/{token}` (policy is global; `/health` is the probe).
- No test that production-missing `Pay:CheckoutBaseUrl` is 503 on start outside Testing.
- No test that `localStorage` failure does not double-mint (would be an SPA test).

---

## Ranked findings

Severity is buyer-facing. Locks that hold are listed after the holes so they are not bargained away.

1. **P0 — CORS + API base are laptop-shaped (B2, B3, G4).** Deployed checkout cannot GET/start unless origins and `VITE_PAY_API_URL` are fixed at build/config. Combined with B1, the pixel is Loading…. **Solve:** config CORS, fail-build without API URL, SPA fallback host, never default production to localhost.
2. **P0 — Loading graveyard (B1, B11).** 014/016/018. **Solve:** error Card + Retry; catch start network.
3. **P1 — Occupancy identity is brittle (B4) and max=1 paid impersonates the visitor (B5).** Private mode double-seats; forwarded one-person link says “Thank you.” **Solve:** in-memory slot cache; host/SPA distinguish mine vs already-paid.
4. **P1 — Abandoned open seats (B6).** SPA “Link is full” while nobody paid. Expired Card is dead. **Solve:** host TTL on `open` children; then the expired pixel is real.
5. **P1 — Verifying timeout is a cul-de-sac (B9) plus Billplz/Razorpay unpaid success URL.** **Solve:** restart poll on Refresh; labeled return-to-pay; do not parse query as paid.
6. **P2 — Email-required mute disable (B7) and no prefill (B8).** Placeholder is finally blocked (016 K11 fixed) but the button does not say why. **Solve:** helper text + prefill from GET.
7. **P2 — Dist stale (B13); CI does not run locks (G6); locks do not grep src Bearer (G7).** Restyle/occupancy can ship in git `dist/` as the old app. **Solve:** gitignore dist; CI `vitest`; src greps.
8. **P2 — `pay-spec` / OpenAPI behind live GET/start (G5).** `slot_key` unspecified. Dist OpenAPI missing even `email_required`. **Solve:** sibling 08; do not generate the SPA from dist yaml.
9. **P3 — Restyle a11y regression (B14); no Malay (G2); no remaining copy (G1); path regex is a prefix match (B12).** **Solve:** `h1` + `aria-live`; real `ms` later; show remaining; `^/c/([^/]+)/?$`.
10. **P3 — Silent missing `redirect_url` (B15); poll vs missing (B10).**

**Fixed since 016 (do not re-open as if live):** mashed 400 sentence; 503 always “rail not configured”; 4179 CORS; placeholder UI; stuck verifying with no timeout UI; no `slot_key`; no full pixel; no Test copy; hardcoded 5179 inside rail files (now `CheckoutUrls`); GET-once (014).

**Holds — fail the program if a later PR bargains these:**

- No OIDC packages, no Bearer on GET/start, missing pixel says “No sign-in.”
- No wallet tiles, no PAN, no provider `<select>`.
- Query `status=verifying` is not paid; Paid pixel is host `status === 'paid'`.
- Test rail: no secrets on this page; Pay marks paid on the host; copy says so.
- Start replay: SPA uses stored `redirect_url`; host does not mint a second PSP session.
- Payment-link occupancy exists on the host; SPA sends `slot_key` and has a full pixel.
- `readDetail` shows host 400/503/403 sentences instead of collapsing 400s.
- Redirect is `location.assign` of a **server-minted** URL.

---

## Refuse

Out of this paper, even when adjacent:

- **Merchant shell chrome** (`:5178` AppSidebar, vault cards, pay-link table UX). Cited only as the copy-link source and as proof that aura-ui was **copied**, not imported as `@repo/aura-ui`.
- **Per-PSP webhook crypto depth** (Stripe `whsec`, CHIP PEM, Billplz X-Signature, Xendit token, Razorpay HMAC). Buyer page polls GET; it does not verify webhooks. Test’s in-process fulfill is in scope as the buyer-visible Test path; HMAC is not.
- **TypeSpec full paper.** `pay-spec` vs live vs SPA is recorded as G5 / mismatches. The compile graph, OpenAPI staleness root cause, and whether to generate `@repo/pay-types` belong in 019/08.
- **Hub portal retarget** to 8081. Still refuse. This origin is `:5179`.
- **OIDC on checkout** as a “later login.” Fail lock. Receipts later = magic link to the **payer mailbox**, still this origin, still no Zitadel.
- **PAN / FPX tiles on `:5179`.** Wrap-rails. Wallets live on the processor page.
- **Treating Test as a production rail.** Host already disables it in Production. SPA must not grow a secret field for Test.
- **Flipping 011/11 NP-CHK-005/006/007** from this evaluation. Pixels exist; lived dogfood and production CORS/hosting do not.

---

## Appendix: quoted evidence

### A. SPA runtime (route, slot, API, poll, start, pixels)

```8:45:apps/lazuar-pay-checkout/src/App.tsx
const payApi = import.meta.env.VITE_PAY_API_URL ?? 'http://localhost:8081'

type PayView = {
  token: string
  amount: number
  currency: string
  status: string
  email_required?: boolean
  started?: boolean
  provider?: string | null
  redirect_url?: string | null
}

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

function tokenFromPath(): string | null {
  const m = window.location.pathname.match(/^\/c\/([^/]+)/)
  return m ? decodeURIComponent(m[1]) : null
}

function verifyingQuery(): boolean {
  return new URLSearchParams(window.location.search).get('status') === 'verifying'
}
```

Paid vs verifying vs full (order 3 / 5 / 6):

```188:277:apps/lazuar-pay-checkout/src/App.tsx
  if (pay.status === 'paid') {
    return (
      <Shell>
        <Card>
          <CardHeader className="text-center">
            …
            <CardTitle className="text-xl">Payment received</CardTitle>
            …
              Thank you. The merchant will see an Official Receipt, not an e-invoice. This page is not a membership
              login.
```

```226:238:apps/lazuar-pay-checkout/src/App.tsx
  if (pay.status === 'full') {
    return (
      <Shell>
        <Card>
          …
            <CardTitle className="text-xl">Link is full</CardTitle>
            <CardDescription>This pay link has no remaining payments.</CardDescription>
```

```242:277:apps/lazuar-pay-checkout/src/App.tsx
  if (verifying && pay.status !== 'paid') {
    return (
      <Shell>
        <Card>
          …
            <CardTitle className="text-xl">Confirming payment</CardTitle>
            <CardDescription>
              The processor success URL is not paid. Waiting for the webhook.
            </CardDescription>
          {verifyTimedOut ? (
            <CardFooter className="flex-col gap-3">
              <p className="text-center text-sm text-slate-500">Not paid yet. The success URL is not paid.</p>
              <Button
                type="button"
                variant="outline"
                className="w-full"
                onClick={() => {
                  setVerifyTimedOut(false)
                  void fetch(payPath(token))
                    .then((r) => (r.ok ? r.json() : null))
                    .then((body: PayView | null) => {
                      if (body) setPay(body)
                    })
                }}
              >
                Refresh status
              </Button>
```

`usableEmail` + `readDetail` (016 K11 + 400 mashup fixes):

```339:352:apps/lazuar-pay-checkout/src/App.tsx
function usableEmail(value: string): boolean {
  const trimmed = value.trim()
  return trimmed.length > 0 && trimmed.toLowerCase() !== 'customer@example.com'
}

async function readDetail(response: Response): Promise<string | null> {
  try {
    const clone = response.clone()
    const body = (await clone.json()) as { detail?: string }
    return body.detail?.trim() || null
  } catch {
    return null
  }
}
```

### B. Host public GET/start, occupancy, email, Test, URLs

`RequiresEmail` including Test:

```35:36:apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs
    public static bool RequiresEmail(string provider) =>
        provider is not Stripe and not Test;
```

BuyerEmail placeholder (SPA `usableEmail` is the same decision):

```3:9:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/BuyerEmail.cs
public static class BuyerEmail
{
    public const string Placeholder = "customer@example.com";

    public static bool IsUsable(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && !string.Equals(email.Trim(), Placeholder, StringComparison.OrdinalIgnoreCase);
```

Minted success/cancel for payment-link children (these **land on** `:5179`):

```244:260:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
        var baseUrl = CheckoutUrls.Base(config, env);
        var row = new CheckoutRow
        {
            …
            SuccessUrl = baseUrl + "/c/" + link.PublicToken + "?status=verifying",
            CancelUrl = baseUrl + "/c/" + link.PublicToken,
            CreatedAt = DateTimeOffset.UtcNow
        };
```

Start replay (stored hosted URL):

```151:155:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
        if (!string.IsNullOrWhiteSpace(row.PspRedirectUrl))
        {
            await db.SaveChangesAsync(ct);
            return Results.Json(new { redirect_url = row.PspRedirectUrl }, OneClient.Json);
        }
```

Test fulfill-on-start:

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

Fulfillment is the only `paid` writer (never `expired` / `failed`):

```26:37:apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs
        if (checkout.Status != "open")
        {
            return;
        }
        …
        checkout.Status = "paid";
```

### C. Merchant URL that buyers open

```40:49:apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx
function checkoutOrigin(): string {
  return ((import.meta.env.VITE_CHECKOUT_ORIGIN as string | undefined) ?? 'http://localhost:5179').replace(
    /\/$/,
    '',
  )
}

function buyerUrl(token: string): string {
  return `${checkoutOrigin()}/c/${token}`
}
```

Mint body has `max_payers` / `unlimited`, **no** `success_url` (host stamps CheckoutBaseUrl).

### D. Locks file (entire contract the checkout package tests)

See `apps/lazuar-pay-checkout/src/locks.test.ts` lines 8–69 as opened in full above. Nine greps, node env, no render.

### E. CORS + CI

`Program.cs` 59–72 (eight laptop origins).  
`CorsTests.Health_allows_checkout_origin` / `Health_allows_preview_checkout_origin` / deny 3003/3004.  
CI `.github/workflows/ci.yml` 111–116: host `dotnet test` + checkout **build**, not `vitest`.

### F. Spec vs live start body

```59:72:packages/pay-spec/main.tsp
model PublicPay {
  token: string;
  amount: decimal;
  currency: string;
  status: string;
  email_required?: boolean;
  started?: boolean;
  redirect_url?: string;
}

model StartPayRequest {
  name?: string;
  email?: string;
}
```

Live `StartPayRequest` also has `SlotKey`. Live GET also has `provider`, occupancy counters, `payer_name`, `payer_email`. `packages/pay-spec/dist` has no `email_required` string.

### G. 016 mashed 400 (historical; **not** live)

016 quoted:

```84:94:apps/lazuar-pay-checkout/src/App.tsx
      if (response.status === 503) {
        setError('rail not configured')
        return
      }
      if (response.status === 400) {
        setError('callback base not public or email required')
        return
      }
```

Live `App.tsx` 141–148 uses `readDetail`. `locks.test.ts` 63–68 asserts the mashed sentence is **absent**. Re-verify against source, not against 016.

---

**One-paragraph verdict.** On `feat/018-merchant-shell` (`9f04ad58`), `:5179` is still a public, no-OIDC, no-tile, no-PAN cash-register pixel that calls only `GET/POST /v1/pay/{token}`. 018 copied aura-ui Card chrome, added `slot_key` occupancy, Test copy, a verifying timeout footer, `readDetail` (so start 400s are **not** one mashed sentence), CORS for preview 4179, and `CheckoutUrls` so success/cancel land on `Pay:CheckoutBaseUrl`. Success URL is **not** treated as paid. Test pays in Start and the Paid pixel keys off GET `paid`. The restyle did not add Bearer or OIDC. It did **not** fix the Loading graveyard, laptop-only CORS, hardcoded `localhost:8081` fallback, private-mode slot rotation, max=1 “Thank you” to strangers, abandoned open seats, mute email-required disable, stale `dist/`, or the absence of Malay / remaining-seat copy. Locks tests are still greps that CI does not run. Live `App.tsx` and `PublicPayEndpoints.cs`, not 014/016 checkboxes, are the evidence.
