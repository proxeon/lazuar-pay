# 05 — Production hosted pay page (`lazuar-pay-checkout` `:5179`), not `lazuar-portal`

**Date:** 21 August 2026  
**Program:** [013-prods](./README.md) — production-ready new Pay, then replace the old tree  
**Slice:** hosted buyer cash register + shareable pay link on **`apps/lazuar-pay-checkout`**, origin **`http://localhost:5179`**.  
**Kind:** analysis only. No implementation in this slice. No product-code change. No flip of [011/11](../011-new-lazuar-pay/11-checklist.md) cells to `done`.  
**Audience:** a developer who is about to grow the `:5179` Vite scaffold into the page a Malaysian buyer opens without a One account, and who must not “just point `lazuar-portal` at 8081.”

This paper is about **the buyer plane in the browser**. It does not design Stripe webhook HMAC, the journal, `RCPT-` numbering, BYOK encryption, or merchant OIDC. Those live in sibling 013 papers (`06-money-rails`, `07-fulfillment-ledger-docs`, `04-merchant-frontend`, `08-one-identity-production`) and in [011 01-product.md](../011-new-lazuar-pay/01-product.md). It does pin the **fail locks** those papers must not violate from this origin: no Zitadel on checkout, no Pay password form, wrap-rails honesty, success/cancel URLs are not fulfillment, never treat setup-intent as paid.

---

## Locked (do not bargain in later PRs)

From 011/01, 011/03, 011/11, 011/12, 012/p50, 012/p60, and this program’s README:

| Lock | ID | Meaning for `:5179` |
|------|----|---------------------|
| Buyers are not One/Zitadel humans | **NP-XX-013**, **NP-CHK-007** | Fail if this page asks for Zitadel / `:5175` login. Cardholders never become Zitadel users because they bought an ebook. |
| No Pay password form | **NP-XX-007** | No `/login`, no email+password, no Hub `POST /one/auth/login`. |
| Hosted cash register | **NP-CHK-005** | A buyer-facing pay page on Pay’s origin, not Hub `:3004`. |
| Shareable pay link | **NP-CHK-006** | A URL a merchant can paste into WhatsApp. |
| Payer email/name on the session | **NP-BUY-001** | The checkout row in Pay holds who paid. Not a One membership. |
| Success/cancel URLs are not fulfillment | **NP-CHK-002** | Stored on the session. Webhook/handler writes subscription + journal + `RCPT-`. |
| Do not retarget `lazuar-portal` to 8081 | **P60** | Old portal speaks Hub `/public/commerce/*` + `lazuar_auth`. 8081 will 401/404, then someone will “just add login.” |
| Wrap-rails honesty | **NP-GW-007** | Stripe/CHIP can auto-charge **if vaulted**. Billplz-class = reminder + hosted link, never silent debit. The page must say so. |
| Never treat setup-intent as paid | **NP-GW-008** | A `$0` Stripe Checkout `mode=setup` that collected a PM is not a capture. Polling “ACTIVE” / “PENDING” / setup-complete is not paid. |
| Receipts / update-payment later share this origin | 011/01 buyer plane, **NP-BUY-003…005** | Magic link to the **payer mailbox**, not merchant `:5178`. |

**First-slice steps this origin is for** ([011/12](../011-new-lazuar-pay/12-first-slice-tracker.md)):

| Step | Job | IDs | Status at this SHA |
|------|-----|-----|--------------------|
| 9 | Create a product + pay link | NP-CAT-001…005, **NP-CHK-006** | `todo`. Fixture checkout exists; no product, no hosted URL, no `:5179` route. |
| 10 | Buyer (no One account) pays on the hosted page | **NP-CHK-005**, **NP-CHK-007**, **NP-BUY-001** | `todo`. Vite is a health probe. **Fail if Zitadel login appears.** |

Pass of the 011 dogfood sentence still requires steps 11–12 (webhook + journal + `RCPT-` + merchant ops). This paper does not pretend those are a frontend job. It does refuse any UI that would make 11–12 lie (unlocking access on `?payment=success`, titling a receipt Tax Invoice, saying “we will charge your card” on Billplz).

---

## Repos and SHAs (as read)

