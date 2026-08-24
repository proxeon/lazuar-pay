# 03 — Checkout Vite (`lazuar-pay-checkout` `:5179`): hosted buyer pay page, no One account

**Date:** 24 August 2026  
**Program:** [014-evals](./README.md) — evaluate new Lazuar Pay, then port Hub gateway adapters as HTTP judgment  
**Slice:** current state of `apps/lazuar-pay-checkout` (Vite **`:5179`**). Hosted buyer pay page. Buyers **MUST NOT** need a One / Zitadel account. How start-pay redirect to Stripe (or later PSP) works. Success / cancel / verifying honesty.  
**Kind:** uncondensed evaluation. **Not** an implementation. **Not** a product-code change. **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md) cells. **Not** a project reference into `apps/lazuar-api`.  
**Audience:** the parent 014 judgment (`00-evaluation.md`) and anyone about to treat K10–K22 checkboxes as equivalent to a production cash register.

This paper is about **the buyer plane in the browser** plus the **public** 8081 doors it actually calls. It does not re-derive Stripe webhook HMAC, journal balance, `RCPT-` numbering, BYOK encryption, or merchant OIDC. Those live in sibling 014 reports (`01-new-pay-host`, `02-merchant-frontend`, `05-stripe-port`, `08-webhooks-secrets-fulfillment`). It does pin the **fail locks** those papers must not violate from this origin: no Zitadel on checkout, no Pay password form, wrap-rails honesty, success/cancel URLs are not fulfillment, never treat setup-intent as paid, never render wallet / FPX tiles ourselves.

**Live code is authority.** [013-prods/05-checkout-frontend.md](../013-prods/05-checkout-frontend.md) was written on 21 August 2026 when this app was a **health probe**. Every sentence in that paper that says “there is no public GET”, “Vite only fetches `/health`”, “fixture is always `open`”, or “do not claim this is a cash register” is **historical**. Re-read this file against `ee2db8e5`, not against `6f866ff0`.

---

## Repos and SHAs (as read)

| Repo | Path | Branch | Full SHA | Short | Tip |
|------|------|--------|----------|-------|-----|
| **Lazuar Pay** (this tree) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` | `main` | `ee2db8e5758305089a38298456c456d6bf0e97ca` | `ee2db8e5` | `feat(pay): Bar B receipts, webhook secret, merchant money UI` |

`git rev-parse HEAD` / `git log -1 --oneline` as recorded for this slice:

```text
ee2db8e5 feat(pay): Bar B receipts, webhook secret, merchant money UI
```

014 index ([README.md](./README.md)) pinned the same SHA at analysis start. 013/05 was pinned to `6f866ff0` on `feat/012-connect-one` (“scaffold merchant and checkout Vite apps”). Do not flatten those two SHAs.

**.NET SDK pin (Pay host):** `10.0.x` in `.github/workflows/ci.yml`. **pnpm pin:** `pnpm@11.5.2`. Checkout Vite is `vite ^8.2.0`, React `^19.2.8`. Portal (museum) is still Next on port **3004**. Merchant Vite is **5178** with OIDC. Checkout Vite is **5179** with **no** OIDC.

**What “Pay” means in this paper**

- The **new focused host** is `apps/lazuar-pay` (`Lazuar.Pay`) on **http://localhost:8081**. Postgres on **5435**. Public buyer doors: `GET /v1/pay/{token}`, `POST /v1/pay/{token}/start`. Merchant doors stay member-gated. Stripe hosted rail + PSP webhook + same-handler fulfillment exist on this SHA.
- The **new buyer origin** is `apps/lazuar-pay-checkout` on **http://localhost:5179**. This is no longer a health card. It is a `/c/{token}` cash-register pixel that fetches public pay and redirects to Stripe.
- The **new merchant origin** is `apps/lazuar-pay-merchant` on **http://localhost:5178**. OIDC. Mints the shareable URL. Not this paper’s SPA, except where it copies `http://localhost:5179/c/{public_token}`.
- The **old Hub portal** is `apps/lazuar-portal` on **http://localhost:3004**. Next.js, Hub cookie `lazuar_auth`, Hub `/public/commerce/*`. **Do not retarget at 8081.** Steal judgment, not routes.
- The **identity plane** is Lazuar One API on **8080** `/api/v1` and product login **`:5175`**. Checkout Vite must not call those. Merchant Vite does.

If a sentence does not say **focused Pay** vs **old Hub** vs **One**, assume it is wrong.

---

## Locked (do not bargain in later PRs)

From 011/01, 011/03, 011/11, 011/12, 012/p50, 012/p60, 013 B00, and this program’s README:

| Lock | ID | Meaning for `:5179` |
|------|----|---------------------|
| Buyers are not One/Zitadel humans | **NP-XX-013**, **NP-CHK-007** | Fail if this page asks for Zitadel / `:5175` login. Cardholders never become Zitadel users because they bought an ebook. |
| No Pay password form | **NP-XX-007** | No `/login`, no email+password, no Hub `POST /one/auth/login`. |
| Hosted cash register | **NP-CHK-005** | A buyer-facing pay page on Pay’s origin, not Hub `:3004`. |
| Shareable pay link | **NP-CHK-006** | A URL a merchant can paste into WhatsApp. Locked shape: `http://localhost:5179/c/{token}` ([B00](../013-prods/checklists/decisions.md)). |
| Payer email/name on the session | **NP-BUY-001** | The checkout row in Pay holds who paid. Not a One membership. |
| Success/cancel URLs are not fulfillment | **NP-CHK-002** | Stored on the session. Webhook/handler writes subscription + journal + `RCPT-`. |
| Do not retarget `lazuar-portal` to 8081 | **P60** | Old portal speaks Hub `/public/commerce/*` + `lazuar_auth`. 8081 will 401/404, then someone will “just add login.” |
| Wrap-rails honesty | **NP-GW-007** | Stripe/CHIP can auto-charge **if vaulted**. Billplz-class = reminder + hosted link, never silent debit. The page must say so. Bar B first rail is **Stripe hosted** (`capability = "hosted_link"`). |
| Never treat setup-intent as paid | **NP-GW-008** | A `$0` Stripe Checkout `mode=setup` that collected a PM is not a capture. Query `?status=verifying` is not paid. |
| Public `/v1/pay/{token}` only | B00 | No merchant Bearer on this app. Do **not** ungated `GET /v1/checkouts/{id}`. |
| Wrap-rails pixel | NP-GW-007 / 013/05 §6 | This page **starts a hosted PSP session**. It does **not** collect raw PAN. It must **never** render GrabPay / TnG / FPX bank tiles itself. |
| Receipts / update-payment later share this origin | 011/01 buyer plane, **NP-BUY-003…005** | Magic link to the **payer mailbox**, not merchant `:5178`. |

011/11 on this SHA still marks **NP-CHK-005 / 006 / 007 as `todo`**. 013 K-track checklists mark K10–K22 `[x]`. Those two facts can both be true: the pixels exist, the dogfood sentence has not been lived, and several K boxes over-claim relative to live files. This paper does **not** flip 011. It records the disagreement.

---

## 1. Method / files opened

Nothing was implemented. The following were opened in full or in the cited ranges.

### 1.1 Production target (focused Pay, live)

| Path | Why |
|------|-----|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-checkout/package.json` | Port 5179, `strictPort`, React 19 only. No router, no OIDC, no `@repo/api-types-ts`. |
| `…/README.md` | “Buyers have no One account. Fail if this page asks for Zitadel login.” |
| `…/vite.config.ts` | Dual-pin 5179; never silently steal merchant 5178. Preview 4179. |
| `…/vitest.config.ts` | Node env; `src/**/*.test.ts`. |
| `…/index.html`, `src/main.tsx`, `src/App.tsx`, `src/App.css`, `src/index.css`, `src/locks.test.ts` | Entire runtime. |
| `…/.env.example` | `VITE_PAY_API_URL=http://localhost:8081` only. |
| `…/tsconfig.json`, `tsconfig.app.json`, `tsconfig.node.json` | Bundler mode; no path aliases into Hub. |
| `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` | CORS 5178/5179; `MapPublicPay`; `MapCheckouts`; `MapWebhooks`. |
| `…/PublicPay/PublicPayEndpoints.cs` | `GET /v1/pay/{token}`, `POST /v1/pay/{token}/start`. |
| `…/Checkouts/CheckoutEndpoints.cs`, `CheckoutSession.cs`, `CheckoutStore.cs`, `CreateCheckoutRequest.cs` | Merchant create still MemberGate; mints `public_token`. Postgres-backed. |
| `…/Gateways/StripeHosted.cs` | Hosted Checkout `mode=payment`; default success/cancel back to `:5179`. |
| `…/Gateways/WebhookEndpoints.cs`, `Money/Fulfillment.cs` | Plane B writes `paid` + `RCPT-`. Vite never does. |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPayTests.cs`, `CheckoutTests.cs`, `CorsTests.cs`, `IsolationTests.cs`, `WebhookTests.cs` | What is actually pinned. |
| `packages/pay-spec/main.tsp` | `PublicPay` + `PublicPayApi`. |
| `apps/lazuar-pay-merchant/src/pages/WorkspacePage.tsx` | Staff copies `http://localhost:5179/c/${public_token}`. |
| `apps/lazuar-pay-merchant/src/App.tsx`, `package.json`, `README.md` | Contrast: OIDC on 5178, **not** on 5179. |

### 1.2 Product law and historical papers