| Repo | Path | Branch | Full SHA | Short | Tip |
|------|------|--------|----------|-------|-----|
| **Lazuar Pay** (this tree) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` | `feat/012-connect-one` | `6f866ff0489a4de77d2fc1b1bbcfa87fbe72b80f` | `6f866ff0` | `feat(pay): scaffold merchant and checkout Vite apps` (2026-08-21 15:15:51 +0800) |
| **Lazuar One** (sibling) | `/Users/akmalfirdaus/Code/lazuar/lazuar-one` | `main` | `0f79fe4f6503847881286ead2e7e57b7c7dc1808` | `0f79fe4` | `WIP: Thu Aug 20 21:24:22 +08 2026` (2026-08-20 21:24:22 +0800) |

`git rev-parse HEAD` and `git log -1` were run in both working copies on 21 Aug 2026. Pay’s working tree is **not** clean: `?? plans/013-prods/` is this analysis folder. One’s tree is the same WIP commit 012 papers already pinned. If either tree moves, re-pin before treating path lists as frozen.

**.NET SDK pin (Pay host):** `10.0.100`. **pnpm pin (Pay repo):** `packageManager: pnpm@11.5.2`. Checkout Vite is `vite ^8.2.0`, React `^19.2.8`. Portal (museum) is Next `16.2.9` on port **3004**.

**What “Pay” means in this paper**

- The **new focused host** is `apps/lazuar-pay` (`Lazuar.Pay`) on **http://localhost:8081**. It serves health, whoami, org-ready, and an **in-memory fixture** `POST/GET /v1/checkouts`. Status is always `"open"`. Not a charge.
- The **new buyer origin** is `apps/lazuar-pay-checkout` on **http://localhost:5179**. Health probe only. This is the production destination for NP-CHK-005/006.
- The **new merchant origin** is `apps/lazuar-pay-merchant` on **http://localhost:5178**. OIDC later. Not this paper’s SPA.
- The **old Hub portal** is `apps/lazuar-portal` on **http://localhost:3004**. Next.js App Router, Hub cookie `lazuar_auth`, Hub `/public/commerce/*`. **Do not retarget.** Steal judgment, not routes.
- The **old Hub API** is `apps/lazuar-api` on **8080** (collides with One). Checkout Vite must not call it.
- The **identity plane** is Lazuar One API on **8080** `/api/v1` and product login **`:5175`**. Checkout Vite must not call those either.

If a sentence does not say **focused Pay** vs **old Hub** vs **One**, assume it is wrong.

---

## 1. Method / SHAs

Nothing was implemented. The following were opened in full or in the cited ranges.

### 1.1 This paper’s production target (focused Pay)

| Path | Why |
|------|-----|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-checkout/package.json` | Port 5179, `strictPort`, React 19, no router, no OIDC, no `@repo/api-types-ts` |
| `…/README.md` | “Buyers have no One account. Fail if this page asks for Zitadel login.” |
| `…/vite.config.ts` | Dual-pin 5179; never silently steal merchant 5178 |
| `…/index.html`, `src/main.tsx`, `src/App.tsx`, `src/App.css`, `src/index.css` | Entire runtime: health `fetch` to 8081 |
| `…/tsconfig.json`, `tsconfig.app.json`, `tsconfig.node.json` | Bundler mode; no path aliases into Hub |
| `apps/lazuar-pay-merchant/src/App.tsx`, `README.md`, `package.json` | Contrast: same scaffold, **OIDC later**, origin 5178 |
| `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` | CORS already allows 5178 and **5179**; maps checkouts |
| `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs` | `POST /v1/checkouts`, `GET /v1/checkouts/{id}` both `MemberGate` |
| `…/CheckoutSession.cs`, `CreateCheckoutRequest.cs`, `CheckoutStore.cs` | Fixture shape; in-memory; always `"open"` |
| `apps/lazuar-pay/src/Lazuar.Pay/One/MemberGate.cs`, `Bearer.cs`, `PayErrors.cs` | 401 without Bearer; 403 not a member |
| `apps/lazuar-pay/src/Lazuar.Pay/One/WhoamiEndpoints.cs` | Merchant session projection. Checkout must not call this. |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/CheckoutTests.cs` | Create without Bearer = 401; GET other-org = 403; always `status: open` |
| `…/CorsTests.cs` | 5179 allowed on `/health`; ops `:3003` **not** allowed |
| `packages/pay-spec/main.tsp`, `README.md` | Contract: merchant create + merchant GET; comment “Not a charge” |
| `apps/lazuar-pay/README.md` | `task pay:checkout`; “Buyer has no One account”; curl create still Bearer |
| `Taskfile.yml` `pay:checkout` | `pnpm --filter lazuar-pay-checkout dev` |
| `pnpm-workspace.yaml` | `apps/*` includes the Vite app |
| `turbo.json` | Generic `dev` persistent; no special checkout task |

### 1.2 Product law (011 / 012)

| Path | Why |
|------|-----|
| `plans/011-new-lazuar-pay/01-product.md` | Buyer plane; wrap-rails; magic link to payer mailbox; public door `POST /v1/checkouts` |
| `plans/011-new-lazuar-pay/02-one-integration.md` § “Two planes” | Merchant staff = One humans. Buyer = Pay checkout profile |
| `plans/011-new-lazuar-pay/03-first-slice.md` | Steps 9–10; fail if buyer created as Zitadel human / Pay password / setup counted as paid |
| `plans/011-new-lazuar-pay/11-checklist.md` | `NP-CHK-*`, `NP-BUY-*`, `NP-GW-007/008`, `NP-API-001/003`, `NP-XX-013` |
| `plans/011-new-lazuar-pay/12-first-slice-tracker.md` | Ordered steps 9–10 still `todo` |
| `plans/012-one-to-pay/checklists/p50-money.md` | Fixture `POST /v1/checkouts` + status GET on Pay `/v1`; **“Buyer pays without a One account” still unchecked** |
| `plans/012-one-to-pay/checklists/p60-old-frontends.md` | Do not retarget portal; hosted checkout origin is `:5179` |
| `plans/012-one-to-pay/checklists/p10-spa-oidc.md` | Origins exist; OIDC unwired. **Trap:** lists 5178 **and** 5179 together. Checkout must never receive OIDC. |
| `plans/012-one-to-pay/04-pay-spec-contract.md` | pay-spec must not import `/public/commerce`; `@repo/pay-types-ts` only when a UI calls `/v1` for real |
| `plans/012-one-to-pay/05-local-topology.md` | CORS section written **before** 5178/5179 existed; “future Pay hosted checkout TBD” is now 5179 |
| `plans/012-one-to-pay/10-dogfood-and-tests.md` | Pointing old ops/portal at 8081 is a fail; first connect is not S1 money |

### 1.3 Museum: Hub portal (steal judgment / refuse folders)

| Path | Why |
|------|-----|
| `apps/lazuar-portal/package.json` | Next 16, port **3004**, `@repo/api-types-ts` (Hub spec) |
| `apps/lazuar-portal/README.md` | Still the `create-next-app` stub (mentions `:3000`). Not a product surface. |
| `apps/lazuar-portal/next.config.ts` | `basePath: NEXT_BASE_PATH` for `https://hub.lazuar.com/portal` |
| `apps/lazuar-portal/src/**` | Full route + module inventory in §5 |
| `plans/008-evals/07-ops-portal-admin-frontend.md` §16–21 | **Current** portal honesty (TIN live, `/pay/{id}` remounted, cookie vs token forks). Prefer this over 007-09 for “what the UI does today.” |
| `plans/009-bugs/03-commerce-dunning-arrears-portal.md` | Magic-link HMAC, arrears RM 1, success URL drops token, always-200 magic-link |
| `plans/009-bugs/01-commerce-checkout-activation.md` | Hop-1/hop-2, `mode=setup` vs `ProcessZeroAmount`, OPEN/COMPLETED/EXPIRED |
| `plans/007-feats/09-checkout-and-payment-links.md` | Competitor bar + two-hop architecture. **Stale on portal pixels** (claims `/pay` is `notFound()`, TIN hidden, no quantity). Use for Stripe/CHIP/Billplz hop-2 shape and conversion hole, not for 2026-08-21 portal inventory. |
| `plans/010-failed-tests/02-initiate-checkout-qty-sst.md` | Hop-2 Amount is unit, adapters × quantity; SST fail-closed |

### 1.4 Museum: Hub hop-2 adapters (how the page talks to PSPs)

| Path | Why |
|------|-----|
| `apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs` `CreateCheckoutSessionOptions` | Hosted Checkout: `mode=payment`, or `mode=setup` when amount 0 + `setupFutureUsage`. Not Stripe Elements. |
| `…/ChipCollectGatewayAdapter.cs` | `POST https://gate.chip-in.asia/api/v1/purchases/` → `checkout_url` |
| `…/BillplzGatewayAdapter.cs` | Hosted bill URL; reminder-only rail |
| `apps/lazuar-api/Modules/Payments/Contracts/PaymentGatewayCapabilities.cs` | Off-session = Stripe or CHIP only. Everything else is reminder-only. `SupportsEmandate` is **false**. |

### 1.5 What was **not** opened as SoT (and why)

- One `apps/lazuar-app` OIDC config — merchant paper, not checkout.
- Old Hub `PublicCheckoutEndpoints.cs` line-by-line — 009-01 and 007-09 already mapped hop-1 HTTP. This paper needs the **buyer pixel** and the **new** `/v1` split.
- `examples/hub-cashier-next` — already the honesty sample that success_url is not paid. Judgment stolen; app not copied.
- Live Billplz / Stripe / CHIP sandbox purchase on 21 Aug 2026. Hop-2 method grids are adapter payloads + 007-09 public docs, same as that paper’s method refuse.

### 1.6 SHA drift you must not flatten

| Older paper | Claim that is **false** on `6f866ff0` |
|-------------|----------------------------------------|
| 012/04 pay-spec | TypeSpec is health-only. **Now** whoami + org-ready + checkouts exist. |
| 012/05 topology | Focused Pay has no CORS, no browser origin. **Now** CORS allows 5178/5179; Vite scaffolds exist. |
| 012/10 dogfood | Host maps only `/health`. **Now** whoami + fixture checkouts. Connected is further along; S1 hosted page is not. |
| 007-09 checkout | Portal `/pay/{id}` is `notFound`; TIN `[MVP-HIDE]`; no quantity stepper. **008-07 re-checked:** `/pay/{id}` is QuoteView; TIN validates; quantity exists. |
| Root `README.md` | `lazuar-portal` **is** “the cash register.” For new Pay it is **not**. |

---

## 2. What checkout Vite is today

### 2.1 Package and listen

`apps/lazuar-pay-checkout/package.json` at `6f866ff0`:

| Field | Value |
|-------|--------|
| `name` | `lazuar-pay-checkout` (private `0.0.0`) |
| `dev` | `vite --port=5179 --host=0.0.0.0 --strictPort` |
| `preview` | `vite preview --port=4179 --strictPort` |
| `build` | `tsc -b && vite build` |
| `lint` | `oxlint` |
| Runtime deps | `react`, `react-dom` **only** |
| Dev deps | Vite 8, plugin-react, TypeScript ~6, `@types/*`, oxlint |
| Not present | `oidc-client`, `react-router`, `openapi-fetch`, `@repo/api-types-ts`, `@lazuar/one-client`, cookie helpers, Stripe.js, CHIP.js |

`vite.config.ts` dual-pins the same port and comments the footgun:

> `strictPort: fail loud if 5179 is busy — never silently steal merchant :5178.`

`task pay:checkout` is `pnpm --filter lazuar-pay-checkout dev`. Host README lists it next to `pay:merchant`. `mprocs-dev.yaml` still autostarts **old** `lazuar-portal` on 3004 with `NEXT_BASE_PATH=/portal`. Compose still publishes **3004**. Nothing in Docker/mprocs starts 5179 yet. That is correct for dual-run: old portal stays the Hub cash register until cutover; new checkout is a host Vite.

### 2.2 Entire UI

`index.html` title: `Lazuar Pay — checkout`. One `#root`. `src/main.tsx` is `StrictMode` + `<App />`. No router. No env file. No `.env.example`.

`src/App.tsx` (complete behaviour):

1. `payApi = import.meta.env.VITE_PAY_API_URL ?? 'http://localhost:8081'`
2. `useEffect` `fetch(`${payApi}/health`)` → `{ status }` or `'unreachable'`
3. Renders kicker “Lazuar Pay”, h1 “Checkout”, a paragraph that buyers have no One account and no Pay password form, and a `<dl>` of origin `:5179`, Pay API URL, and `/health` status.

It does **not**:

- Read a checkout id from the path or query
- Call `GET /v1/checkouts/{id}`
- Collect payer name/email
- Redirect to Stripe/CHIP
- Poll paid
- Set a cookie
- Send `Authorization`
- Import anything from `apps/lazuar-portal` or `@repo/api-types-ts`

README is the product lock in English:

> Buyers have **no** One account. Fail if this page asks for Zitadel login. Receipts / update-payment can share this origin later (magic link to the payer mailbox), not the merchant shell.

### 2.3 Contrast with merchant Vite (`:5178`)

Same React/Vite/oxlint scaffold. `App.tsx` copy differs: “Staff shell… Sign-in is One login at `:5175`.” Merchant README: “OIDC is not wired yet. Do not add a password form. Send `access_token` as Bearer… never `id_token`.”

| | Merchant `:5178` | Checkout `:5179` |
|--|------------------|------------------|
| Who | One human (Ada, invited MEMBER) | Cardholder / invoice payer |
| Auth later | OIDC PKCE → Bearer to Pay `/v1/whoami` and `/v1/checkouts` **POST** | **None.** Public read + pay. |
| Calls One? | Yes, indirectly (login `:5175`, then Pay whoami → One `/me`) | **Never** |
| Fail lock | Password form, `id_token` as Bearer, Hub ops routes | Zitadel login appearing, Hub cookie, `/public/commerce` |
| CORS on 8081 | Yes (`localhost` + `127.0.0.1` twins) | Yes (same) |

**P10 trap.** [p10-spa-oidc.md](../012-one-to-pay/checklists/p10-spa-oidc.md) P10.1 checks “Pay UI origins exist: merchant `:5178` **and** checkout `:5179`.” P10.2 then lists “Register OIDC SPA” without splitting origins. Registering **checkout** as a One app, adding `http://localhost:5179/callback` to `REDIRECT_ALLOWLIST`, or mounting `oidc-client` on this Vite, **is NP-CHK-007 failing**. OIDC is a **merchant** job. Checkout’s “unwired” is the finished state, not a backlog item.

### 2.4 Isolation that already holds (keep)

| Check | Evidence |
|-------|----------|
| No Hub OpenAPI types | `package.json` deps: react only |
| No Hub cookie name | No `lazuar_auth` string in `apps/lazuar-pay-checkout/src` |
| No One login URL | README forbids Zitadel; App.tsx copy matches |
| Port collision | `strictPort` 5179 vs merchant 5178 vs One login 5175 vs One app 5174 vs Hub portal 3004 |
| Pay host isolation | `IsolationTests` still bans `lazuar-api` / MediatR in the C# host. Vite is a separate package; do not add a workspace dep on `@repo/api-types-ts`. |

### 2.5 What “production hosted pay page” still lacks (the gap this paper is for)

The dogfood step 10 page is not this health card. Missing, on purpose, because the fixture is not a charge:

1. A URL shape (`/c/{id}` or `/pay/{token}`) a merchant can copy.
2. A **public** GET the browser can call without Bearer (today’s GET is member-gated — §3).
3. States: open / paid / expired / missing (§4).
4. Payer name + email fields (§7). NP-BUY-001 is `todo`; the fixture session has neither field.
5. A Pay button that starts Stripe Checkout or CHIP purchase (or Billplz bill) and **redirects** (§6).
6. A return surface that polls Pay status and **does not** unlock on the query string (§4, NP-CHK-002, NP-GW-008).
7. Wrap-rails copy: vaulted vs reminder-only (§6).
8. Tests that a buyer journey never hits `:5175` or `/v1/whoami`.

Until those exist, `task pay:checkout` proves **CORS + process liveness**, which CorsTests already pin for `/health`. It does not prove NP-CHK-005.

---

## 3. Public vs merchant-authenticated routes (pay link must work without Bearer; merchant create stays on 8081 with member gate)

### 3.1 What 8081 actually maps

`Program.cs` at this SHA:

| Method | Path | Auth | Who it is for |
|--------|------|------|----------------|
| `GET` | `/health` | none | Probes (Vite already calls this) |
| `GET` | `/v1/health` | none | pay-spec health |
| `GET` | `/v1/whoami` | Bearer → One `/me` | **Merchant** (and later `lzr_sk_`) |
| `GET` | `/v1/orgs/{orgId}/ready` | Bearer + `authz/check member` | **Merchant** dummy admin |
| `POST` | `/v1/checkouts` | Bearer + member of `body.org_id` | **Merchant create** (fixture) |
| `GET` | `/v1/checkouts/{id}` | Bearer + member of **session.org_id** | **Merchant read** (fixture) |

There is **no** `POST /v1/auth/login`. There is **no** cookie middleware. CORS is origin-allow 5178/5179, `AllowAnyHeader` + `AllowAnyMethod`, **no** `AllowCredentials`. That last fact is load-bearing: a Hub-style `credentials: "include"` cookie session cannot be smuggled onto 8081 even if someone pastes `lazuar_auth` from `:3003`.

### 3.2 TypeSpec vs host (aligned, both merchant-only)

`packages/pay-spec/main.tsp` Checkouts interface:

```tsp
/** Merchant creates a fixture checkout. org_id is the One tenant id. Requires Bearer + member. */
@post @route("/checkouts") create(…): CheckoutSession;

/** Merchant reads a checkout they are a member of. */
@get @route("/checkouts/{id}") get(@path id: string): CheckoutSession;
```

Comments are honest. The production hosted page **cannot** use this GET as specified. NP-API-003 (“`GET` payment status”) is marked `done` in 011/11 as `GET /v1/checkouts/{id}; other org 403`. That is **merchant** status, not buyer status. Flipping NP-CHK-005 to `done` while leaving GET member-gated would be a lying cell: the cash register cannot load the bill.

### 3.3 MemberGate on both verbs (evidence)

`CheckoutEndpoints.Create`:

1. `MemberGate.RequireMemberAsync(request, one, body.OrgId)`
2. Amount `> 0` else 400
3. Currency default `MYR`
4. Idempotency from header or body
5. Persist `status = "open"`
6. 201 JSON snake_case

`CheckoutEndpoints.Get`:

1. `store.Get(id)` → 404 `"Checkout not found"` **before** One is called (CheckoutTests `Get_unknown_is_404` asserts `One.SendCount == 0`)
2. Then `MemberGate` on `session.OrgId`
3. 200 JSON if member

`MemberGate`:

- No `Authorization: Bearer …` → **401** `"Missing bearer token"`
- Empty org → 400
- One `authz/check member` allowed → pass
- One 401 → 401 `"Identity provider rejected the token"`
- One 403 or `allowed: false` → 403 `"Not a member of this org"`
- Transport/timeout → 503

CheckoutTests pin: create without Bearer is 401 and **does not call One**; create for `org_id=t2` while One only allows `t1` is 403; GET of t1 session with a t2-only token is 403; idempotency key `k1` returns the same id.

A buyer on `:5179` has no Bearer. Today they would get **401** on GET of a real id, and **404** on a missing id *if they somehow had a Bearer*. Without a Bearer, missing vs existing is: Get still 404s missing without calling One; existing hits MemberGate and 401s. That is a small **existence oracle** (401 ⇒ id exists). Fine for a member API. Wrong for a public pay link if you later drop the gate without adding a second secret — see §10.

### 3.4 Merchant create stays on 8081 with member gate

Do **not** move `POST /v1/checkouts` to the checkout origin or to an anonymous POST the buyer makes with amount in the body. The Bezos door ([011/08](../011-new-lazuar-pay/08-bezos-door.md)) is:

- Merchant ops (Vite `:5178`) or `lzr_sk_` worker → `POST /v1/checkouts` with One JWT / key.
- `org_id` is the One tenant id (NP-ONE-009). Pay does not have a second organizations table (NP-XX-014).
- Buyer never chooses `org_id` or `amount` except where the product later allows PWYW **inside a merchant-created session**.

S1 dogfood step 9 (“create a product + pay link”) is a **merchant** job. The shareable URL is an output of that POST (or of a later `pay_url` field). Checkout Vite **consumes** the link; it does not mint money.

NP-CAT is still `todo`. Until a Product resource exists, the fixture `POST /v1/checkouts { org_id, amount, currency, success_url, cancel_url }` is enough for a **manual** pay link (Ada curls, pastes `:5179/c/{id}`). That is an honest interim. Do not invent a public “create checkout” to skip merchant auth.

### 3.5 What the buyer **may** call (does not exist yet)

Need a public resource, **maybe a different path** than member-gated `GET /v1/checkouts/{id}`. Candidates (decision in §10, not implemented here):

| Shape | Auth | Returns | Risk |
|-------|------|---------|------|
| A. Same GET, drop MemberGate, treat id as a capability URL | none | Full session including `org_id`, success/cancel URLs | Id entropy is 128-bit hex today (`Guid.NewGuid().ToString("N")`). Oracle + data leak if ids ever become sequential. Merchant GET and buyer GET share cache/logs. |
| B. `GET /v1/pay/{public_token}` buyer-safe DTO | none | amount, currency, status, merchant display, rail honesty, whether payer fields are needed. **No** One org internals. | Two identifiers to mint and store. Token in the shareable URL. |
| C. `GET /v1/checkouts/{id}?token=` HMAC like Hub arrears | query token | same as B | Hub’s magic-link bugs (009-03): fallback secret, non-constant-time compare, unencoded `=`, success URL dropping token. Do not clone `MagicLinkTokenService` as-is. |
| D. Buyer `POST /v1/pay/{id}/start` (name, email) → `{ redirect_url }` | none (id/token capability) | PSP hosted URL | This is how hop-2 starts. Must not accept a new amount. Must refuse expired/paid. |

**Recommendation direction (not a silent decision):** keep **merchant** `GET /v1/checkouts/{id}` member-gated (NP-API-003 as it is). Add a **buyer** resource (B + D). Do not “open the member GET” to ship the page faster.

### 3.6 What the buyer must **not** call

| Path | Why |
|------|-----|
| `GET /v1/whoami` | One `/me` projection. Would 401 without Bearer; with a stolen staff JWT it would paint Ada on the cash register. |
| `GET /v1/orgs/{id}/ready` | Member gate. |
| One `GET /api/v1/me` | Checkout origin is not on One CORS (012/05). Adding it would be the Zitadel footgun. |
| One `:5175` | Product login. NP-CHK-007 fail. |
| Hub `POST /one/auth/login`, `GET /one/auth/me` | Homemade IdP. P60. |
| Hub `POST /public/commerce/checkout`, `GET /public/commerce/{slug}/products/{slug}` | Cathedral. pay-spec README forbids importing these routes. |
| Hub `GET /public/one/{slug}/branding` | Hub One module, not real One. Branding for S1 can be a Pay field on the session/org, not a Hub public route. |

### 3.7 CORS as it is (buyer `fetch` from 5179 already allowed)

`Program.cs`:

```csharp
p.WithOrigins(
    "http://localhost:5178",
    "http://127.0.0.1:5178",
    "http://localhost:5179",
    "http://127.0.0.1:5179")
.AllowAnyHeader()
.AllowAnyMethod();
```

`CorsTests.Health_allows_checkout_origin` pins `Access-Control-Allow-Origin: http://localhost:5179` on `GET /health`. `Health_does_not_allow_ops_origin` pins **no** ACAO for `http://localhost:3003`. There is **no** test yet that `GET /v1/checkouts/{id}` or a future public pay GET returns ACAO for 5179 (including preflight `OPTIONS`). Add that when the public resource exists. Do **not** add `:3004` to this list “so portal can dual-run against 8081.”

`localhost` ≠ `127.0.0.1` (One issue 077; 012/05). Both twins are already listed. Keep them.

No `AllowCredentials`. Keep it that way for checkout. Merchant Bearer is an `Authorization` header (`AllowAnyHeader`). Checkout should stay header-less or capability-URL.

### 3.8 Sequence that must stay true

```text
Ada (One human)
  :5175 login → access_token
  :5178 merchant (later) or curl
       POST http://localhost:8081/v1/checkouts
       Authorization: Bearer <access_token>
       { org_id, amount, currency, success_url, cancel_url }
       → 201 { id, status: open, … }

Ada copies
  http://localhost:5179/c/<id-or-public-token>

Buyer (not a One human)
  GET http://localhost:8081/v1/pay/<token>     # no Bearer
  fill name + email if empty
  POST http://localhost:8081/v1/pay/<token>/start
       → { redirect_url: https://checkout.stripe.com/… or CHIP/Billplz }
  window.location = redirect_url

PSP
  capture / fail / cancel
  webhook → Pay 8081 (not the Vite app)
  redirect → success_url or cancel_url (often back to :5179)

Buyer
  :5179 success view polls public status
  COMPLETED/paid → “we got it; receipt by email”
  anything else → wait / expired / try again
  never “you’re in” because the query string said so
```

If step “Buyer GET” requires Bearer, the slice has already failed NP-CHK-007 even if the login UI is “just a spinner.”

---

## 4. Page states: open / paid / expired / missing. Fixture today is always open — production needs GET that buyers can call (maybe a different public resource than member-gated `GET /v1/checkouts/{id}`)

### 4.1 Fixture session shape (always open)

`CheckoutSession` C# + pay-spec model:

| Field | Fixture | Notes |
|-------|---------|-------|
| `id` | `Guid.NewGuid().ToString("N")` | 32 hex chars. Unguessable **if** this entropy stays. Not a `RCPT-` number (NP-DOC-002). |
| `org_id` | from body | One tenant id |
| `amount` | decimal `> 0` | No SST, no seats, no currency minor-unit honesty beyond JSON decimal |
| `currency` | default `MYR` | Uppercased |
| `status` | **always `"open"`** | No paid, no expired, no void. NP-CHK-004 is `todo`. |
| `success_url` | optional | Stored. Not used by any redirect yet. NP-CHK-002 `done` as storage only. |
| `cancel_url` | optional | Same |
| `created_at` | UTC now | No `expires_at` |
| payer email / name | **absent** | NP-BUY-001 `todo` |
| `hosted_url` / `pay_url` | **absent** | NP-CHK-006 `todo` |
| rail / `is_reminder_only` | **absent** | NP-GW-007 cannot be shown |

`CheckoutStore` is a `ConcurrentDictionary`. Process restart wipes sessions. Comment on the store: “In-memory fixture store. Not a ledger. Replace when money is real.” Idempotency key is `org_id + "\n" + key`. Replay returns the **same open session**, not a new one. There is no expire job.

CheckoutTests `Create_and_get_open_session` asserts JSON `status === "open"` after POST and GET. There is no test for `"paid"` because the host cannot produce it.

### 4.2 Hub states (judgment to steal, not the enum to copy)

Old Commerce `CheckoutSession` (009-01 files table): **OPEN / COMPLETED / EXPIRED**. Status poller (`CommerceQueryService.Checkout.cs`) maps COMPLETED / EXPIRED / else **PENDING**. Portal success view (`CheckoutSuccessView.tsx`) treats **only `COMPLETED` as paid**:

```51:61:apps/lazuar-portal/src/modules/checkout/components/CheckoutSuccessView.tsx
        // Paid only when commerce session is COMPLETED. Never treat ACTIVE / PENDING / EXPIRED as success.
        if (response.status === "COMPLETED") {
          if (response.token) setAccessToken(response.token);
          setStatus("SUCCESS");
          return;
        }

        if (response.status === "EXPIRED") {
          setStatus("EXPIRED");
          return;
        }
```

That comment is the NP-GW-008 pixel. Steal it. Hub still has a backend trap: Stripe `TryMapSetupIntentSucceeded` emits event type `"PAYMENT_COMPLETED"` with amount 0 (`StripeGatewayAdapter.cs` ~659–685). Money-rails paper owns not booking that as cash. The **page** must not show “Order Complete!” because a setup session completed.

Hub expiry: `CheckoutSessionExpiryJob` expires OPEN past `ExpiresAt` (24h create, 5-min loop). 007-09: no abandon email. New Pay S1 can expire without mail. Need **some** TTL so a leaked pay link does not live forever.

Hub success poll: 20 attempts × 3 s (`CheckoutSuccessView`; 007-09 quoted 10×2.5s — **drift**; live code is 20×3000ms). Timeout copy is “check your email,” not “paid.” Steal that. The dashboard link on timeout/success often **drops the magic token** (009-03 B03-C09; success URL minted without `?token=`). New `:5179` success must not send the buyer to a tokenless Hub `/portal`. If S1 has no buyer dashboard yet, the CTA is “close this tab; receipt will be emailed” (NP-MAIL-001 still `todo` — then the CTA is even more modest: “Pay has the payment if the merchant’s webhook is live”).

### 4.3 States the `:5179` page must render

| State | How the buyer got there | Pixel | Pay GET |
|-------|-------------------------|-------|---------|
| **loading** | First paint | Skeleton / “Loading payment” | in flight |
| **missing** | Unknown id/token, 404 | “This payment link is not valid.” No login form. | 404 (same for authorized-missing and unauthorized-missing on the **public** resource — no 401/403 fork that confirms existence unless the token is the secret) |
| **open** | Unpaid, unexpired | Amount, currency, merchant name, payer fields, Pay CTA, wrap-rails sentence | `status=open` |
| **paid** | Webhook already completed, or poll flipped | “Paid. This link cannot be paid again.” Receipt later. **Not** “Tax Invoice.” **Not** access grant. | `status=paid` (or Hub’s COMPLETED — pick one string and put it in pay-spec) |
| **expired** | TTL or merchant void | “This link has expired.” No Pay button. | `status=expired` |
| **cancelled return** | PSP `cancel_url` | Amber “Payment was cancelled.” Session still **open** unless Pay expired it. Pay button still works. | still `open` |
| **success return / verifying** | PSP `success_url` | Spinner. Poll public status. | `open` until webhook |
| **success return / timeout** | Poll exhausted, still open | “Still confirming. Check email / ask the merchant.” Retry poll. **Not paid.** | `open` |
| **blocked / reminder-only mismatch** | later | Do not show “Update card” on Billplz (009-03 B03-C06) | rail flag |