| Path | Why |
|------|-----|
| `plans/011-new-lazuar-pay/11-checklist.md` | NP-CHK-005/006/007, NP-BUY-001 still `todo`. |
| `plans/011-new-lazuar-pay/12-first-slice-tracker.md` | Steps 9–10 still `todo`. |
| `plans/013-prods/05-checkout-frontend.md` | Historical health-probe paper. Authority **until** it disagrees with live files. |
| `plans/013-prods/checklists/k10-public-pay-get.md` … `k22-checkout-runbook.md` | All `[x]`. Spot-check vs live. |
| `plans/013-prods/checklists/decisions.md` | B00: public identifier is `token`; URL is `/c/{token}`; Stripe first rail. |
| `plans/013-prods/checklists/g16-psp-hosted-session.md`, `g17-redirect-url.md`, `g15-wrap-rails-label.md` | Hosted session + `{ redirect_url }`. |
| `plans/013-prods/checklists/f11-open-to-paid.md`, `f15-not-tax-invoice.md`, `f20-get-receipt.md` | Paid writer; receipt is merchant-side; buyer download is Bar C. |
| `plans/013-prods/checklists/q10-isolation-vite.md`, `q12-ci-vite-build.md`, `q15-cors-still-denies-ops.md` | Isolation + CI + CORS deny 3003/3004. |
| `plans/013-prods/checklists/b99-bar-b-done.md` | Buyer opens `:5179/c/{token}` **without** a One account — still unchecked. |
| `plans/014-evals/README.md` | This slice’s charter. |

### 1.3 What was not opened as SoT

- One `apps/lazuar-app` OIDC config — merchant paper.
- Live Stripe Dashboard click-through on 24 Aug 2026 — this is a code-authority paper, not a dogfood diary. B99 remains unchecked.
- Hub `PublicCheckoutEndpoints.cs` line-by-line — 013/05 and 009-01 already mapped hop-1 HTTP. Steal the **pixel law**, not the cathedral.
- `examples/hub-cashier-next` — judgment (success_url is not paid) still stolen; app not copied.

---

## 2. Historical 013/05 vs live `ee2db8e5` (do not reuse the stop lines)

013/05 Appendix C said the entire checkout Vite was a health probe. 013/05 Stop lines:

> Do not claim `lazuar-pay-checkout` is a cash register. It is a health probe on `:5179`.  
> Do not claim `GET /v1/checkouts/{id}` is the buyer door. It is member-gated.  
> Do not claim NP-CHK-005/006/007 or NP-BUY-001 are `done`.

The **second** sentence is still true (merchant GET is still MemberGate). The **third** is still true in 011/11 (cells unflipped; this paper will not flip them). The **first** is **false** on this SHA.

| 013/05 claim at `6f866ff0` | Live at `ee2db8e5` |
|---------------------------|---------------------|
| `App.tsx` fetches `${payApi}/health` and paints origin / API / health `<dl>` | `App.tsx` parses `/c/{token}`, fetches `GET /v1/pay/{token}`, posts `POST /v1/pay/{token}/start`, `window.location.assign(redirect_url)` |
| No path token | `tokenFromPath()` `/^\/c\/([^/]+)/` |
| No payer fields | Name + email inputs; POST body `{ name, email }` |
| No public GET | `PublicPayEndpoints.Get` — no Bearer |
| No start | `PublicPayEndpoints.Start` → StripeHosted |
| Fixture always `"open"`; in-memory `ConcurrentDictionary` | Postgres `checkouts`; status becomes `"paid"` in `Fulfillment.FulfillPaidAsync` |
| No `public_token` | Minted on merchant create; unique index |
| No tests in the Vite app | `src/locks.test.ts` (package.json bans) |
| No `.env.example` | `.env.example` with `VITE_PAY_API_URL` only |
| CorsTests only `/health` | **Still** only `/health` (K14 over-claims) |
| IsolationTests Vite ban “keep” as a wish | `IsolationTests.Vite_apps_do_not_use_hub_types` exists |

The 013 paper’s **architecture recommendation** (option B: separate `public_token`, buyer-safe DTO, click-time hop-2, no OIDC) **landed**. The 013 paper’s **honesty gaps for the pixel** (verifying poll, wrap-rails copy, required email, receipts for the buyer, expired writer) **mostly did not**. Treating K10–K22 `[x]` as “Bar B cash register is done” would be the same class of lie 013 warned against when the app was a health probe.

---

## 3. What checkout Vite is today

### 3.1 Package and listen

`apps/lazuar-pay-checkout/package.json` at `ee2db8e5`:

| Field | Value |
|-------|--------|
| `name` | `lazuar-pay-checkout` (private `0.0.0`) |
| `dev` | `vite --port=5179 --host=0.0.0.0 --strictPort` |
| `preview` | `vite preview --port=4179 --strictPort` |
| `build` | `tsc -b && vite build` |
| `lint` | `oxlint` |
| `test` | `vitest run` |
| `check-types` | `tsc -b` |
| Runtime deps | `react`, `react-dom` **only** |
| Dev deps | Vite 8, `@vitejs/plugin-react`, TypeScript ~6, `@types/node`, `@types/react`, `@types/react-dom`, oxlint, **vitest** |
| Not present | `oidc-client-ts`, `react-oidc-context`, `react-router-dom`, `openapi-fetch`, `@repo/api-types-ts`, `@lazuar/one-client`, cookie helpers, `@stripe/stripe-js`, CHIP.js, Billplz JS |

Merchant `:5178` **does** depend on `oidc-client-ts`, `react-oidc-context`, `react-router-dom`. That split is the plane lock. Do not “share” those deps into checkout because both apps are Vite.

`vite.config.ts` dual-pins the same port and comments the footgun:

```ts
// Dual-pinned with package.json `vite --port=5179`.
// strictPort: fail loud if 5179 is busy — never silently steal merchant :5178.
export default defineConfig({
  plugins: [react()],
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

`vitest.config.ts`:

```ts
export default defineConfig({
  test: { environment: 'node', include: ['src/**/*.test.ts'] },
})
```

There is **no** jsdom / happy-dom environment. `locks.test.ts` is a filesystem grep of `package.json`, not a render of `App.tsx`. A future Playwright “no Zitadel” e2e is still absent. That is allowed; claiming K17.3 “Runtime (if you e2e)” as proven is not.

`task pay:checkout` in `Taskfile.yml`:

```yaml
  pay:checkout:
    desc: Hosted buyer pay page on http://localhost:5179 (not lazuar-portal)
    cmds:
      - pnpm --filter lazuar-pay-checkout dev
```

`pnpm-workspace.yaml` includes `apps/*`. `turbo.json` has a generic persistent `dev`; no special checkout task. CI (`.github/workflows/ci.yml` job `pay`) runs:

```yaml
      - name: Build merchant and checkout
        run: |
          pnpm --filter lazuar-pay-merchant build
          pnpm --filter lazuar-pay-checkout build
```

after `dotnet test apps/lazuar-pay/Lazuar.Pay.slnx`. Q12’s “filter those two packages only” is live. The Hub `dotnet` job is a **different** job (`working-directory: apps/lazuar-api`). A broken `App.tsx` can no longer hide behind Hub green. The checkout `test` script is **not** invoked in CI — only `build` (`tsc -b && vite build`). `locks.test.ts` is therefore a local `pnpm --filter lazuar-pay-checkout test` pin, not a PR pin, unless someone runs it by hand.

`index.html` title remains `Lazuar Pay — checkout`. One `#root`. `src/main.tsx` is `StrictMode` + `<App />`. No router package. Vite’s SPA fallback serves `index.html` for `/c/{token}` in `dev` / `preview`. Production static hosting must do the same (object storage 404 on `/c/…` is a cutover bug, not a Vite bug).

`.env.example`:

```
# Focused Pay host. Never Hub :8080. Never point lazuar-portal here.
VITE_PAY_API_URL=http://localhost:8081
```

Forbidden names from 013/05 §8.4 are still absent: no `VITE_API_URL` Hub prefix, no `VITE_ONE_AUTHORITY`, no `VITE_OIDC_CLIENT_ID`, no `VITE_PORTAL_URL`, no `VITE_STRIPE_*`. Keep it that way. Vite inlines `VITE_*` into the buyer bundle.

### 3.2 Entire UI (`src/App.tsx`)

There is still a single component. The whole buyer runtime is this file. Quote it in behavioural order, not as a screenshot.

**API origin and path token**

```tsx
const payApi = import.meta.env.VITE_PAY_API_URL ?? 'http://localhost:8081'

type PayView = {
  token: string
  amount: number
  currency: string
  status: string
}

function tokenFromPath(): string | null {
  const m = window.location.pathname.match(/^\/c\/([^/]+)/)
  return m ? decodeURIComponent(m[1]) : null
}
```

`PayView` is a **hand-written subset** of the live public JSON. The host also returns `payer_name` and `payer_email`. The SPA ignores them. There is no `@repo/pay-types-ts`. That is still allowed (012/04: generate from pay-spec only when the UI calls `/v1` for real — it now does, but types were not generated). Drift risk: if the host adds `merchant_name` / `payer_required` / `is_reminder_only`, TypeScript will not notice.

Query string is **not** parsed. StripeHosted’s default success URL is `/c/{token}?status=verifying`. The SPA does not read `status`. See §7.

**GET on mount — no Bearer**

```tsx
  useEffect(() => {
    if (!token) {
      setError('missing')
      return
    }
    fetch(`${payApi}/v1/pay/${token}`)
      .then((r) => {
        if (r.status === 404) throw new Error('missing')
        if (!r.ok) throw new Error(`status ${r.status}`)
        return r.json()
      })
      .then((body: PayView) => setPay(body))
      .catch((err: unknown) =>
        setError(err instanceof Error ? err.message : 'error'),
      )
  }, [token])
```

Honesty that holds:

- No `Authorization` header.
- No `credentials: "include"`.
- 404 → `'missing'` → missing pixel (see below).
- Default API is **8081**, not Hub `8080/api/v1`.

Honesty that does **not** hold:

- Non-404 failure (`500`, CORS, network `Failed to fetch`) sets `error` to something other than `'missing'` while `pay` stays `null`. The render order is:

```tsx
  if (error === 'missing' || !token) { /* missing pixel */ }
  if (!pay) {
    return <p>Loading…</p>
  }