NP-CHK-004 is `open → paid / expired`. The page is the only buyer-visible proof of that state machine. A fixture that cannot leave `open` cannot ship as NP-CHK-005 even if the Vite looks like Stripe.

**Naming:** Hub used COMPLETED because Commerce sessions doubled as subscription handles (`?sub_id=`). New Pay should not call the checkout id `sub_id`. Success query can be `?checkout=` or path `/c/{id}/return`. Do not reuse `sub_id` and then poll a subscription.

### 4.4 Success/cancel URLs (NP-CHK-002)

Fixture already stores them. Production rules:

1. Merchant **may** pass their own site (`https://course.example/thanks`). That page is **marketing**. It is not entitlement.
2. Pay **should** also own a return URL on `:5179` so a merchant who leaves the fields empty still gets verifying/expired pixels.
3. PSP is configured with those URLs at hop-2 mint time (Stripe `SuccessUrl`/`CancelUrl`, CHIP `success_redirect`/`cancel_redirect`/`failure_redirect`, Billplz `redirect_url`).
4. Fulfillment is the **webhook handler** (NP-FUL-001). The Vite app never writes `status=paid`.
5. `examples/hub-cashier-next` already teaches (3)+(4). Do not unlearn it because a product manager wants a green check at `?payment=success`.

Hub bug to not copy: custom initiate success URL `/{slug}/checkout/custom/success` existed as a **page** later (008-07), but 007-09 recorded a stretch where the URL 404’d. New Pay: if you put it on the session, the route must exist on **5179**, not on 3004.

### 4.5 Public GET payload (buyer-safe)

Merchant GET can keep the fixture DTO (`org_id`, success/cancel, created_at). Buyer GET should be a **subset plus display**:

| Field | Buyer needs? | Leak if public? |
|-------|--------------|-----------------|
| `id` / public token | yes | it’s in the URL |
| `status` | yes | no |
| `amount`, `currency` | yes | it’s the bill |
| `expires_at` | yes | no |
| merchant display name / logo | yes (007-09 conversion: lead with the merchant, not “Powered by Lazuar”) | branding is public |
| `org_id` (One uuid) | **no** | enumerates tenants |
| `success_url` / `cancel_url` | no (browser already redirected) | may contain merchant secrets / tokens |
| payer email (masked) | maybe | PII |
| `is_reminder_only` / rail class | **yes** (NP-GW-007 copy) | no |
| `setup_only` | **yes** (NP-GW-008: “we are saving a card, not charging”) | no |
| line items / product name | S1 yes if product exists | no |

Until products exist, display name can be “Payment to {merchant}” + amount.

### 4.6 Missing vs unauthorized

On the **public** resource: unknown token → **404** (or a generic 404 HTML from Vite). Do not 401 (that implies an auth wall). Do not 403 (that implies the buyer should log in as a member). Do not render Hub’s lock-icon landing (“use the magic links sent to your email”) as the 404 — that trains the buyer to look for a login.

Vite has no `not-found.tsx` today. When a router exists, missing is a first-class state, not a React error boundary.

---

## 5. Mapping old portal flows (hop-1, hop-2, quote, arrears, magic link) → keep / later / refuse

### 5.1 What Hub portal **is** (inventory, 21 Aug 2026)

Stack: Next.js App Router 16.2.9, React 19.2.4, Tailwind 4, shadcn island, `openapi-fetch` + **`@repo/api-types-ts`** (Hub `packages/api-spec`). Dev: `next dev -H 0.0.0.0 -p 3004`. Production `basePath` `NEXT_BASE_PATH` (Caddy `/portal`, `https://hub.lazuar.com/portal`). README is still create-next-app (`localhost:3000`). Dockerfile exists for Hub images — **do not** retarget it at 8081.

**Auth:** server client (`src/modules/core/lib/server-client.ts`) forwards cookie **`lazuar_auth`** to Hub `API_URL` default `http://localhost:8080/api/v1`. Browser client (`modules/checkout/lib/api.ts`) uses `credentials: "include"` to the same Hub URL. Checkout product page **also** `GET /one/auth/me` + `GET /one/me/entitlements` to paint IdentityBanner. Portal layout `GET /one/auth/me` to show Logout. This is the Hub homemade IdP, **not** One Zitadel. Pointing it at focused Pay does nothing useful; pointing it at real One `/api/v1/me` would still be the wrong cookie.

**Root `src` files (every TS/TSX/CSS/mjs, excluding `node_modules`):**

#### App Router pages

| File | URL | Live? | Job |
|------|-----|-------|-----|
| `src/app/page.tsx` | `/` | Yes | Lock icon + “use the magic links sent to your email.” No tenant picker. |
| `src/app/layout.tsx` | (root) | Yes | Geist fonts, locale, **Lazuar** footer Terms/Privacy/Refund |
| `src/app/not-found.tsx` | unknown | Yes | Localized 404 |
| `src/app/globals.css` | | Yes | Tailwind |
| `src/app/accept-invite/page.tsx` | `/accept-invite` | Yes | **Redirects to ops `:3003`**. Staff invite, not buyer. |
| `src/app/legal/layout.tsx` | | Yes | Legal chrome |
| `src/app/legal/terms/page.tsx` | `/legal/terms` | Yes | Platform-as-processor; magic-link access; Malaysia law; last updated June 2026 |
| `src/app/legal/privacy/page.tsx` | `/legal/privacy` | Yes | Lists Resend for receipts/magic links |
| `src/app/legal/refund/page.tsx` | `/legal/refund` | Yes | Creator is MoR; cancel via magic-link dashboard |
| `src/app/[tenantSlug]/layout.tsx` | `/{slug}/*` | Yes | Fetches Hub branding; sets CSS `--brand` |
| `src/app/[tenantSlug]/page.tsx` | `/{slug}` | Yes | Redirect to `/{slug}/portal` (keeps `?token=`) |
| `src/app/[tenantSlug]/checkout/[productSlug]/layout.tsx` | | Yes | `CheckoutI18nProvider` + `CheckoutHeader` (logo, EN/BM) |
| `src/app/[tenantSlug]/checkout/[productSlug]/page.tsx` | `/{slug}/checkout/{product}` | Yes | **Hop-1.** Public product GET + optional Hub `/one/auth/me` |
| `src/app/[tenantSlug]/checkout/[productSlug]/success/page.tsx` | `…/success` | Yes | Polls Hub status (`CheckoutSuccessView`) |
| `src/app/[tenantSlug]/checkout/custom/success/page.tsx` | `/{slug}/checkout/custom/success` | Yes | Same poller, displayName “Payment request” |
| `src/app/[tenantSlug]/pay/[sessionId]/page.tsx` | `/{slug}/pay/{id}` | Yes | **Quote hop-1.** `QuoteView` (008-07: remounted; 007-09 stale `notFound`) |
| `src/app/[tenantSlug]/portal/layout.tsx` | | Yes | “Buyer Dashboard” header; Logout **only if Hub cookie** |
| `src/app/[tenantSlug]/portal/page.tsx` | `/{slug}/portal` | Yes | Magic-link form **or** subscriptions + documents |
| `src/app/[tenantSlug]/update-payment/[subId]/page.tsx` | `/{slug}/update-payment/{id}` | Yes | Arrears / RM 1 card update. **404 without `?token=`** |

#### Checkout module (mounted)

| File | Job |
|------|-----|
| `src/modules/checkout/components/CheckoutView.tsx` | Client orchestrator: coupon, qty, interval, SST gross, trial due-today 0 |
| `src/modules/checkout/components/CheckoutForm.tsx` | Name/email/phone/address/TIN; `POST /public/commerce/checkout`; `window.location.assign(url)` |
| `src/modules/checkout/components/CheckoutLayout.tsx` | `flex-col-reverse` so **amount above form** on mobile |
| `src/modules/checkout/components/CheckoutSuccessView.tsx` | Poll COMPLETED only; TIMEOUT; EXPIRED; token to portal |
| `src/modules/checkout/components/OrderSummaryCard.tsx` | Totals, qty stepper, PWYW, SST line via `grossAmount` |
| `src/modules/checkout/components/PromoCodeInput.tsx` | Validate coupon |
| `src/modules/checkout/components/IdentityBanner.tsx` | Cookie session: guest vs “Use my Lazuar account” vs workspace admin |
| `src/modules/checkout/components/QuoteView.tsx` | Proforma + B2B TIN (no MyInvois validate) + proceed → same `submitCheckout` |
| `src/modules/checkout/types.ts` | `CheckoutContext`, `CheckoutAuthContext` |
| `src/modules/checkout/lib/api.ts` | Hub public commerce client + TIN + idempotency sessionStorage |
| `src/modules/checkout/lib/grossBreakdown.ts` | Exclusive SST on unit × seats. **Judgment to steal.** |
| `src/modules/checkout/lib/grossBreakdown.test.mjs` | Pins hop-1 type 02 / 8% / qty 3 |
| `src/modules/checkout/i18n/CheckoutI18n.tsx` | EN/BM switcher, cookie `lazuar_locale` |
| `src/modules/checkout/i18n/getCheckoutLocale.ts` | `?lang=` → `?locale=` → cookie → `Accept-Language` |
| `src/modules/checkout/i18n/locales.ts`, `messages.ts`, `translate.ts`, `format.ts`, `errors.ts` | Dictionary. Legal/portal/update-payment **not** fully translated (008-07). |
| `src/modules/checkout/i18n/i18n.test.mjs` | Locale resolve |

#### Portal / buyer dashboard (mounted)

| File | Job |
|------|-----|
| `src/modules/portal/components/RequestMagicLinkForm.tsx` | `POST /public/commerce/{slug}/portal/magic-link` — always treats as success |
| `src/modules/portal/components/PortalPlanChange.tsx` | `GET/POST …/portal/plans` with token |
| `src/modules/portal/components/PortalDashboardLink.tsx` | Keeps `?token=` on header link (partial fix of B03-C09; layout still titled “Buyer Dashboard”) |

#### Core

| File | Job |
|------|-----|
| `src/modules/core/lib/server-client.ts` | **Forwards `lazuar_auth`.** Refuse this pattern on 5179. |
| `src/modules/core/lib/branding.ts` | `GET /public/one/{slug}/branding` on **Hub** |

#### Dead / refuse

| File | Job |
|------|-----|
| `src/modules/community/components/CommunityPortalView.tsx` | **Not imported by any route** (008-07). Types `One.AuthUser` from Hub spec. |
| `src/modules/community/lib/api.ts` | Clone of checkout `api.ts` including Hub types |

#### Scaffold junk (do not copy)

| Path | Count / note |
|------|----------------|
| `apps/lazuar-portal/components/ui/*.tsx` | **60** shadcn primitives (accordion … tooltip). Checkout hop-1 does not import them. Sidebar cookie helper lives here unused by buyer pages. |
| `hooks/use-mobile.ts` | Unused by portal app router (008-07: inlined elsewhere in ops/admin) |
| `lib/utils.ts` | `cn()`; QuoteView imports a copy path |

### 5.2 Hub hop-1 / hop-2 (the architecture, not the files)

007-09 still describes the **money path** correctly even where pixels drifted:

```text
[Hop 1 — Lazuar identity / amount page]
  GET  /{tenant}/checkout/{product}     SSR product
  POST /public/commerce/checkout        CRM + session + hop-2 URL
  buyer typed name + email (± phone ± address ± TIN)

[Hop 2 — Processor hosted page]
  Billplz: bill URL. Methods = collection (FPX, wallets if enabled there).
  Stripe: checkout.stripe.com. Methods = Stripe account (card, wallets).
  CHIP: gate.chip-in.asia checkout_url. Methods = brand (FPX, DuitNow, …).
  Buyer may re-enter name/email. Billplz name is email local-part.

[Hop 3 — Return]
  success?sub_id=  → poll GET status
  COMPLETED ⇒ green; else TIMEOUT ⇒ “check your email”
  cancel?cancelled=true → hop 1 amber banner

[Hop 4 — Actual fulfillment]
  Gateway webhook → Commerce Order/Sub + journal/docs
  Success HTML is not entitlement.
```

Aura `/book` **skips hop-1** and redirects to hop-2 (M2M cashier). Scoring only hop-1 would hide that. New Pay S1 is Commerce-like (Pay-hosted cash register) **or** a merchant-copied link that is already a Pay session. It is not Aura.

**Two-hop tax** (007-09): Malaysian buyers see Maybank/FPX on hop-2, after they already submitted a Lazuar form. Stripe Payment Links / CHIP collect links / Billplz Catalog show methods on first paint. That is the conversion hole. S1 still uses two hops because wrap-rails + BYOK means **we do not render FPX**. Improving first-paint methods means Stripe Embedded Checkout / Elements or “skip hop-1 when payer email is already on the session.” See §6. Do not solve it by building a homemade FPX grid (NP-XX-011).

### 5.3 Keep / later / refuse

Legend: **keep** = steal judgment (and maybe a small function) into `:5179`. **later** = still Pay, not S1 dogfood. **refuse** = do not port; museum or One.

#### Keep (S1 cash register)

| Hub thing | Why keep | How it lands on 5179 |
|-----------|----------|----------------------|
| Guest-first: no login wall | NP-CHK-007 | Default. IdentityBanner cookie path is the opposite — refuse that. |
| Name + email required before hop-2 | NP-BUY-001 | Fields on the session. Prefill if merchant created with payer email. |
| Amount above form on mobile (`CheckoutLayout` `flex-col-reverse`) | 007-09 / 008-07 | Same UX. Do not import the TSX; reimplement. |
| Redirect to **hosted PSP page** (not Elements) | wrap-rails, BYOK | `window.location = redirect_url` from Pay. §6. |
| Success poll **only** paid/COMPLETED | NP-GW-008, NP-CHK-002 | Verifying / timeout / expired pixels. |
| Cancel banner, session still open | | `?cancelled=1` on the open page. |
| Exclusive SST on **unit** then × seats | NP-MON-003 (wave V1) / `grossBreakdown.ts` | Display judgment. Fail closed if SST unknown (NP-MON-004). **Do not** port TIN/MyInvois. |
| Shareable URL | NP-CHK-006 | Path on 5179, not `/{hubSlug}/checkout/{productSlug}` on 3004. |
| Lead with **merchant** mark, not “Powered by Lazuar” as the brand | 007-09 psychology | Header: merchant name/logo. Lazuar can be a small processor line. |
| Idempotency on pay-start | NP-CHK-003 already on create | Buyer double-click must not mint two PSP sessions (Hub 009-03 B03-C02 cousin). |
| MYR default | NP-CAT-003 | Fixture already defaults MYR. |
| `strictPort` / dedicated origin | P60 | Already in Vite. |

#### Later (still this origin, not S1)

| Hub thing | IDs | Why later |
|-----------|-----|-----------|
| Product catalog hop-1 (`/{slug}/checkout/{product}`) | NP-CAT, NP-CHK-006 | Step 9 product model is `todo`. Fixture amount-only link is enough for first pay. |
| Quantity stepper 1–99 | NP-CAT-004 | SST unit × seats. After products. |
| Coupons / promo | | Hub reserve/confirm is a cathedral. Not dogfood. |
| Trial “due today 0” + setup hop-2 | NP-GW-008 | Dangerous: Hub `mode=setup` + `ProcessZeroAmount reminderOnly: true`. Only after money-rails honesty. |
| EN/BM i18n | | Steal dictionary **shape**, rewrite strings. Portal/pay/update-payment were English-only (008-07). |
| Quotes / `/{slug}/pay/{id}` / `QuoteView` | NP-SOON-001…003 | Custom amount + proforma PDF. SST on quote must match hop-2. Not v1. |
| Magic-link buyer dashboard | NP-BUY-003…005, NP-MAIL-003 | Receipts, cancel, plan change. Same origin, **payer mailbox**, 24h token. Do not clone Hub HMAC as-is (009-03 C10/C11). |
| Update-payment / arrears page | NP-SOON-004, NP-BUY-004 | Hub RM 1 vs MYR 2 min (B03-C05); decline marks PAST_DUE (B03-C01); reminder-only sold as update-card (B03-C06). Rebuild from wrap-rails, do not port the page. |
| Receipt download | NP-DOC, NP-BUY-005 | After `RCPT-` exists. Never title Tax Invoice (NP-XX-003). |
| Branding from Pay org profile | NP-ONE-010 | One tenant profile later; do not call Hub `/public/one/{slug}/branding`. |
| Legal pages | | Rewrite as Pay-the-software, merchant-the-seller. Do not copy June 2026 Hub articles blindly. |
| Success → dashboard CTA | | Only when magic-link portal exists; keep token on the URL (B03-C09). |

#### Refuse (do not port to 5179)

| Hub thing | Why refuse | IDs |
|-----------|------------|-----|
| `GET /one/auth/me` + IdentityBanner “Use my Lazuar account” | Makes hop-1 a Hub login. On new Pay it would become Zitadel. | NP-CHK-007, NP-XX-013 |
| Cookie `lazuar_auth` / `credentials: "include"` | Hub cookie realm. Pay 8081 has no cookie auth. | P60, NP-XX-007 |
| `is_guest_checkout` toggle | Hub handler **ignored** it (007-09 honesty bug 6). New page has only guests. | |
| Workspace-admin banner on checkout | Staff testing should use merchant `:5178` or an explicit “preview as buyer” that still does not call whoami as the pay path. | |
| TIN / MyInvois `validateTin` / ID type BRN/NRIC | Homemade LHDN at checkout. | NP-XX-001, NP-XX-002 |
| `QuoteView` B2B “TIN collected at checkout” amber that promises a tax invoice | Tax provider is later. | NP-LAT-001, NP-XX-003 |
| LHDN status column on documents table | VALID badge without a provider. | NP-DOC-004, NP-XX-003 |
| Community portal (`CommunityPortalView`) | Dead island; Hub `One.AuthUser`. | NP-XX-006 adjacent |
| `/accept-invite` → ops `:3003` | Staff invite is **One** copy-link, merchant SPA. | NP-ONE-011, NP-XX-018 |
| Portal cancel / keep / plan-change | Buyer dashboard later; Hub cancel server actions have ignored errors in older evals (008-07 hole 17; live `portal/page.tsx` now redirects `err=action` — still not S1). | |
| WhatsApp number as required phone | WhatsApp dunning is refuse; copy that says “required for delivery” is Hub Communications. | NP-XX-004 |
| shadcn museum (`components/ui/*`) | 60 files, unused by hop-1. Vite stays small. | |
| Next.js App Router + `basePath=/portal` | New origin is Vite 5179, not Caddy `/portal`. | P60 |
| `@repo/api-types-ts` | Hub OpenAPI. Generate `@repo/pay-types-ts` only when 5179 calls `/v1` for real (012/04, P60.3). | |
| `NEXT_PUBLIC_API_URL=http://localhost:8080/api/v1` | Dual-run footgun. Checkout uses `VITE_PAY_API_URL` → **8081**. | |
| Stripe Customer Portal (`GenerateCustomerPortalAsync`) | Hub “Copy Portal Link.” New update-payment is Pay-hosted + wrap-rails, not Stripe Billing customer portal as SoT. | NP-XX-012 |
| PWYW display that does not match charge | 007-09 honesty bug 1. If PWYW ships later, summary **is** the charge. | |
| Zero-amount success omitting id → “Invalid Session” | 007-09 bug 2. | |
| Always-200 unthrottled magic-link | 009-03 B03-C10. When magic-link ships: throttle + constant-time HMAC + no compiled fallback secret. | |
| Header “Buyer Dashboard” that drops token | 009-03 B03-C09; `PortalDashboardLink` tries to keep it, tenant index redirect is the better pattern. | |
| Calling Hub `/public/commerce/*` from 5179 “temporarily” | First line of P60 failure. | |

### 5.4 Hop-1 vs “skip hop-1”

Two honest S1 shapes, both allowed by 011:

1. **Cash register (NP-CHK-005):** buyer opens 5179, sees amount, types email, clicks Pay, hop-2 PSP. This is Hub product checkout without TIN/coupon/IdentityBanner.
2. **Already-addressed pay link:** merchant create included `payer_email`; page is amount + Pay; email is read-only. Closer to Stripe Payment Link / CHIP collect link. Still a Pay-hosted pixel so wrap-rails copy can be honest.

Aura-style **no Pay pixel at all** (integrator redirects straight to Billplz) is NP-SOON-007 M2M, not this SPA. Do not make 5179 a blank redirector that cannot show “invoice each cycle, we will email the next link.”

### 5.5 Arrears / update-payment (later, same origin)

Hub `/{slug}/update-payment/{subId}?token=`:

- ACTIVE + reminder-only → “Invoice each cycle… no card on file” (copy is the wrap-rails sentence — **keep the words**, not the GET).
- ACTIVE + vaulted rail → **RM 1** verification (B03-C05 fights MYR 2 minimum; B03-C01 decline → PAST_DUE).
- PAST_DUE → Gross “Complete Payment.”

When NP-BUY-004 is built on 5179: use Stripe **setup mode** (no capture) or CHIP equivalent for healthy update; never RM 1; never treat setup as paid; never offer “update card” on Billplz. Magic-link token is Pay-issued to the **payer email**, not One.

### 5.6 Quotes (later, same origin or `/q/{id}`)

Hub `/{slug}/pay/{sessionId}` is a real QuoteView (008-07). Ops copies `VITE_PORTAL_URL/{slug}/pay/{id}`. New merchant UI will copy `http://localhost:5179/q/{id}` (or `/c/{id}` if quotes are checkouts). Proforma PDF is NP-SOON-002 — **not a tax invoice**. SST on the quote must match hop-2 (NP-SOON-003). Refuse UBL/TIN-as-legal-feature on that page.

---

## 6. How the page talks to Stripe/CHIP (redirect vs Elements vs hosted PSP page). Wrap-rails implications.

### 6.1 What Hub actually did (BYOK, not MoR)

`PaymentGatewayCapabilities`:

- `SupportsOffSession` = **STRIPE or CHIP** only.
- `IsReminderOnlyGateway` = everything else (Billplz, Razorpay, Xendit, blank).
- `SupportsEmandate` = **false** (no homemade FPX e-mandate; NP-XX-011).
- Wallets (GrabPay, TnG, …) are **hosted on the processor page**, not Lazuar buttons.
- DuitNow QR: Xendit/CHIP/Billplz hosted; “We do not render QR ourselves.”

Adapters mint a **string URL** and the portal assigns `window.location`:

| Rail | Mint | Buyer lands on | Methods UI |
|------|------|----------------|------------|
| **Stripe** | Checkout Session | `checkout.stripe.com` | Card + wallets if the Stripe account has them. Lazuar sets `PaymentMethodTypes = ["card"]` (wallets ride on card). **Not** Elements. **Not** Embedded Checkout. **Not** Payment Links product. |
| **CHIP** | `POST …/purchases/` | `checkout_url` from JSON (`gate.chip-in.asia`) | Brand config: FPX, DuitNow, wallets, cards, BNPL. |
| **Billplz** | `POST /api/v3/bills` | Billplz hosted bill | Collection mix. **Cannot vault. No silent auto-charge.** |

Stripe `CreateCheckoutSessionOptions` (`StripeGatewayAdapter.cs` ~556–623):

- Normal: `Mode = "payment"`, one line item, `UnitAmountDecimal` in minor units, `Quantity`, `SuccessUrl`, `CancelUrl`, metadata on session **and** PaymentIntent.
- `$0` + `setupFutureUsage`: `Mode = "setup"` + `SetupIntentData.Metadata`. Comment: “A $0 PaymentIntent is invalid.”
- `ApplySetupFutureUsage`: `PaymentIntentData.SetupFutureUsage = "off_session"` + `CustomerCreation = "always"`.

CHIP: optional `force_recurring` + `skip_capture` when amount 0. Client `full_name` is `GatewayCommon.ExtractName(email)` — **email local-part**, same class of lie as Billplz (007-09 honesty bug 12). New Pay should send the **payer name from the session** (NP-BUY-001), not re-extract.

S1 dogfood (011/01): **Stripe or one Malaysian rail you will actually dogfood (CHIP or Billplz), not five adapters.** The page talks to **Pay 8081**, not to Stripe.js with the merchant’s publishable key on day one.

### 6.2 Three ways a hosted page can take a card

| Mode | Where methods render | S1? |
|------|----------------------|-----|
| **A. Redirect (Hub)** | PSP origin | **Yes.** Matches BYOK wrap-rails. Pay mints URL server-side with secret keys that never touch 5179. |
| **B. Stripe Elements / Embedded Checkout** | 5179 iframe/fields | Later. Needs `pk_` on the buyer origin, CSP, and a Pay endpoint that creates a Checkout Session or PaymentIntent client_secret. Conversion win (methods on first paint). Not required to pass step 10. |
| **C. Pay-rendered FPX bank grid** | 5179 | **Refuse.** We are not an acquiring bank. CHIP/Billplz already host FPX. |

Recommendation: **A for S1.** Document B as a later conversion ticket, not as “production-ready checkout.” 007-09 CK-001 (methods on first pixel) stays red until B or until merchant create skips hop-1 (email already on session **and** auto-redirect — still hop-2 methods, just one less form).

5179 must **not** ship `STRIPE_SECRET_KEY`, CHIP brand secret, or Billplz API key. Those are Pay-host BYOK (NP-GW-001), paper 06.

### 6.3 Wrap-rails copy on the cash register (NP-GW-007)

The page is where the lie happens if we stay silent.

| Session rail | Pay button | Helper sentence | After paid |
|--------------|------------|-----------------|------------|
| Stripe or CHIP, one-off | “Pay {amount}” | “You will complete payment on Stripe/CHIP.” | One capture. No “we will auto-charge next month.” |
| Stripe or CHIP, recurring, **will vault** | “Pay {amount} and save this method” | “If this payment succeeds, later invoices can be charged to this card.” | Only if hop-2 actually used `setup_future_usage` / CHIP `force_recurring` **and** webhook stored a PM. |
| Stripe or CHIP, **setup only** (trial / $0 / update-card) | “Save payment method” **not** “Pay” | “We are not charging today.” | **Not paid.** NP-GW-008. |
| Billplz / reminder-only | “Pay {amount}” | “You will pay on Billplz. We **cannot** auto-debit this method later; we will email a new link.” | Subscription is reminder-only. Ops/dunning later must not AUTO_CHARGE. |
| Unknown / keys missing | disabled | “This merchant has not connected a payment method.” | No redirect |

Hub ops already warns “Billplz cannot vault / no silent auto-charge” on the **merchant** vault page (008-07 §12). The **buyer** page did not always say it; “Total Due Today” on `mo`/`yr` + Billplz was honesty bug 9 in 007-09. Do not repeat.

`is_reminder_only` on Hub arrears GET is **gateway-derived**, not the row flag (B03-C06): a Stripe reminder-only sub is sold as “update card,” and success **clears** the flag. New buyer DTO must use the **session/subscription row**, not `PaymentGatewayCapabilities.IsReminderOnlyGateway(name)` alone. A Stripe session can still be reminder-only (`ProcessZeroAmount`).

### 6.4 Never treat setup-intent as paid (NP-GW-008) — frontend contract

Backend (paper 06) must not journal setup as cash. Frontend must not:

1. Show “Order Complete!” because Stripe redirected to `success_url` after `mode=setup`.
2. Poll a status named `ACTIVE` / `PENDING` / `SETUP_COMPLETE` as paid (Hub comment already forbids ACTIVE/PENDING).
3. Unlock a download or “Go to dashboard” that implies access (NP-FUL-002: buyer access is Pay’s subscription row, written in the webhook handler).
4. Display `amount` 0 as “Paid RM 0.00” in a way that looks like a settled charge if the intent was vaulting.

Visible states for setup-only: **“Card saved”** vs **“Payment received”**. Two different strings. If S1 has no trials, do not mint `mode=setup` at all; then the page never has this fork. That is an allowed S1 simplification. It is **not** allowed to mint setup and then reuse the paid pixel.

Hub `TryMapSetupIntentSucceeded` returning `PAYMENT_COMPLETED` is exactly the event name that would fool a naive poller. Public status for buyers should be Pay’s enum (`open`/`paid`/`expired`), not Stripe’s.

### 6.5 Who mints hop-2: Pay host, not Vite

```text
5179  POST /v1/pay/{token}/start { name, email }
8081  load session (must be open)
      refuse if expired/paid
      persist payer identity (NP-BUY-001)
      load BYOK keys for org
      call Stripe/CHIP/Billplz with success/cancel URLs
      persist psp_ref (Checkout Session id / purchase id / bill id)
      return { redirect_url }
5179  location.assign(redirect_url)
```

Do not create the Stripe session at **merchant POST /v1/checkouts** time unless you want 24h Stripe expiry fighting Pay TTL (Stripe Checkout sessions expire; Hub open sessions were 24h). For shareable links that sit in WhatsApp for days, mint hop-2 **at click**. For “pay now” emails, minting early is OK if TTL matches.

Double-click: Hub PAST_DUE update-payment **did not cache** the URL (B03-C02) → two Checkouts → two captures. `start` must be idempotent per open session (return the same `redirect_url` if still valid).

### 6.6 What 5179 never loads

- `stripe` npm package / `@stripe/stripe-js` in S1
- CHIP collect.js
- Billplz JS
- Merchant `pk_live_` in `import.meta.env` (publishable keys are for mode B later, still not secret keys)

The only outbound origins from the buyer browser besides Pay 8081 should be: the PSP (after redirect), maybe a merchant `success_url`, and static assets.

---

## 7. Payer identity inside Pay (email, magic link later) — not One

### 7.1 Two planes (011/02)

| Plane | System | Who |
|-------|--------|-----|
| Merchant staff | One humans + membership | Ada, invited MEMBER/VIEWER |
| Buyer / payer | **Pay checkout profile** | Person who pays on the hosted page |

> Cardholders never become Zitadel users because they bought an ebook. (011/02)

011/01 buyer plane:

- Payer email/name on the checkout session.
- Magic link / receipts for **that** mailbox.
- Small payer profile inside Pay (old CRM/client-profile job, **stripped**).
- Do not grant buyer access in One (NP-FUL-002).

Fixture session has **no** payer fields. `CreateCheckoutRequest` has `org_id`, `amount`, `currency`, `success_url`, `cancel_url`, `idempotency_key`. NP-BUY-001 remains `todo` until the row (and the public DTO, and the 5179 form) exist.

### 7.2 What 5179 collects in S1

Required:

- **Email** (the mailbox that will receive the receipt and, later, magic links).
- **Name** (passed to CHIP/Stripe as the customer name, not `ExtractName(email)`).

Optional later: phone, address. Not TIN. Not “create account / password.” Not “sign in with Google.”

If merchant create already supplied payer email (invoice link), the field is read-only. Buyer can still be asked to confirm.

Persist on the **Pay session** at `start` (or earlier PATCH). This is NP-BUY-001. It is not `POST /tenants/{id}/members/invite`.

### 7.3 Small payer profile (NP-BUY-002) — later, still Pay

Hub CRM `ClientProfile` is the cathedral version: merged by email for documents (B03-C25, wider than arrears sibling rule), newest-sub as magic-link subject ignoring status (B03-C23). New Pay: a thin `(org_id, email)` profile that checkouts hang off. No Zitadel `sub`. No Hub `lazuar_auth` user id.

Do not reuse One `user_id` if Ada pays her own product as a test; that test must still be a **guest** checkout (or merchant preview) so NP-CHK-007 stays true. Hub IdentityBanner made “pay as myself” look like a Lazuar account. Refuse.

### 7.4 Magic link later (NP-BUY-003) — **this origin**, not `:5178`

011/01: “Buyer portal: magic link to the **payer** mailbox, update-payment, download **receipt**.” README of the Vite app already reserves this:

> Receipts / update-payment can share this origin later (magic link to the payer mailbox), not the merchant shell.

So:

| Later URL (illustrative) | Job | Auth |
|--------------------------|-----|------|
| `https://pay.example/c/{token}` | Cash register | capability URL |
| `https://pay.example/r/{magic}` | Receipt list / download `RCPT-` | mailbox token |
| `https://pay.example/u/{magic}` | Update payment / arrears | mailbox token |

Do **not** put those on `lazuar-pay-merchant` (`:5178`). Ada’s OIDC session is the wrong proof of “I am the cardholder.” Do not put them on `lazuar-portal` `:3004`. Do not send magic links to One login `:5175`.

Hub lessons when that ships (009-03):

- Always-200 on request is correct anti-enumeration; **throttle** (B03-C10).
- No compiled-in `fallback_dev_secret_key` (B03-C11). Fail closed without a Pay-owned secret.
- Constant-time compare.
- Base64url + encode in every href (B03-C17).
- Success/return URLs **keep** the token (B03-C09). Hub arrears success went to `/{slug}/portal` with no token.
- Do not use Hub cookie as an alternate door that hides the magic-link form but then calls APIs with `token=""` (008-07 §16.8–16.10). 5179 has no cookie door.

### 7.5 Fail if Zitadel appears (testable)

Step 10 fail lock is operational, not literary:

1. Open the pay link in a fresh browser profile.
2. No redirect to `localhost:5175`, `localhost:8085`, `zitadel`, `login.lazuar`.
3. No `GET /v1/whoami`.
4. No `GET http://localhost:8080/api/v1/me`.
5. Network tab: 8081 public pay resource + later PSP. That is all.
6. Pay with a test card / FPX sandbox **without** creating an One user.

An e2e that logs in as Ada to **create** the link is fine (merchant plane). The **buyer** Playwright context must not reuse that storage state.

---

## 8. Production: `:5179` CORS already allowed; need public checkout read API; no OIDC on this origin

### 8.1 Local topology (2026-08-21)