```

  So a dead 8081 or a 503 on GET paints **Loading… forever**. There is no error pixel for “Pay is down.”
- GET is once. There is **no poll**. Returning from Stripe with `?status=verifying` while the webhook has not yet flipped `paid` re-fetches once, sees `open`, and paints the **Pay form again**.

**POST start and redirect**

```tsx
  async function startPay() {
    if (!token) return
    setBusy(true)
    try {
      const response = await fetch(`${payApi}/v1/pay/${token}/start`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, email }),
      })
      if (response.status === 503) {
        setError('rail not configured')
        return
      }
      if (!response.ok) {
        setError(`start ${response.status}`)
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

Honesty that holds:

- Still no Bearer.
- 503 is mapped to the host’s honest stub string `rail not configured` (K12.2). The host really does throw `InvalidOperationException("rail not configured")` when BYOK is missing (`StripeHosted`).
- Redirect is `window.location.assign` of a **server-minted** URL. The SPA does not talk to Stripe.js. PAN is collected on Stripe’s origin. Wrap-rails mode A (013/05 §6.2) landed.

Honesty that does **not** hold:

- **Every** 503 becomes `'rail not configured'`, including `Stripe rejected the org key` and `Stripe returned no URL`. The buyer cannot tell “Ada never pasted `sk_test_`” from “Ada pasted a revoked key.”
- 409 (`Checkout is not open`) and 403 (`Org charges are paused`) become `start 409` / `start 403`. 403 on the **public** door is not an auth wall (it is charges-paused), but the pixel does not say that. K13 forbade 401/403 that imply “please log in.” The status code is still 403.
- Empty `name` / `email` are posted. The button is not disabled. The host persists only non-whitespace. Hop-2 can start with no mailbox. K18.1 “required before hop-2” is **not** live.
- If JSON 200 lacks `redirect_url`, the function returns silently. No alert.
- Double-click: `busy` disables the button after the first click, which is a **UI** debounce, not start-idempotency. A second tab, or a return-to-open form after Stripe success, calls `CreateAsync` again. See §6.5.

**Pixels**

Missing (no token, or GET 404):

```tsx
        <p className="kicker">Lazuar Pay</p>
        <h1>Checkout</h1>
        <p>This payment link is not valid. No sign-in.</p>
```

That last sentence is the NP-CHK-007 pixel. There is no Sign in button, no `:5175` link, no Hub lock-icon “use the magic links sent to your email.” Good.

Loading: `<p>Loading…</p>` — also the accidental graveyard for non-404 GET failures.

Paid (`pay.status === 'paid'`):

```tsx
        <h1>Paid</h1>
        <p>
          Thank you. This page is not a membership login. The merchant will see
          an Official Receipt.
        </p>
```

Honesty: not a membership; title is not Tax Invoice; access is not granted. Gap: the **buyer** cannot see the `RCPT-` number or download anything (NP-BUY-005 is Bar C, F20 is merchant-gated). The copy promises the merchant will see a receipt — true only because this branch is keyed off host `status === 'paid'`, which `Fulfillment` writes in the same transaction as `Documents`. If someone later paints “Paid” from the query string, this sentence becomes a lie.

Expired (`pay.status === 'expired'`):

```tsx
        <h1>Expired</h1>
        <p>This payment link is no longer open.</p>
```

The branch exists. The **host never writes `expired`**. Grep of `apps/lazuar-pay/**/*.cs` for `expired` hits only the Start refuse:

```csharp
        if (session.Status is "paid" or "expired")
        {
            return PayErrors.Status(409, "Conflict", "Checkout is not open");
        }
```

No TTL job, no `expires_at` column, no merchant void. NP-CHK-004 is `open → paid / expired`. Live is `open → paid` only (F11). The expired pixel is a costume.

Open (everything else, including `"open"` and any future unknown string):

```tsx
        <p className="kicker">Lazuar Pay</p>
        <h1>Checkout</h1>
        <p>
          {pay.amount} {pay.currency}. Buyers have no One account. Completing
          payment on the processor is not the same as a success URL.
        </p>
        {error && <p role="alert">{error}</p>}
        {/* Name + Email inputs */}
        <button type="button" disabled={busy} onClick={() => void startPay()}>
          Pay
        </button>
```

Amount is the raw decimal + currency code (`10 MYR`, not `RM 10.00`). Merchant display name is absent (K11.1 “if you have it” — the host does not return it). Wrap-rails sentence is the NP-CHK-002 line (“success URL is not paid”), **not** NP-GW-007 (“you will complete payment on Stripe; we cannot auto-debit a Billplz method”). Bar B is Stripe-only so the missing Billplz warning is not a live lie **yet**. Adding CHIP/Billplz without a `capability` / `is_reminder_only` field on the public DTO will make this paragraph a lie.

There is **no** verifying spinner. There is **no** cancel-amber banner. There is **no** timeout “still confirming.” K16 and K19 checkboxes claiming those pixels exist are false vs `App.tsx`.

### 3.3 CSS leftovers

`App.css` still defines `dl` / `dt` / `dd` grid from the health-probe card. `App.tsx` no longer renders a `<dl>`. `index.css` is system-ui on `#f6f5f3`. Fine. Evidence that 013’s scaffold was edited in place, not replaced by a Hub portal clone. Keep it that way — do not import `apps/lazuar-portal/components/ui/*`.

### 3.4 Isolation that already holds (keep)

| Check | Evidence |
|-------|----------|
| No Hub OpenAPI types | `package.json` runtime deps: react only. `locks.test.ts` asserts no `@repo/api-types-ts`. `IsolationTests.Vite_apps_do_not_use_hub_types` reads both Vite `package.json` files and fails on `@repo/api-types-ts`. |
| No OIDC client | `locks.test.ts` asserts no `oidc-client-ts`, no `react-oidc-context`. Merchant package.json **has** both — the test is per-app. |
| No Hub cookie name | No `lazuar_auth` string in `apps/lazuar-pay-checkout/src`. |
| No One login URL | README forbids Zitadel; missing pixel says “No sign-in”; no `/callback` route. |
| Port collision | `strictPort` 5179 vs merchant 5178 vs One login 5175 vs One app 5174 vs Hub portal 3004 vs Hub ops 3003. |
| No Stripe.js / Elements / FPX grid | No such imports. Pay button is one `<button>Pay</button>`. |
| Pay host isolation | IsolationTests still ban `lazuar-api` / MediatR / `Modules.` / `BuildingBlocks` in the C# host. Vite is a separate package. |

`locks.test.ts` in full:

```ts
describe('checkout honesty', () => {
  it('has no OIDC dependency', () => {
    const pkg = readFileSync(join(root, 'package.json'), 'utf8')
    expect(pkg).not.toContain('oidc-client-ts')
    expect(pkg).not.toContain('react-oidc-context')
    expect(pkg).not.toContain('@repo/api-types-ts')
  })
})
```

It does **not** grep `src/` for `zitadel`, `whoami`, `/callback`, or `Authorization`. K17.2 “Grep checkout `src` + `package.json` + `.env*`” is a human checkbox, not an automated one. Live `src` grep on this SHA: the only `login` hit is the paid copy “not a membership login.” No `whoami`, no `zitadel`, no `oidc`.

Q10.2 also said IsolationTests should fail if either Vite package contains `MediatR` or `apps/lazuar-api`. Live `Vite_apps_do_not_use_hub_types` only asserts `@repo/api-types-ts`. The C# IsolationTests still scan `*.cs` / `*.csproj`. The Vite widening is narrower than the checklist.

### 3.5 Contrast with merchant Vite (`:5178`)

| | Merchant `:5178` | Checkout `:5179` |
|--|------------------|------------------|
| Who | One human (Ada, invited MEMBER) | Cardholder / invoice payer |
| Auth | OIDC PKCE → Bearer to Pay `/v1/whoami` | **None.** Public read + pay. |
| Router | `react-router-dom`: `/callback`, `/login`, `/o/:orgId` | Regex `/c/{token}` inside `App.tsx` |
| Calls One? | Yes, indirectly (login `:5175`, then Pay whoami → One `/me`) | **Never** |
| Mints money? | `POST /v1/checkouts` with Bearer; copies pay URL | Consumes the token; does not choose `org_id` or `amount` |
| Fail lock | Password form, `id_token` as Bearer, Hub ops routes | Zitadel login appearing, Hub cookie, `/public/commerce` |
| CORS on 8081 | Yes (`localhost` + `127.0.0.1` twins) | Yes (same policy) |

**P10 trap, still live.** Registering **checkout** as a One app, adding `http://localhost:5179/callback` to `REDIRECT_ALLOWLIST`, or mounting `oidc-client` on this Vite, **is NP-CHK-007 failing**. OIDC is a **merchant** job. Checkout’s lack of OIDC is the finished state, not a backlog item. Merchant `README.md` documents `:5178/callback` only. Keep 5179 off that list.

Merchant `WorkspacePage` is how the shareable URL is born:

```tsx
    const checkout = await payFetch(token, '/v1/checkouts', {
      method: 'POST',
      orgHint: orgId,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        org_id: orgId,
        amount: Number(amount),
        currency: 'MYR',
      }),
    })
    // ...
    const body = (await checkout.json()) as { public_token?: string }
    if (body.public_token) setPayUrl(`http://localhost:5179/c/${body.public_token}`)
```

and the chrome:

```tsx
          {payUrl && (
            <p>
              Buyer (no One account): <a href={payUrl}>{payUrl}</a>
            </p>
          )}
```

The link is hardcoded to `http://localhost:5179`. There is no `pay_url` field from the host. NP-CHK-006 “shareable pay link” is a **concatenation in the merchant SPA**, not a first-class API field. That is enough for laptop dogfood. It is not enough for a production merchant who should copy `https://pay.example/c/{token}`.

Product create and checkout create are **two POSTs**. `CheckoutRow.ProductId` exists in the table and is **not** set by `CheckoutEndpoints.Create`. Stripe line-item name is the literal `"Pay"`. The buyer never sees Ada’s product name. Catalog and cash register are adjacent, not joined.

---

## 4. Public vs merchant-authenticated routes

B00 lock (decisions.md):

> Buyer | No One/Zitadel account. Public `GET /v1/pay/{token}` + `POST /v1/pay/{token}/start`. Do **not** ungated `GET /v1/checkouts/{id}`.  
> Shareable URL | `http://localhost:5179/c/{token}`.

This landed.

### 4.1 What 8081 actually maps (buyer-relevant)

`Program.cs`:

```csharp
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/v1/health", () => Results.Ok(new { status = "ok" }));
// ...
app.MapWhoami();
app.MapOrgReady();
app.MapCheckouts();
app.MapCatalog();
app.MapPublicPay();
app.MapGateways();
app.MapWebhooks();
app.MapPaymentQueries();
app.MapOneWebhooks();
```

| Method | Path | Auth | Who |
|--------|------|------|-----|
| `GET` | `/health`, `/v1/health` | none | Probes. Checkout Vite **no longer** calls these. |
| `GET` | `/v1/whoami` | Bearer → One `/me` | **Merchant.** Checkout must not call this. |
| `GET` | `/v1/orgs/{orgId}/ready` | Bearer + member | Merchant dummy admin. |
| `POST` | `/v1/checkouts` | Bearer + member of `body.org_id` | **Merchant create.** Mints `public_token`. |
| `GET` | `/v1/checkouts/{id}` | Bearer + member of **session.org_id** | **Merchant read.** Still gated. |
| `GET` | `/v1/pay/{token}` | **none** | **Buyer read.** |
| `POST` | `/v1/pay/{token}/start` | **none** | **Buyer hop-2.** |
| `PUT/GET` | `/v1/orgs/{orgId}/gateway` | writer / member | BYOK. Not the buyer. |
| `POST` | `/v1/webhooks/{provider}/{orgId}` | Stripe signature | Plane B. Not the Vite app. |
| `GET` | `/v1/orgs/{orgId}/payments`, `/receipts` | member | Merchant money UI. Buyer has no equivalent. |

There is still **no** `POST /v1/auth/login`. There is still **no** cookie middleware. CORS is origin-allow 5178/5179, `AllowAnyHeader` + `AllowAnyMethod`, **no** `AllowCredentials`. That last fact remains load-bearing: a Hub-style `credentials: "include"` cookie session cannot be smuggled onto 8081.

### 4.2 Merchant create still MemberGate (do not open it)

`CheckoutEndpoints.Create`:

1. `MemberGate.RequireMemberAsync(request, one, body.OrgId)` — no Bearer → **401** `"Missing bearer token"`; not a member → **403**.
2. Auto-seed `OrgSettingsRow` with `SstRegistered = false` if missing (so Fulfillment’s SST fail-closed on `null` does not fire for a fresh org).
3. `ChargesPaused` → 403 `"Org charges are paused"`.
4. Amount `> 0` else 400.
5. Currency default `MYR`, uppercased.
6. Idempotency from header or body.
7. Mint `Id = Guid.NewGuid().ToString("N")` (32 hex) **and**

```csharp
            PublicToken = Convert.ToHexString(Guid.NewGuid().ToByteArray()) + Convert.ToHexString(Guid.NewGuid().ToByteArray()),
```

   64 hex chars (two GUIDs concatenated). Not sequential. Not `org_id`. Not a product slug. K10.2 “unguessable 128-bit+” is met on **length**; GUID entropy is not `RandomNumberGenerator`. Good enough for Bar B WhatsApp links. Do not later switch the public identifier to a serial int.

8. `Status = "open"`, `Interval = "one_off"`.
9. 201 JSON snake_case via `OneClient.Json`.

`CheckoutTests` still pin: create without Bearer is 401 and **does not call One**; other-org 403; GET other-org 403; unknown GET 404 without One; idempotency `k1`; default MYR; amount 0 → 400.

`CreateCheckoutRequest` still has no payer fields. Ada cannot pre-address the session. Every buyer types name/email on 5179 (or leaves them blank — see §8).

`CheckoutStore` comment: “Postgres-backed checkouts. Not a ledger.” Correct. The ledger is `Fulfillment`. Restart no longer wipes sessions (D17). Host README is **stale** and still says:

> Checkout is an in-memory fixture (`status: open`). Not a real charge.

Do not teach new engineers from `apps/lazuar-pay/README.md` for money. The curl in that README still shows Bearer create (correct) and does not mention `/v1/pay/{token}` (incorrect omission).

### 4.3 Public GET (K10 / K11)

```csharp
    static async Task<IResult> Get(string token, CheckoutStore store, CancellationToken ct)
    {
        var session = await store.GetByPublicTokenAsync(token, ct);
        if (session is null)
        {
            return PayErrors.Status(404, "Not Found", "Checkout not found");
        }

        return Results.Json(new
        {
            token,
            amount = session.Amount,
            currency = session.Currency,
            status = session.Status,
            payer_name = session.PayerName,
            payer_email = session.PayerEmail
        }, OneClient.Json);
    }
```

`GetByPublicTokenAsync` is `AsNoTracking` lookup on unique `PublicToken`. No `MemberGate`. No One HTTP. `PublicPayTests.Public_get_does_not_need_bearer` creates with Bearer, then GETs **without** Bearer twice, and asserts `One.SendCount` does not increase on the second GET. `Public_missing_is_404` asserts unknown token 404 and `One.SendCount == 0`.

Error body is boring JSON `{ status, title, detail }` (`PayErrors.Status`). Not “sign in.” Not Hub magic-link copy.

**Buyer-safe?**

| Field | Returned? | 013/05 / K11 wanted? |
|-------|-----------|----------------------|
| `token` | yes (echo of path) | yes |
| `amount`, `currency`, `status` | yes | yes |
| `payer_name`, `payer_email` | **yes, raw** | “maybe masked”; K11 did not list them as required |
| `org_id` | **no** | must not |
| `success_url` / `cancel_url` | **no** | must not (may contain merchant secrets) |
| gateway secrets | no | must not |
| merchant display name | **no** | K11.1 “if you have it (else omit)” — omitted |
| `payer_required` | **no** | K11.1 checked `[x]` — **not live** |
| `is_reminder_only` / `rail` / `setup_only` | **no** | 013 §4.5 yes |
| `expires_at` | **no** | 013 §4.5 yes; column does not exist |
| product name / line items | **no** | after NP-CAT join |

K11.2 (must not leak org internals / secrets / staff URLs) **holds**. K11.1 (`payer_required`, merchant display) **does not**. The checklist is `[x]` anyway. Live DTO is a **subset plus raw PII**, not the documented shape.

Returning raw `payer_email` on an unauthenticated GET means the WhatsApp capability URL **is** the PII capability. That is acceptable for a pay link the merchant already sent to that person; it is not acceptable if the token leaks into Stripe `Referer` logs *and* we later show a previous payer’s mailbox to a stranger who opened the same link. Prefer echoing only after start, or mask (`a***@domain`). Not implemented.

`pay-spec` `PublicPay` model is **narrower than the host**:

```tsp
model PublicPay {
  token: string;
  amount: decimal;
  currency: string;
  status: string;
}
```

No `payer_name` / `payer_email` in the spec. Host returns them. Spec vs host drift. K20.1 “snake_case matches host” is only true for the four fields the spec knows.

### 4.4 Public start (K12)

```csharp
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
        if (!string.IsNullOrWhiteSpace(body?.Name)) row.PayerName = body.Name.Trim();
        if (!string.IsNullOrWhiteSpace(body?.Email)) row.PayerEmail = body.Email.Trim();

        try
        {
            var url = await stripe.CreateHostedUrlAsync(row, ct);
            row.PspRedirectUrl = url;
            await db.SaveChangesAsync(ct);
            return Results.Json(new { redirect_url = url }, OneClient.Json);
        }
        catch (InvalidOperationException ex)
        {
            return PayErrors.Status(503, "Service Unavailable", ex.Message);
        }
        catch (Stripe.StripeException)
        {
            return PayErrors.Status(503, "Service Unavailable", "Stripe rejected the org key");
        }
```

`StartPayRequest` is `Name` + `Email` only. Extra JSON `amount` is ignored (not bound). Buyer cannot retarget the bill. Good. Bezos door holds: merchant create is the amount door.

Unknown token → 404, same class as GET. **Not tested** on POST (K13.4 claimed GET/POST; live `PublicPayTests` only GET). Paid/expired → 409, not a new Stripe session. **Not tested.** 503 without rail: live via `StripeHosted` throw `"rail not configured"`. **Not tested** as an HTTP assertion. Empty webhook 400 lives in `PublicPayTests` — a Plane B test that wandered into the public-pay file.

No Bearer. No One call on the start path (org settings + credentials + Stripe). Buyer plane holds.

`PspRedirectUrl` is overwritten every start. Previous Stripe Checkout Session is abandoned. Hub B03-C02 (double-click mints two captures) is **not** closed on the host. The SPA `busy` flag is not a substitute.

Name/email persist on the **checkout row** (`PayerName` / `PayerEmail`). `payers` table is written later in `Fulfillment` only if those fields are non-empty at paid time. 013/07 recommendation (“store on open checkout; upsert payers only when paid”) landed. Empty start → paid webhook → **no** `PayerRow`. NP-BUY-001 is “on the checkout session”; the session columns exist; they are optional in practice.

### 4.5 Sequence that is live

```text
Ada (One human)
  :5175 login → access_token
  :5178 WorkspacePage
       PUT  /v1/orgs/{orgId}/gateway { provider: stripe, secret: sk_test_… }
       POST /v1/orgs/{orgId}/products { name, amount, currency: MYR }
       POST /v1/checkouts { org_id, amount, currency: MYR }
            → 201 { id, public_token, status: open, … }
  copies http://localhost:5179/c/{public_token}

Buyer (not a One human)
  GET  http://localhost:8081/v1/pay/{token}     # no Bearer
  fill name + email (or not)
  POST http://localhost:8081/v1/pay/{token}/start
       → 200 { redirect_url: https://checkout.stripe.com/… }
         or 503 { detail: "rail not configured" }
  window.location = redirect_url

Stripe Checkout (mode=payment)
  capture / fail / cancel
  webhook POST /v1/webhooks/stripe/{orgId} → Fulfillment (status=paid, RCPT-, journal)
  redirect → success_url (default :5179/c/{token}?status=verifying)
          or cancel_url (default :5179/c/{token})

Buyer
  :5179 re-GETs public status once
  if webhook already wrote paid → Paid pixel
  else → open form again (not verifying)
```

If step “Buyer GET” required Bearer, the slice would already have failed NP-CHK-007. It does not. The remaining fail is **honesty after return**, not login.

---

## 5. CORS origins

`Program.cs`:

```csharp
builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p =>
        p.WithOrigins(
                "http://localhost:5178",
                "http://127.0.0.1:5178",
                "http://localhost:5179",
                "http://127.0.0.1:5179")
            .AllowAnyHeader()
            .AllowAnyMethod());
});
var app = builder.Build();
app.UseCors();
```

No `AllowCredentials`. `localhost` ≠ `127.0.0.1` twins are both listed (One issue 077). **Not** listed: `:3003` ops, `:3004` portal, `:5173` admin, `:5175` login, `:3005`.

`CorsTests` live:

| Test | Pin |
|------|-----|
| `Health_allows_merchant_origin` | ACAO `http://localhost:5178` on `GET /health` |
| `Health_allows_checkout_origin` | ACAO `http://localhost:5179` on `GET /health` |
| `Health_does_not_allow_ops_origin` | **no** ACAO for `http://localhost:3003` |
| `Health_does_not_allow_portal_origin` | **no** ACAO for `http://localhost:3004` |

Q15 `[x]` matches these four tests. K14 `[x]` does **not**:

> CorsTests cover public pay GET/POST/OPTIONS + deny 3003 and 3004

There is **no** test that `GET /v1/pay/{token}` or `POST /v1/pay/{token}/start` or `OPTIONS` returns ACAO for 5179. The default policy applies to all mapped routes, so a browser on 5179 **should** succeed. That is not the same as a pin. 013/05 §3.7 already asked for this test “when the public resource exists.” The resource exists. The test was not added.

Do **not** add `:3004` “so portal can dual-run against 8081.” Do **not** add `:5179` to One CORS or login `REDIRECT_ALLOWLIST`. One CORS still does not need 5179. Adding it is how a well-meaning P10 PR fails NP-CHK-007.

Production will need the **deployed** checkout origin on this list (and fail-closed empty, same class as One). Localhost twins stay for laptop dogfood.

---

## 6. How start-pay redirect to Stripe works

### 6.1 Who mints hop-2: Pay host, not Vite

013/05 §6.5 sequence is live, with Stripe as the only rail (`GatewayEndpoints` refuses non-`stripe` with `"Bar B first rail is stripe"`).

`StripeHosted.CreateHostedUrlAsync`:

```csharp
        var cred = await db.GatewayCredentials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrgId == checkout.OrgId && x.Provider == Provider, ct);
        if (cred is null)
        {
            throw new InvalidOperationException("rail not configured");
        }

        var secret = box.Unprotect(cred.Ciphertext);
        var service = new SessionService(new StripeClient(secret));
        var cents = (long)Math.Round(checkout.Amount * 100m, MidpointRounding.AwayFromZero);
        var session = await service.CreateAsync(new SessionCreateOptions
        {
            Mode = "payment",
            ClientReferenceId = checkout.Id,
            SuccessUrl = checkout.SuccessUrl ?? "http://localhost:5179/c/" + checkout.PublicToken + "?status=verifying",
            CancelUrl = checkout.CancelUrl ?? "http://localhost:5179/c/" + checkout.PublicToken,
            Metadata = new Dictionary<string, string> { ["checkout_id"] = checkout.Id, ["org_id"] = checkout.OrgId },
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = checkout.Currency.ToLowerInvariant(),
                        UnitAmount = cents,
                        ProductData = new SessionLineItemPriceDataProductDataOptions { Name = "Pay" }
                    }
                }
            ]
        }, cancellationToken: ct);
        return session.Url ?? throw new InvalidOperationException("Stripe returned no URL");
```

Locked facts:

| Fact | Live |
|------|------|
| Mode | `"payment"` — **not** `subscription`, **not** `setup`. NP-GW-008 is easier because Bar B does not mint setup at all. |
| Keys | Org BYOK via `SecretBox.Unprotect`. Never `VITE_*`. Never on 5179. |
| Amount | Session amount × 100, away-from-zero. Quantity 1. Not catalog seats. |
| Line item name | `"Pay"`. Not product name. |
| Client reference | Pay `checkout.Id` (merchant id, not public token). Webhook fulfills by this. |
| Metadata | `checkout_id` + `org_id`. `org_id` is **not** shown to the buyer DTO; it **is** sent to Stripe. Acceptable for BYOK metadata; do not echo it on GET. |
| Customer email / name | **Not passed.** Stripe will ask again. Hub CHIP `ExtractName(email)` lie is not copied; the better path (pass `PayerName`) is also not copied. |
| Success URL | Merchant `SuccessUrl` if set, else `:5179/c/{publicToken}?status=verifying`. Merchant UI currently does **not** set it, so dogfood returns to 5179. |
| Cancel URL | Merchant `CancelUrl` if set, else `:5179/c/{publicToken}` with no query. |

G16 `[x]` “Stripe Checkout `mode=payment`, cards. Not `mode=subscription`. Not `mode=setup` as paid” matches the options object. `PaymentMethodTypes` is **not** set; Stripe account defaults apply (card + wallets that ride on card). Lazuar still does **not** render those wallets. Correct.

G17 `[x]` “store checkout_url” is **partial**: `PspRedirectUrl` is stored. `provider` / `provider_session_id` columns were not added. Stripe session id is **not** persisted at start (it arrives later as webhook `session.Id` → `ChargeRow.ProviderRef`). If start must be idempotent, you need the Stripe session id **or** a still-valid URL. Today you have a URL that may already be expired on Stripe’s side and no id to retrieve.

### 6.2 503 rail not configured

No `gateway_credentials` row for `(orgId, stripe)` → `InvalidOperationException("rail not configured")` → HTTP 503 with that detail. SPA special-cases status 503 to the same English string. A merchant who created a pay link before pasting `sk_test_` produces an honest dead Pay button. Good.

Bad org key → `StripeException` → 503 `"Stripe rejected the org key"` → SPA still says `rail not configured`. Slightly dishonest, still fail-closed, still not a fake `checkout.stripe.com`.

K12.2 “Never a made-up `checkout.stripe.com`” holds. There is no stub URL.

### 6.3 What 5179 never loads (keep)

- `stripe` npm / `@stripe/stripe-js`
- CHIP collect.js
- Billplz JS
- Merchant `pk_live_` / `pk_test_` in `import.meta.env`
- `STRIPE_SECRET_KEY` (that is Pay-host BYOK)

The only outbound origins from the buyer browser besides Pay 8081 should be: the PSP (after redirect), maybe a merchant `success_url` if Ada set one, and static assets. Network-tab law for NP-CHK-007: **8081 public pay + later `checkout.stripe.com`**. That is all. `:5175`, `:8085`, `/v1/whoami`, One `/api/v1/me` are fail.

### 6.4 Wallet buttons we must NEVER render ourselves

013/06: “Do not show GrabPay tiles on `:5179`. PSP hosted page owns tiles.” G15.3 “No unread DuitNow / wallet tiles on `:5178` / `:5179`.” NP-XX-011: no homemade FPX e-mandate grid.

Live `App.tsx` has one button labeled `Pay`. There is no method picker. Adding Apple Pay / Google Pay / FPX / DuitNow / TnG **buttons on this origin** is a refuse even if Stripe Elements would make conversion better. Mode B (Elements / Embedded Checkout) is a later conversion ticket and still must not become a Pay-rendered bank list. Bar B is redirect.

If a future PR adds `@stripe/stripe-js` “just to show Payment Request Button on first paint,” that PR fails this slice’s wrap-rails lock unless it is explicitly a new program with CSP + publishable key + a Pay endpoint that mints a client_secret. Do not sneak it into “polish the cash register.”

### 6.5 Idempotency and double charge

Create-checkout idempotency (`Idempotency-Key` per org) is **merchant** NP-CHK-003 and tested. Start-pay idempotency is **not**.

Failure mode:

1. Buyer clicks Pay → Stripe session A, `PspRedirectUrl = A`.
2. Buyer completes A. Webhook slow.
3. Stripe sends them to `?status=verifying`.
4. SPA sees `open`, shows Pay again.
5. Buyer clicks Pay → Stripe session B, **new** capture possible.

Closing this is a **host** job (return stored URL if still open and not Stripe-expired; refuse second mint) plus a **pixel** job (verifying poll, hide Pay while `open` after a success return). Neither exists. Hub B03-C02 is the cousin. Do not ship a second rail on top of this hole.

---

## 7. Success / cancel / verifying honesty

### 7.1 Who writes `paid`

`WebhookEndpoints` on `checkout.session.completed`:

```csharp
                if (session.Mode == "setup" || (session.AmountTotal is null or 0))
                {
                    return Results.Json(new { ignored = "setup_or_zero" }, OneClient.Json);
                }

                var checkoutId = session.ClientReferenceId ?? session.Metadata?["checkout_id"];
                if (!string.IsNullOrWhiteSpace(checkoutId))
                {
                    await fulfillment.FulfillPaidAsync(checkoutId, StripeHosted.Provider, session.Id, ct);
                }
```

`Fulfillment.FulfillPaidAsync`: amount `<= 0` returns without writing; status not `"open"` commits no-op; SST `null` throws; else `checkout.Status = "paid"` + charge + optional payer + optional subscription + balanced journal + `RCPT-{year}-{seq}` titled **Official Receipt** + audit `checkout.paid`, one transaction.

Vite never PATCHes status. Query string never writes the row. NP-CHK-002 / NP-FUL-001 hold on the host.

NP-GW-008 host side: setup/zero is ignored, not journaled. Bar B does not mint `mode=setup`, so the frontend fork “Card saved” vs “Payment received” is unused. **Do not** later mint setup and reuse the Paid pixel.

### 7.2 What the SPA does on return (the hole)

Stripe default `SuccessUrl` includes `?status=verifying`. The SPA:

1. Does not read `window.location.search`.
2. Does not enter a verifying state.
3. Does not poll `GET /v1/pay/{token}`.
4. Does not timeout to “still confirming.”
5. Does not hide the Pay button because “we just came back from Stripe.”

K16.1 verifying: **not live**.  
K19.1 “Poll public GET until `status=paid` or timeout”: **not live**.  
K19.1 “Paid pixel only when public GET says `paid`”: **true for the Paid branch** (it keys off `pay.status === 'paid'`), **false as a return experience** (return usually still `open`, so the form shows). It does **not** paint Paid because the query said `verifying` / `success`. That half of NP-CHK-002 is intact: there is no green check from the query string. The other half (a verifying spinner so the buyer is not invited to pay twice) is missing.

Cancel URL has no query. Session stays `open` (host does not expire). Pay button still works. That is the correct cancel behaviour — accidentally, because cancel and “success but still open” are the **same pixel**.

If Ada **does** set `success_url` on create to `https://course.example/thanks`, Stripe never returns to 5179. That page is marketing. Pay will not tell the buyer they are unpaid. Merchant UI currently does not pass `success_url`, so dogfood stays on 5179. When someone adds a success_url field to `:5178`, K19 must still own a Pay return **or** the merchant site must not unlock access. 013 Q3 is still open.

### 7.3 Paid copy vs receipts for the buyer

Paid pixel: “The merchant will see an Official Receipt.” Merchant `:5178` lists `/v1/orgs/{orgId}/receipts` (member-gated) with `number` + `title`. F20/F21 are **staff**. F20.3: “Buyer public download is Bar C (`NP-BUY-005`).”

Missing on `:5179`:

- `RCPT-` number for the person who paid
- PDF / download
- Email of the receipt (NP-MAIL-001; `mail_outbox` table exists, no sender on the buyer path in this slice)
- Magic-link `/r/{magic}` reserved by the checkout README

README still says the right reservation:

> Receipts / update-payment can share this origin later (magic link to the payer mailbox), not the merchant shell.

Do not put receipt download on `:5178` as “the buyer portal.” Do not send magic links to `:5175`.

Title lock: Paid H1 is `Paid`, not `Invoice`. Host document title is `Official Receipt`. Grep of checkout `src` has no `Tax Invoice`, no `VALID`. F15 holds on this origin because the origin barely mentions documents.

### 7.4 Expired

UI branch: yes. Host writer: no. `expires_at`: no. Leaked WhatsApp links live forever in `open` and can still `start` until something marks `paid`. 013/05 wanted a TTL so a leaked pay link does not live forever. Bar B parked it (F11.2: do not fulfill `expired`; Bar C / Hub 036). The pixel exists so a future writer does not 500 the SPA. It is not a product feature yet.

---

## 8. Payer name / email (NP-BUY-001)

### 8.1 Two planes (still)

| Plane | System | Who |
|-------|--------|-----|
| Merchant staff | One humans + membership | Ada, invited owner/admin/member |
| Buyer / payer | **Pay checkout profile** | Person who pays on `:5179` |

Cardholders never become Zitadel users because they bought an ebook. Start does not call `POST /tenants/{id}/members/invite`. Fulfillment’s `PayerRow` has `OrgId` + `Email` + `Name` — no One `user_id`. Ada buying her own product as a test is still a guest checkout. Merchant OIDC on 5178 is a different origin; 5179 cannot see that sessionStorage (different ports; no cookies). Hub IdentityBanner cannot leak onto this SPA unless someone ports it. Do not.

### 8.2 What 5179 collects

Required by K18: name **and** email before hop-2.  
Live: two uncontrolled `<input>`s, no `required`, no type=email, no disable-Pay-until-filled. POST always fires.

Host: persist when non-whitespace. No 400 if missing. Stripe: does not receive the name/email (`CustomerEmail` unset). Buyer may type them on 5179 **and again** on checkout.stripe.com. Two-hop tax (007-09) is still here, now with a possible **third** email field on Stripe.

Prefill: host GET returns `payer_name` / `payer_email`. SPA `PayView` does not include them. Fields start empty even on a second visit. K18 “if merchant already put payer email, field may be read-only” — merchant create cannot put it (`CreateCheckoutRequest` has no such properties).

TIN / BRN / NRIC / MyInvois: absent. Good. NP-XX-002 stays refused.

### 8.3 Tracker

011/11 **NP-BUY-001** is `todo`. Columns exist. Form exists. Persistence-on-start exists. Required-before-hop-2 does **not**. Passing name into Stripe does **not**. This paper does not flip the cell. An honest later flip needs: non-empty email stored on the row that the webhook will copy into `payers`, and a test.

---

## 9. No OIDC on this origin (NP-CHK-007)

### 9.1 Package / source / env

- `package.json`: react + react-dom. No oidc.
- `locks.test.ts` pins the two oidc package names and Hub types.
- No `/callback` route. Merchant has `/callback` + `/login` + `RequireAuth`.
- `.env.example`: `VITE_PAY_API_URL` only.
- `App.tsx` fetch: no Authorization.
- README fail lock in English:

> Buyers have **no** One account. Fail if this page asks for Zitadel login.

K22 runbook: open `http://localhost:5179/c/{token}` with **no** One account; merchant mints via Bearer; Hub portal 3004 is not in the path. The checkout README is short but it states the lock. Host README still talks about in-memory fixtures and does not walk `/c/{token}`.

### 9.2 Runtime e2e

There is no Playwright project under `apps/lazuar-pay-checkout`. K17.3 “Fresh profile: no redirect to `:5175`” is a human procedure, not CI. The code path cannot redirect to 5175 because nothing imports an authority URL. That is strong static evidence. It is not a lived B99 tick. `b99-bar-b-done.md` item “Buyer opens `:5179/c/{token}` **without** a One account” is still `[ ]`.

### 9.3 IsolationTests Vite ban (quote)

```csharp
    [Test]
    public void Vite_apps_do_not_use_hub_types()
    {
        var repo = FindPayRoot();
        while (repo is not null && !Directory.Exists(Path.Combine(repo, "apps", "lazuar-pay-merchant")))
        {
            repo = Directory.GetParent(repo)?.FullName;
        }

        Assert.That(repo, Is.Not.Null);
        foreach (var name in new[] { "lazuar-pay-merchant", "lazuar-pay-checkout" })
        {
            var pkg = Path.Combine(repo, "apps", name, "package.json");
            Assert.That(File.Exists(pkg), Is.True, pkg);
            var text = File.ReadAllText(pkg);
            Assert.That(text, Does.Not.Contain("@repo/api-types-ts"), pkg);
        }
    }
```

This is the cathedral lock on the SPA packages. Merchant is allowed OIDC (One, not Hub). Checkout is allowed neither Hub types nor OIDC (the latter pinned in vitest, not in this C# test). Adding `@repo/api-types-ts` to checkout **fails `task pay:test`**. Adding `oidc-client-ts` to checkout **fails vitest** if someone runs it, and **fails human K17**, and would not fail IsolationTests. Widen IsolationTests if that footgun worries you; do not weaken it.

---

## 10. `packages/pay-spec` public pay

```tsp
model PublicPay {
  token: string;
  amount: decimal;
  currency: string;
  status: string;
}

model StartPayResponse {
  redirect_url: string;
}

@route("/v1")
@tag("Pay")
interface PublicPayApi {
  @get
  @route("/pay/{token}")
  get(@path token: string): PublicPay;

  @post
  @route("/pay/{token}/start")
  start(@path token: string): StartPayResponse;
}
```

K20 holds on **paths**: `GET /v1/pay/{token}`, `POST /v1/pay/{token}/start`, namespace `LazuarPay`, server `http://localhost:8081`. Does **not** import Hub `/public/commerce`. Does **not** mark merchant `GET /v1/checkouts/{id}` unauthenticated.

Gaps vs host:

- `start` has no `@body` (`StartPayRequest` name/email). Spec looks like an empty POST; host accepts JSON.
- GET model omits `payer_name` / `payer_email`.
- No error models (404/409/503).
- File header comment is stale: “Checkout is a fixture (open session), not a charge.”
- `pay-spec/README.md` still says “Grow `main.tsp` when `POST /v1/checkouts` exists.” It exists.

`@repo/pay-types-ts` is still not a checkout dependency. Hand-written `PayView` is the contract the pixel actually uses. CI compiles TypeSpec (`pnpm --filter @repo/pay-spec exec tsp compile .`) but does not generate TS clients into this app. That is isolation-correct. It is also how DTO drift survives.

Merchant `CheckoutSession` in the spec now includes optional `public_token`. Host returns it. Good.

---

## 11. Tests that exist vs tests this origin still needs

### 11.1 Exist

| Test | Pin |
|------|-----|
| `PublicPayTests.Public_get_does_not_need_bearer` | 200 without Authorization; second GET does not call One |
| `PublicPayTests.Public_missing_is_404` | Unknown token 404, One.SendCount 0 |
| `PublicPayTests.Empty_webhook_is_400` | Plane B; misplaced file |
| `CheckoutTests.Create_without_bearer_is_401` | Merchant create is not public |
| `Create_and_get_open_session` | Merchant JSON; GET needs Bearer; `status: open` at create |
| `Get_unknown_is_404` | Missing + Bearer, no One call |
| `Create_for_other_org_is_403` / `Get_other_org_session_is_403` | Member gate |
| `Create_idempotent_on_key` | NP-CHK-003 |
| `Create_defaults_currency_to_myr` | MYR |
| `Create_rejects_non_positive_amount` | |
| `CorsTests.Health_allows_checkout_origin` | 5179 ACAO on `/health` |
| `Health_does_not_allow_ops_origin` / `_portal_origin` | 3003/3004 denied |
| `IsolationTests.Vite_apps_do_not_use_hub_types` | both Vite package.json |
| `WebhookTests.Completed_session_writes_receipt_and_replay_is_noop` | paid + RCPT- + journal; not a SPA test |
| `locks.test.ts` | no oidc, no Hub types in checkout package.json |

### 11.2 Missing (013/05 Appendix D, still missing after Bar B)

1. Public GET CORS ACAO 5179, including OPTIONS, on `/v1/pay/{token}` — K14 claimed; not in CorsTests.
2. `POST /v1/pay/{token}/start` without Bearer: 503 when no rail; 200 `{ redirect_url }` with a fake Stripe is **hard** hermetically (real Stripe HTTP). At least pin 503 + 404 + 409 without network.
3. `start` on paid is 409, not a new session.
4. `start` unknown token is 404, never 401/403.
5. `start` does not accept a new amount (ignore or 400) — untested.
6. Public GET body does not contain `org_id` / `success_url` — untested (true by construction; still a one-liner).
7. Status poll: only `paid` flips the success pixel — **no frontend test**.
8. Playwright: buyer context never requests `:5175` or `/v1/whoami`.
9. Name+email required — neither host nor SPA test.
10. Start idempotency.

CI `pay` job runs host tests + Vite **build**, not Vite **test**. `locks.test.ts` can rot on main if nobody runs vitest.

---

## 12. K10–K22 spot-check (checklist `[x]` vs live)

Legend: **holds** = live files match the checkbox intent. **over-claimed** = `[x]` but live is thinner. **holds with a hole** = route/pixel exists; honesty incomplete.

| Cell | Checklist claim | Live |
|------|-----------------|------|
| **K10** public GET no Bearer | `[x]` | **Holds.** `MapGet("/v1/pay/{token}")`; tests. Merchant GET still gated. |
| **K11** buyer-safe DTO | `[x]` including `payer_required` + merchant display | **Over-claimed.** Amount/currency/status/token yes; no org_id leak yes; `payer_required` and merchant name **absent**; extra raw PII. |
| **K12** POST start | `[x]` 503 stub or real `{ redirect_url }` | **Holds with a hole.** Route, 404, 409 paid/expired, 503, Stripe mint all exist. **No tests** for start. Double-mint. |
| **K13** unknown 404 | `[x]` GET and POST; never 401/403 | **Holds with a hole.** GET tested. POST start unknown **untested**. Start **does** 403 when charges paused (not an auth wall, still 403). |
| **K14** CORS on `/v1/pay/*` | `[x]` GET/POST/OPTIONS + deny 3003/3004 | **Over-claimed.** Policy should apply; tests are `/health` only. Deny 3003/3004 holds on health (Q15). |
| **K15** Vite `/c/{token}` | `[x]` | **Holds.** Path regex, `VITE_PAY_API_URL` → 8081, title, strictPort 5179, no whoami, no portal retarget. |
| **K16** page states | `[x]` open/paid/expired/missing/**verifying** | **Over-claimed.** Open/paid/expired/missing pixels exist. **Verifying does not.** Expired is unreachable from the host. |
| **K17** no OIDC | `[x]` | **Holds** statically (package + src + README). Runtime e2e not in CI. |
| **K18** payer fields | `[x]` required before hop-2 | **Over-claimed.** Fields + persist-if-present. **Not required.** Not passed to Stripe. No merchant prefill. |
| **K19** success_url is not paid | `[x]` verifying poll | **Holds with a hole.** Does not paint Paid from query string. **Does not poll.** Default success URL’s `?status=verifying` is ignored. Double-pay invitation. |
| **K20** TypeSpec public pay | `[x]` | **Holds** on paths. Drift on body/PII fields. Stale header comment. |
| **K21** no Hub types | `[x]` | **Holds.** IsolationTests + locks.test.ts + package.json. |
| **K22** runbook | `[x]` | **Holds** on checkout README (short). Host README stale (in-memory fixture). B99 still open. |

Do not flip 011 from this table. Do not un-check 013 cells in this evaluation. Report the disagreement to `10-honesty-gaps-next.md`.

---

## 13. 011 NP-CHK-005 / 006 / 007 (still `todo`, and why that is not hypocrisy)

From `plans/011-new-lazuar-pay/11-checklist.md`:

| ID | Feature | Status | Notes |
|----|---------|--------|-------|
| NP-CHK-004 | States: open → paid / expired | `todo` | Host: open → paid. Expired writer absent. |
| NP-CHK-005 | Hosted buyer pay page (cash register) | `todo` | Pixel exists. Verifying/poll/required email/wrap-rails copy/e2e do not. |
| NP-CHK-006 | Shareable pay link | `todo` | Token + `/c/{token}` + merchant `<a>`. No `pay_url` field. Localhost hardcoded. |
| NP-CHK-007 | Buyer pays **without** a One account | `todo` | Fail if checkout requires Zitadel login. Statically true. B99 unlived. |

012 first-slice tracker steps 9–10 still `todo`. B99.1 “Buyer opens `:5179/c/{token}` **without** a One account” still `[ ]`.

013/05 said: do not claim these `done` while the app is a health probe. They are still `todo` now that the app is a thin cash register. That is **consistent with 011’s “flip when a human pay path ran”**, not with 013 K-track’s `[x]` meaning “Bar B closed.” The K track unblocked B99. B99 is the lived sentence. This paper’s job is to say: **the door is there; the dogfood tick is not; several K boxes are costumes.**

NP-CHK-005 *could* be argued “the SPA exists.” 011/03 step 10 is “Buyer (no One account) **pays** on the hosted page.” Paying requires Ada’s keys, Stripe test card, webhook to 8081, and a Paid pixel that did not invite a second capture. That is B99, not a Vite file listing.

---

## 14. What is missing (ranked, this origin)

These are evaluation findings, not a work order. Implementation is a later program.

### 14.1 Honesty holes that can take money

1. **No verifying poll.** Default Stripe success URL returns to an **open Pay button**. Combined with non-idempotent `start`, this is a double-charge footgun. Steal Hub `CheckoutSuccessView` discipline: only `paid` is paid; timeout is “still confirming”; never unlock on query string. Hub poller was 20×3s. Any numbers are fine; the branch must exist.
2. **Start is not idempotent.** Persist Stripe session id; reuse `PspRedirectUrl` while open; do not `CreateAsync` on every click.
3. **Payer email not required.** Receipt mailbox, later magic link, and `PayerRow` all depend on it. Fail closed on empty email (400 or disable Pay).
4. **Payer name/email not sent to Stripe.** Two-hop re-entry; CHIP/Billplz later will re-learn Hub’s `ExtractName(email)` if we do not pass the session fields.

### 14.2 Pixel holes that do not take money yet but will lie

5. **Wrap-rails copy.** Bar B Stripe hosted_link: “You will complete payment on Stripe.” Do not say auto-charge. When CHIP/Billplz land, public DTO needs `capability` / `is_reminder_only` and the button helper **must** change. Today the paragraph only says success URL ≠ paid.
6. **Merchant display / product name.** Buyer sees `10 MYR` and kicker “Lazuar Pay”. 007-09 conversion: lead with the merchant, not “Powered by Lazuar.” Join `ProductId` or pass a display name onto the session.
7. **Expired writer + TTL.** Pixel is ready; host is not. Leaked tokens live forever.
8. **GET error pixel.** Non-404 GET failure is infinite Loading.
9. **503 mapping.** All 503s are “rail not configured.”
10. **Charges-paused 403** on public start. Copy should not say “log in.” Today it says `start 403`.

### 14.3 Buyer-plane product still later (same origin, not S1)

11. **Receipts for the buyer.** Merchant sees `RCPT-`. Buyer is told the merchant will see it. NP-BUY-005 / magic link `/r/{magic}` / mail. README already reserved this origin.
12. **Update-payment / arrears.** NP-BUY-004. Never “update card” on reminder-only. Never RM 1. Never setup-as-paid.
13. **EN/BM, quantity, coupons, quotes, TIN.** Refuse TIN. Park the rest.
14. **Elements / wallets on first paint.** Conversion later. **Never** Pay-rendered FPX/GrabPay/TnG/DuitNow tiles.

### 14.4 Tests / DX / docs

15. CorsTests on `/v1/pay/*` OPTIONS/GET/POST.
16. PublicPayTests for start 404/409/503.
17. CI `pnpm --filter lazuar-pay-checkout test`.
18. IsolationTests Vite scan for `oidc-client-ts` on the **checkout** package (merchant must keep oidc).
19. Host README: delete “in-memory fixture”; document `GET /v1/pay/{token}` and `:5179/c/{token}`.
20. pay-spec: start body, payer fields, drop “fixture” comment.
21. `pay_url` as a first-class field so merchant UI does not hardcode `localhost:5179`.
22. Playwright buyer context ≠ merchant storage state. Not a prerequisite to call the pixel a cash register; a prerequisite to flip NP-CHK-007 in 011.

### 14.5 What must not be added to close the gaps

- OIDC / `:5175` / `GET /v1/whoami` on this origin
- `lazuar_auth` / `credentials: "include"` / `AllowCredentials`
- `@repo/api-types-ts` / Hub `/public/commerce`
- Retarget `lazuar-portal` `:3004` at 8081
- Stripe.js Elements + homemade method grid in the same PR as “fix verifying”
- Tax Invoice / VALID / TIN at checkout
- Wallet buttons rendered by us
- Opening `GET /v1/checkouts/{id}` to skip the public DTO

---

## 15. Anti-goals (fail this slice even if the page looks like Stripe)

Unchanged from 013/05 §9, restated against live code.

### 15.1 Identity

| Anti-goal | Why | Live |
|-----------|-----|------|
| Redirect `:5179` → `:5175` / Zitadel `/ui/login` | NP-CHK-007 | Not present |
| Mount OIDC on checkout Vite | P10 trap | Not present; merchant has it |
| `GET /v1/whoami` from checkout | Merchant endpoint | Not present |
| Password form / Google sign-in | NP-XX-007 | Not present |
| Create Zitadel human on successful pay | NP-XX-013 | Not present |
| IdentityBanner “Use my Lazuar account” | Hub cookie plane | Not present |
| `credentials: "include"` to 8081 | Pay CORS has no credentials | fetch defaults |

### 15.2 Cathedral retarget

| Anti-goal | Why | Live |
|-----------|-----|------|
| Set portal `NEXT_PUBLIC_API_URL` to 8081 | P60 | Not this app’s job; do not |
| Add `:3004` to Pay CORS | Invites that retarget | CorsTests deny 3004 on `/health` |
| Import `@repo/api-types-ts` | Hub contract | IsolationTests + locks |
| Copy `apps/lazuar-portal` into checkout | Next, shadcn, TIN, cookie | Not copied; single `App.tsx` |
| Implement Hub `/public/commerce/checkout` on 8081 | pay-spec forbids | Public pay is `/v1/pay/{token}` |

### 15.3 Money lies on the pixel

| Anti-goal | Why | Live |
|-----------|-----|------|
| Green “Paid” on PSP success redirect alone | NP-CHK-002 | **Avoided** (query ignored) **and invited** (form still Pay) |
| Green “Paid” on `mode=setup` | NP-GW-008 | Host ignores setup; SPA has no setup pixel |
| “We will charge your card automatically” | NP-GW-007 | Copy does not say that; also does not say hosted-on-Stripe |
| Title receipt Tax Invoice | NP-XX-003 | Says Official Receipt (merchant) / Paid (buyer) |
| Unlock files from status poll | NP-FUL-002 | No files on this SPA |
| Buyer-chosen amount on public POST | Bezos door | `StartPayRequest` has no amount |
| Silent second hop-2 session | B03-C02 | **Present on the host** |

### 15.4 Scope creep

WhatsApp dunning, TIN, UBL, plan-change, Stripe Billing Customer Portal as SoT, Elements/FPX in the first redirect PR, merchant sidebar on 5179. None of these are in `App.tsx`. Keep them out.

---

## 16. Mapping 013/05 open questions to live answers

013/05 §10 asked product questions. Bar B answered some in B00 + code.

| Q | 013 lean | Live |
|---|----------|------|
| Q1 capability URL vs public_token vs HMAC | Q1-B public_token | **Q1-B.** `/c/{token}` + `GET /v1/pay/{token}`; merchant GET stays gated. Two GUIDs hex. Not Hub HMAC. |
| Q2 mint hop-2 at create or click | click-time | **Click-time.** `start` calls Stripe. WhatsApp link can sit; Stripe session is not 24h-stale at paste. |
| Q3 success URL default | Pay-owned return | **Pay-owned default** `:5179/c/{token}?status=verifying` if merchant omitted. Pixel does not honour `verifying`. Merchant UI omits custom URLs. |
| Q4 status strings | `open \| paid \| expired` | **Those strings.** Host produces `open` and `paid`. `expired` is a refuse class only. |
| Q5 SST line on S1 | show session amount | **Session amount only.** No tax line on 5179. SST fail-closed is fulfillment, not this SPA. |
| Q6 EN/BM | English | **English.** |
| Q7 router | react-router | **No router package.** Regex in `App.tsx`. Fine for one route. Add a router when `/r` / `/u` exist — not Next. |
| Q8 One CORS for 5179 | No | **Still no.** |
| Q9 Preview as Ada | guest window | Merchant copies a link; 5179 has no OIDC to detect Ada. Good. |
| Q10 CSRF / origin | CORS + rate-limit | CORS yes. Rate-limit **no**. Buyer body cannot set success_url (ignored because not on `StartPayRequest`). Good. |
| Q11 existence oracle | public always 404 | **Public always 404** on bad token. Merchant GET still 401 vs 404. Do not “fix” merchant GET. |
| Q12 `@repo/pay-types-ts` | not now | **Still not.** Pixel uses a hand type. |

---

## 17. File inventory (checkout Vite, complete)

Base: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-checkout/`

| File | Role on `ee2db8e5` |
|------|---------------------|
| `package.json` | 5179 `strictPort`; react only; vitest |
| `README.md` | Fail if Zitadel; `task pay:checkout` |
| `.env.example` | `VITE_PAY_API_URL=http://localhost:8081` |
| `vite.config.ts` | 5179 / preview 4179, `strictPort` |
| `vitest.config.ts` | node env |
| `index.html` | Title “Lazuar Pay — checkout” |
| `tsconfig.json` / `tsconfig.app.json` / `tsconfig.node.json` | Bundler; `src` + vite config |
| `src/main.tsx` | React 19 `createRoot` |
| `src/App.tsx` | **Cash register.** `/c/{token}`, GET, start, redirect |
| `src/App.css` | Layout + leftover `dl` from health probe |
| `src/index.css` | system-ui |
| `src/locks.test.ts` | no oidc / Hub types |
| `public/favicon.svg` | Vite default mark |
| `dist/**` | Local build artifact (hashed). Not source of truth. |

No `src/pages`, no Stripe, no router, no OIDC, no Hub types. That smallness is a feature.

---

## 18. Sources index (absolute)

```
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-checkout/**
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/App.tsx
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/pages/WorkspacePage.tsx
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/package.json
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/README.md
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Program.cs
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Checkouts/*.cs
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Gateways/StripeHosted.cs
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Gateways/WebhookEndpoints.cs
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Money/PaymentQueryEndpoints.cs
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/One/MemberGate.cs
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/One/PayErrors.cs
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/README.md
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPayTests.cs
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/CheckoutTests.cs
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/CorsTests.cs
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/WebhookTests.cs
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/pay-spec/main.tsp
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/pay-spec/README.md
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/Taskfile.yml
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/.github/workflows/ci.yml
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/11-checklist.md
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/12-first-slice-tracker.md
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/013-prods/05-checkout-frontend.md
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/013-prods/checklists/k10-public-pay-get.md … k22-checkout-runbook.md
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/013-prods/checklists/{g15,g16,g17,f11,f15,f20,q10,q12,q15,b99,decisions}.md
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/014-evals/README.md
```

---

## Stop lines

Do not claim `lazuar-pay-checkout` is still a health probe. It is a `/c/{token}` cash-register pixel on `:5179` that calls public `/v1/pay/{token}` with **no** Bearer and redirects to Stripe Checkout `mode=payment`.  
Do not claim `GET /v1/checkouts/{id}` is the buyer door. It is still member-gated.  
Do not claim NP-CHK-005/006/007 or NP-BUY-001 are `done` in 011 — this paper does not flip them; K-track `[x]` over-claims verifying, required email, CORS-on-pay tests, and `payer_required`.  
Do not retarget `lazuar-portal` (`:3004`) at 8081.  
Do not register `:5179` as an One OIDC redirect.  
Do not treat Stripe `mode=setup`, `?status=verifying`, or a success query string as paid. The SPA currently avoids the green lie by ignoring the query — and then shows **Pay** again. That is not verifying honesty.  
Do not say “we will auto-charge” on a reminder-only rail. Do not render GrabPay / TnG / FPX / DuitNow tiles on this origin. Hop-2 is the PSP page.  
Do not collect raw PAN here.  
Buyers are not Zitadel humans. Fail the slice if login appears.

---

*End of 03 — Checkout Vite (`lazuar-pay-checkout` `:5179`). Evaluation only. 24 August 2026. Pay `ee2db8e5` on `main`.*