| Process | Port | Role for this paper |
|---------|------|---------------------|
| One API | 8080 | Identity. Checkout does not call it. |
| One login | 5175 | Merchant sign-in. Checkout does not link it. |
| One app | 5174 | One customer SPA. Not Pay. |
| Focused Pay | **8081** | Money door `/v1`. |
| Pay merchant Vite | **5178** | Staff. OIDC later. |
| Pay checkout Vite | **5179** | Buyer. **This origin.** |
| Hub API | 8080 (collision) | **Off** when One occupies 8080. |
| Hub portal | 3004 | Museum cash register. Dual-run until cutover. |
| Hub ops | 3003 | Museum. CORS on 8081 **denies** this origin. |
| Preview checkout | 4179 | `vite preview`, not prod. |

012/05 wrote “future Pay hosted checkout TBD, likely a Pay-owned origin — Yes, later” for CORS on 8081. **Later is now** for the allowlist; **not** for the public GET.

One CORS still does **not** need 5179. Adding `http://localhost:5179` to One `App:CorsOrigins` or login `REDIRECT_ALLOWLIST` is how a well-meaning P10 PR fails NP-CHK-007. Staging One issue 011 (production CORS falls back to localhost) is One’s; do not “fix” it by registering the buyer origin.

### 8.2 What production 5179 still needs (host + SPA)

| Need | Today | Production |
|------|-------|------------|
| Public read API | Member GET only | Buyer resource §3.5 |
| Status ≠ always open | Fixture | paid/expired writes from webhook + TTL job |
| Payer fields | none | NP-BUY-001 |
| Hop-2 start | none | Pay-side adapters (paper 06) + 5179 redirect |
| Router | single `App.tsx` | `/c/:id`, `/c/:id/return`, later `/r`, `/u` |
| `VITE_PAY_API_URL` | default 8081 | Production Pay origin, **not** `/api/v1` Hub prefix |
| CORS | 5179 localhost twins | Production checkout origin(s), 127 twin not relevant; **no** Hub portal origin |
| Cookies | none | keep none for S1 |
| OIDC | none | **keep none** |
| Types | none | `@repo/pay-types-ts` when the GET exists (012/04: not before) |
| HTTPS | local http | Real PSP redirect_urls require public https (Stripe/CHIP). Tunnel is ops, not this SPA. |
| Content-Security-Policy | none | After Elements (mode B). Redirect mode is simpler. |
| Tests | CorsTests on `/health` | CORS on public GET; 401/404 matrix; Playwright “no Zitadel”; paid pixel not on setup |

### 8.3 No OIDC on this origin (explicit)

Merchant SPA (paper 04):

- Register One app `POST /tenants/{id}/apps`
- Redirects: `http://localhost:5178/callback` (and prod merchant origin)
- PKCE, `access_token` as Bearer to 8081

Checkout SPA:

- **No** One app
- **No** callback route
- **No** `oidc-client-ts`
- **No** silent renew iframe
- **No** `client_id` in `import.meta.env` “just in case”

P10.3 already says “Password form in Pay” and “`id_token` as Bearer” must not. Add a row in whoever implements P10: **checkout origin is not an OIDC redirect.** If One’s seed script today copies `lazuar-app` and someone adds both Vite ports to the allowlist, delete 5179 from that list.

### 8.4 Env honesty

Checkout has no `.env.example`. When one is added:

```
VITE_PAY_API_URL=http://localhost:8081
```

Forbidden:

```
VITE_API_URL=http://localhost:8080/api/v1     # Hub
VITE_ONE_AUTHORITY=http://localhost:8085      # Zitadel
VITE_OIDC_CLIENT_ID=…                         # merchant only
VITE_PORTAL_URL=http://localhost:3004         # museum
```

Merchant Vite may have OIDC env. Do not share a `.env` at repo root that both apps load.

### 8.5 Deploy shape (not this paper’s CD)

Paper 10 (CI/observability) will decide how 5179 is hosted (object storage + CDN vs Node). Requirements from **this** slice:

- Static SPA is enough for redirect-mode checkout (no SSR required). Hub chose Next SSR for hop-1 product GET; new Pay can SSR later for unfurl/Open Graph (007-09: hop-1 URLs currently lose unfurl). S1 WhatsApp paste can show a generic title “Lazuar Pay — checkout” (`index.html`) until product pages exist.
- `basePath` is **not** `/portal`. Do not reuse Hub Caddy path map (`README.md` `/portal*` → `:3004`).
- `strictPort` is a **dev** constraint. Production is a hostname, not a port.

### 8.6 Cutover vs dual-run (portal stays until kill criteria)

P60: keep `lazuar-portal` `VITE`/`NEXT_PUBLIC_API_URL` on Hub 8080. Dual-run means:

- Old buyers still open `:3004/{slug}/checkout/{product}`.
- New dogfood buyers open `:5179/c/{id}`.
- Both may exist on one laptop; they must not share cookies (different ports, good) and must not share API hosts.

Kill criterion for 3004 is paper 02 (replace/cutover), not “5179 health probe is green.”

---

## 9. Anti-goals (Zitadel on checkout, Hub cookie, calling Hub `/public/commerce`)

A PR that does any of the following **fails this slice** even if the page looks like Stripe.

### 9.1 Identity

| Anti-goal | Why |
|-----------|-----|
| Redirect `:5179` → `:5175` / Zitadel `/ui/login` | NP-CHK-007, NP-XX-013 |
| Mount OIDC on checkout Vite | P10 trap |
| `GET /v1/whoami` from checkout | Merchant endpoint |
| `GET` One `/api/v1/me` from checkout | Buyer is not an One human; also CORS |
| Password form, magic-link **to One**, “continue with Google” | NP-XX-007 |
| Create Zitadel human on successful pay | NP-XX-013 |
| IdentityBanner “Use my Lazuar account” | Hub cookie plane |
| Forward `lazuar_auth` | `server-client.ts` pattern |
| `credentials: "include"` to 8081 | Pay CORS has no credentials; do not add them to make Hub cookies work |

### 9.2 Cathedral retarget

| Anti-goal | Why |
|-----------|-----|
| Set portal `NEXT_PUBLIC_API_URL` to `http://localhost:8081` | P60; Hub paths 404; then “just add login” |
| Add `:3004` to Pay CORS | Invites that retarget |
| Import `@repo/api-types-ts` into `lazuar-pay-checkout` | Hub contract |
| Copy `apps/lazuar-portal` into `apps/lazuar-pay-checkout` | Next, shadcn, TIN, cookie, community |
| Implement Hub `/public/commerce/checkout` on 8081 | pay-spec README forbids; 012/04 |
| Grow pay-spec with `/one/auth/*` | 012/04 |
| Call Hub `/public/one/{slug}/branding` | Fake One |

### 9.3 Money lies on the pixel

| Anti-goal | Why |
|-----------|-----|
| Green “Paid” on PSP success redirect alone | NP-CHK-002 |
| Green “Paid” on Stripe `mode=setup` / CHIP `skip_capture` | NP-GW-008 |
| “We will charge your card automatically” on Billplz | NP-GW-007 |
| “Update card” on reminder-only | B03-C06 |
| RM 1 verification as the healthy-update path | B03-C05, MYR min 2 |
| Title receipt Tax Invoice / print VALID | NP-XX-003 |
| UUID as document number | NP-DOC-002 |
| Unlock files/access in the SPA from status poll | NP-FUL-001/002: same handler as journal |
| Buyer-chosen amount on an unconstrained public POST | Merchant create is the door |
| Silent second hop-2 session on double-click | B03-C02 |

### 9.4 Scope creep (not v1 on this origin)

| Anti-goal | Why |
|-----------|-----|
| WhatsApp dunning button | NP-XX-004 |
| TIN at checkout as legal e-invoice | NP-XX-002 |
| Quote/UBL/LHDN QR | NP-XX-001, NP-LAT-001 |
| Plan change / cancel | later buyer portal |
| Stripe Billing Customer Portal as SoT | NP-XX-012 |
| Elements/FPX grid in the same PR as the first redirect | conversion later |
| Merchant ops chrome (sidebar, whoami) on 5179 | wrong plane |

### 9.5 P50/P60 reminders

P50.1 still open: “Buyer pays **without** a One account.” Fixture POST/GET being `done` does not close it.

P60.2: ops `POST /one/auth/login` is Hub homemade IdP — Pay must not implement it. That sentence is usually aimed at `:5178`. It also forbids a “temporary” login on `:5179` if hop-1 feels empty.

---

## 10. Open questions (public token on pay link vs unguessable id)

These are product/contract questions for the next implementation program. This paper does not pick silently except where a lock already did.

### Q1. Capability URL (`/c/{unguessable-id}`) vs separate `public_token` vs HMAC query?

**Facts:**

- Today’s id is 128-bit hex (`N` format). That is capability-URL grade **if** we never switch to sequential ints / UUIDv7 time-leak as the **only** secret.
- Merchant `GET /v1/checkouts/{id}` is member-gated and should stay that way (§3.5).
- Hub arrears used HMAC query `?token=` after a GUID-only P0 (008 P0-2 closed by 009-03). Residual: encoding, fallback secret, success URL drop.
- Shareable links will appear in WhatsApp, emails, access logs, Referer headers to PSPs.

**Options:**

| | Link | Public GET | Merchant GET | Notes |
|--|------|------------|--------------|-------|
| **Q1-A** | `https://pay.example/c/{id}` | `GET /v1/pay/{id}` (id is secret) | `GET /v1/checkouts/{id}` Bearer | One identifier. Referer leak to Stripe sends the capability. |
| **Q1-B** | `https://pay.example/c/{public_token}` | `GET /v1/pay/{public_token}` | `GET /v1/checkouts/{id}` | Two identifiers. Merchant support uses `id`; buyer never sees `org_id`. Prefer **buyer-safe DTO**. |
| **Q1-C** | `https://pay.example/c/{id}?t={hmac}` | same + HMAC gate | member GET without HMAC | Hub shape. Easy to drop `t=` (B03-C09). |

**Lean (non-binding):** Q1-B. Keep member GET. Mint a high-entropy `public_token` stored on the session, put **only** that in the WhatsApp URL. Rotate on expire. Do not reuse Hub HMAC-SHA256-over-guid with `Jwt:Secret`.

### Q2. Does S1 mint hop-2 at create time or at Pay click?

Create-time: link can 302 straight to Stripe (CHIP collect / Stripe Payment Links competitor shape). Click-time: 5179 can show wrap-rails copy and collect email. **Lean:** click-time for S1 so NP-BUY-001 and NP-GW-007 have a pixel. Auto-redirect if email already present **after** showing amount for one paint is a later polish.

### Q3. Success URL default: merchant site or `:5179/return`?

NP-CHK-002 allows merchant URLs. Verifying/timeout pixels live on 5179. **Lean:** Pay always has a return route; merchant `success_url` is a **button** after paid, not the PSP redirect target, until we trust merchants not to treat landing as paid. Alternatively PSP redirects to 5179/return which then 302s to merchant **after** status=paid. Open because it affects Stripe Dashboard UX.

### Q4. Status strings: `open/paid/expired` vs Hub `OPEN/COMPLETED/EXPIRED` vs Stripe-ish?

pay-spec today: `status: string` with fixture `"open"`. **Lean:** lowercase `open | paid | expired` in pay-spec, never `COMPLETED` (confused with setup complete). Public poller allows only `paid`.

### Q5. SST line on S1 cash register?

NP-MON-003 is **V1**, not S1. Fixture amount is a single decimal. **Lean:** S1 shows the amount the session holds; no tax line until Pay computes Gross server-side. When it does, steal `grossBreakdown.ts` judgment (unit then × seats, fail closed). Do not port TIN.

### Q6. EN/BM in S1?

008-07: Hub BM is hop-1 chrome/form only. **Lean:** S1 English; locale switch later. Do not block step 10 on i18n.

### Q7. Router: `react-router` vs file-based?

Scaffold has none. **Lean:** `react-router` (or Vite file router) inside this package. Do not adopt Next to “be like portal.”

### Q8. Should `:5179` be on One CORS “for branding”?

**No.** Branding is Pay-owned display fields. One CORS for 5179 is the Zitadel footgun.

### Q9. Preview as Ada (merchant wants to click her own link)?

Allowed in a fresh window **as guest**. Forbidden: checkout detecting Ada’s OIDC session and skipping email. Hub did that with cookies and then mixed tokenless portal calls.

### Q10. Public `POST start` CSRF / origin check?

CORS already restricts browser origins. Non-browser POST to start could mint PSP sessions if the token leaks. Rate-limit per token. Bind return URLs server-side (ignore body success_url from the buyer).

### Q11. Existence oracle on member GET (401 vs 404)

Today: missing → 404 (no One call); existing + no Bearer → 401. Fine for merchant API. Public API: **always 404** on bad token. Do not “fix” member GET to 404-without-Bearer in a way that breaks CheckoutTests without updating them on purpose.

### Q12. Who generates `@repo/pay-types-ts`?

012/04: not now. When 5179 calls public GET for real, generate from **pay-spec**, not Hub. Checkout must not wait on npm publish (NP-XX-021).

---

## Appendix A — Fixture vs production session (field map)

| Fixture (`6f866ff0`) | Production S1 (this paper) | Wave |
|----------------------|----------------------------|------|
| `id` | merchant id (unguessable or internal) | S1 |
| — | `public_token` for WhatsApp URL | S1 (if Q1-B) |
| `org_id` | same; **omit from buyer DTO** | S1 |
| `amount` | charge amount (Gross when SST exists) | S1 / V1 SST |
| `currency` | MYR default | S1 |
| `status: open` | `open \| paid \| expired` | S1 NP-CHK-004 |
| `success_url` | merchant optional; Pay return owned | S1 NP-CHK-002 |
| `cancel_url` | same | S1 |
| `created_at` | + `expires_at` | S1 |
| — | `payer_email`, `payer_name` | S1 NP-BUY-001 |
| — | `pay_url` (5179 link) | S1 NP-CHK-006 |
| — | `redirect_url` (PSP, at start) | S1 |
| — | `rail` / `is_reminder_only` / `setup_only` | S1 NP-GW-007/008 |
| — | product id / name / seats | after NP-CAT |
| — | `rcpt_number` on paid | S1 NP-DOC, shown later on `/r` |

---

## Appendix B — Hub portal complete file inventory (absolute)

Base: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/`

### Package / config

`package.json`, `pnpm-lock.yaml`, `next.config.ts`, `tsconfig.json`, `eslint.config.mjs`, `postcss.config.mjs`, `components.json`, `Dockerfile`, `README.md`, `.gitignore`

### `src/app` (20 files)

`page.tsx`, `layout.tsx`, `not-found.tsx`, `globals.css`, `accept-invite/page.tsx`, `legal/layout.tsx`, `legal/terms/page.tsx`, `legal/privacy/page.tsx`, `legal/refund/page.tsx`, `[tenantSlug]/layout.tsx`, `[tenantSlug]/page.tsx`, `[tenantSlug]/checkout/[productSlug]/layout.tsx`, `[tenantSlug]/checkout/[productSlug]/page.tsx`, `[tenantSlug]/checkout/[productSlug]/success/page.tsx`, `[tenantSlug]/checkout/custom/success/page.tsx`, `[tenantSlug]/pay/[sessionId]/page.tsx`, `[tenantSlug]/portal/layout.tsx`, `[tenantSlug]/portal/page.tsx`, `[tenantSlug]/update-payment/[subId]/page.tsx`

### `src/modules` (mounted)

checkout components (8), checkout i18n (8 including tests), checkout lib (3 including test), checkout `types.ts`, portal components (3), core lib (2)

### `src/modules` (unmounted)

`community/components/CommunityPortalView.tsx`, `community/lib/api.ts`

### `components/ui` (60 shadcn files, unmounted by hop-1)

`accordion.tsx`, `alert-dialog.tsx`, `alert.tsx`, `aspect-ratio.tsx`, `avatar.tsx`, `badge.tsx`, `breadcrumb.tsx`, `button-group.tsx`, `button.tsx`, `calendar.tsx`, `card.tsx`, `carousel.tsx`, `chart.tsx`, `checkbox.tsx`, `collapsible.tsx`, `combobox.tsx`, `command.tsx`, `context-menu.tsx`, `dialog.tsx`, `direction.tsx`, `drawer.tsx`, `dropdown-menu.tsx`, `empty.tsx`, `field.tsx`, `hover-card.tsx`, `input-group.tsx`, `input-otp.tsx`, `input.tsx`, `item.tsx`, `kbd.tsx`, `label.tsx`, `menubar.tsx`, `native-select.tsx`, `navigation-menu.tsx`, `pagination.tsx`, `popover.tsx`, `progress.tsx`, `radio-group.tsx`, `resizable.tsx`, `scroll-area.tsx`, `select.tsx`, `separator.tsx`, `sheet.tsx`, `sidebar.tsx`, `skeleton.tsx`, `slider.tsx`, `sonner.tsx`, `spinner.tsx`, `switch.tsx`, `table.tsx`, `tabs.tsx`, `textarea.tsx`, `toggle-group.tsx`, `toggle.tsx`, `tooltip.tsx`

### Other

`hooks/use-mobile.ts`, `lib/utils.ts`, `public/*` favicons

**Steal from this list:** `grossBreakdown.ts` judgment, success-poller state machine, mobile layout order, wrap-rails sentences on the update-payment reminder-only branch, “always-200” **shape** of magic-link (not the implementation).

**Refuse the rest as a folder copy.**

---

## Appendix C — New checkout Vite inventory (complete)

Base: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-checkout/`

| File | Role |
|------|------|
| `package.json` | 5179 strictPort |
| `README.md` | Fail if Zitadel |
| `vite.config.ts` | 5179 / preview 4179 |
| `index.html` | Title “Lazuar Pay — checkout” |
| `tsconfig.json` | project references |
| `tsconfig.app.json` | `src` |
| `tsconfig.node.json` | `vite.config.ts` |
| `src/main.tsx` | React 19 createRoot |
| `src/App.tsx` | Health probe UI |
| `src/App.css`, `src/index.css` | Minimal system-ui |
| `public/favicon.svg` | Vite default mark |

No router, no env example, no tests, no `src/pages`, no Stripe.

---

## Appendix D — Tests that exist vs tests this origin still needs

### Exist (Pay host)

| Test | Pin |
|------|-----|
| `CheckoutTests.Create_without_bearer_is_401` | Merchant create is not public |
| `Create_and_get_open_session` | Fixture JSON; GET needs Bearer |
| `Get_unknown_is_404` | Missing + Bearer, no One call |
| `Create_for_other_org_is_403` / `Get_other_org_session_is_403` | Member gate |
| `Create_idempotent_on_key` | NP-CHK-003 |
| `Create_defaults_currency_to_myr` | NP-CAT-003 |
| `Create_rejects_non_positive_amount` | |
| `Health_still_skips_one` | Probes |
| `CorsTests.Health_allows_checkout_origin` | 5179 ACAO on `/health` |
| `Health_does_not_allow_ops_origin` | 3003 denied |
| `WhoamiTests.*` | Merchant only |

### Missing (must appear before NP-CHK-005 `done`)

1. Public GET without Bearer returns 200 for an open session **or** 404; never 401 “log in.”
2. Public GET does not return `org_id` / staff URLs if Q1-B.
3. Public GET CORS ACAO 5179, including OPTIONS.
4. `start` without Bearer mints redirect_url; second start is idempotent.
5. `start` on paid/expired is 409/410, not a new Stripe session.
6. Status poll: only `paid` flips the success pixel; setup-only is a different pixel.
7. Playwright: buyer context never requests `:5175` or `/v1/whoami`.
8. Isolation: `lazuar-pay-checkout/package.json` does not depend on `@repo/api-types-ts`.

---

## Appendix E — NP rows this origin is allowed to flip (later, not this analysis)

When implementation exists, flip in **011/11**, not here.

| ID | Can 5179 + public API close it? | Cannot close alone |
|----|--------------------------------|--------------------|
| NP-CHK-004 | Page + host state machine | Webhook writer (06/07) |
| NP-CHK-005 | **Yes** (this SPA) | |
| NP-CHK-006 | URL on 5179 + `pay_url` field | Merchant UI copy button (04) |
| NP-CHK-007 | **Yes** (fail e2e) | |
| NP-BUY-001 | Form + session fields | |
| NP-BUY-002…005 | later on this origin | mail (NP-MAIL) |
| NP-GW-007 | Copy on the page | Adapter matrix (06) |
| NP-GW-008 | Success pixel | Adapter + handler (06/07) |
| NP-CHK-002 | Return view honesty | already stored |
| NP-API-003 | **No** — current `done` is merchant GET. Need a **new** row or a note that buyer GET is separate. | Do not silently reuse NP-API-003 |

P50.1 checkbox “Buyer pays without a One account” is the same as NP-CHK-007.

---

## Appendix F — Sources index (absolute)

```
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-checkout/**
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/App.tsx
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/README.md
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Program.cs
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Checkouts/*.cs
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/One/MemberGate.cs
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/One/Bearer.cs
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/One/WhoamiEndpoints.cs
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/CheckoutTests.cs
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/CorsTests.cs
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/pay-spec/main.tsp
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/011-new-lazuar-pay/{01-product,02-one-integration,03-first-slice,11-checklist,12-first-slice-tracker}.md
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/012-one-to-pay/{04-pay-spec-contract,05-local-topology,10-dogfood-and-tests}.md
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/012-one-to-pay/checklists/{p10-spa-oidc,p50-money,p60-old-frontends}.md
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/008-evals/07-ops-portal-admin-frontend.md
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/009-bugs/{01-commerce-checkout-activation,03-commerce-dunning-arrears-portal}.md
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/007-feats/09-checkout-and-payment-links.md
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/**  (inventory §5 / Appendix B)
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/{Stripe,ChipCollect,Billplz}GatewayAdapter.cs
/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Contracts/PaymentGatewayCapabilities.cs
```

One SHA `0f79fe4` was recorded; One HTTP was not re-audited here (012/01 already mapped `/me`). Checkout must not grow a dependency on that façade.

---

## Stop lines

Do not claim `lazuar-pay-checkout` is a cash register. It is a health probe on `:5179`.  
Do not claim `GET /v1/checkouts/{id}` is the buyer door. It is member-gated.  
Do not claim NP-CHK-005/006/007 or NP-BUY-001 are `done`.  
Do not retarget `lazuar-portal` (`:3004`) at 8081.  
Do not register `:5179` as an One OIDC redirect.  
Do not treat Stripe `mode=setup` or a success query string as paid.  
Do not say “we will auto-charge” on a reminder-only rail.  
Steal SST **math** and success-poller **discipline** from Hub; steal nothing that logs the buyer into Zitadel or Hub.

---

*End of 05 — Production hosted pay page (`lazuar-pay-checkout` `:5179`), not `lazuar-portal`. Analysis only. 21 August 2026. Pay `6f866ff0`. One `0f79fe4`.*
