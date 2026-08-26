# 08 — pay-spec vs live doors vs SPA clients

**Date:** 2026-08-26  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Branch:** `feat/018-merchant-shell`  
**HEAD:** `9f04ad58` — `fix(pay-ui): match receipts table to pay-link chrome`  
**Slice:** TypeSpec (`packages/pay-spec/main.tsp`) vs generated OpenAPI (`packages/pay-spec/dist/openapi.yaml`) vs live Minimal API maps on `:8081` vs what the two Vite apps actually send and parse.  
**Type:** Uncondensed contract honesty paper. **Not** an implementation. Does **not** edit TypeSpec, OpenAPI, C#, TypeScript, tests, or specs.

**Authority rule used here:** when TypeSpec / OpenAPI / host / SPA disagree, **the live host is the product**. Both sides of a disagreement are findings (spec lie **or** host lie). The paper names which side to change later; it does not change either.

**Counts at this SHA (path-level, methods distinct):**

| Surface | Operations |
|---------|------------|
| Live `Map*` on `apps/lazuar-pay` | **22** (19 under `/v1`, plus unversioned `GET /health` and `GET /ready`) |
| `packages/pay-spec/main.tsp` | **13** |
| On-disk `packages/pay-spec/dist/openapi.yaml` | **11** |
| Merchant SPA Pay fetches | **8** Pay paths (plus One `POST /tenants`, out of pay-spec by design) |
| Checkout SPA Pay fetches | **2** (`GET` public pay + `POST` start) |

A fresh `task pay:spec` would emit the 13 TypeSpec ops, not the 11 on disk, and still not the 22 live doors. The generated file is stale **and** the source TypeSpec is a subset of the host.

Hub `packages/api-spec`, Hub honesty allowlist, rail crypto internals, and One TypeSpec are out of scope except where Pay **projects** One (`whoami`) or **calls** One (`POST /tenants` from merchant).

---

## Coordinates

| | |
|--|--|
| Focused host | `apps/lazuar-pay/src/Lazuar.Pay` listen **8081** (`Properties/launchSettings.json` `applicationUrl`) |
| TypeSpec | `packages/pay-spec/main.tsp` namespace `LazuarPay`, `@server("http://localhost:8081")`, prefix **`/v1`** (not `/api/v1`) |
| OpenAPI emit | `tspconfig.yaml` → `{project-root}/dist/openapi.yaml` via `@typespec/openapi3` |
| Compile task | `Taskfile.yml` `pay:spec` (`pnpm exec tsp compile .` in `packages/pay-spec`) |
| CI compile | `.github/workflows/ci.yml` job `pay` step `Compile pay-spec` (`pnpm --filter @repo/pay-spec exec tsp compile .`) |
| Hub honesty | `scripts/check-openapi-minimal-honesty.mjs` scrapes **`apps/lazuar-api`** and `packages/api-spec`. **Does not** see Pay. |
| Merchant | `apps/lazuar-pay-merchant` `:5178`, `VITE_PAY_API_URL` default `http://localhost:8081` |
| Checkout | `apps/lazuar-pay-checkout` `:5179`, same Pay origin |
| Wire JSON | `Program.cs` `JsonNamingPolicy.SnakeCaseLower` + case-insensitive; `OneClient.Json` same |
| Isolation | `IsolationTests.cs` bans Hub modules / `@repo/api-types-ts`. Does **not** assert pay-spec ↔ Map* honesty. |

`packages/pay-spec/README.md` says OpenAPI lands in `dist/openapi.yaml` **(gitignored)**. Root `.gitignore` has a `dist/` rule. The file is present in this workspace as a compile leftover. CI compiles TypeSpec as a **syntax** gate and does not dirty-check or honesty-diff the yaml against `MapGet`/`MapPost`/`MapPut`.

---

## Files opened

**pay-spec**

- `packages/pay-spec/main.tsp`
- `packages/pay-spec/dist/openapi.yaml`
- `packages/pay-spec/tspconfig.yaml`
- `packages/pay-spec/package.json`
- `packages/pay-spec/README.md`

**Host composition + every `Map*`**

- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Hosting/HealthEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Hosting/PayErrors.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiResponse.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/OrgReadyEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/Bearer.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneClient.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneMeMapper.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneMeResponse.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneAuthz.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookSignature.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CreateCheckoutRequest.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutSession.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutStore.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/CreatePaymentLinkRequest.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkOccupancy.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Catalog/CatalogEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/BuyerEmail.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/CheckoutUrls.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Money/Queries/PaymentQueryEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestHosted.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/appsettings.json`
- `apps/lazuar-pay/src/Lazuar.Pay/appsettings.Development.json`
- `apps/lazuar-pay/src/Lazuar.Pay/Properties/launchSettings.json`
- `apps/lazuar-pay/.env.example`
- `apps/lazuar-pay/README.md`

**Tests / isolation / CI / tasks**

- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Hosting/HealthTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/WhoamiTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OrgReadyTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OneWebhookTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Checkouts/CheckoutTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Catalog/CatalogTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Credentials/GatewayTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPay/PublicPayTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Webhooks/WebhookTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Money/PaymentQueryTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayTest.cs`
- `Taskfile.yml` (`pay:spec`, `pay:test`, `pay:dev`)
- `.github/workflows/ci.yml` (jobs `contracts`, `pay`)
- `scripts/check-openapi-minimal-honesty.mjs` (Hub-only; first 80 lines)
- `.gitignore`
- `turbo.json`
- `package.json` (workspace root)

**SPA clients**

- `apps/lazuar-pay-merchant/src/lib/payApi.ts`
- `apps/lazuar-pay-merchant/src/lib/oneApi.ts`
- `apps/lazuar-pay-merchant/src/lib/processors.ts`
- `apps/lazuar-pay-merchant/src/lib/http.ts`
- `apps/lazuar-pay-merchant/src/lib/roles.ts`
- `apps/lazuar-pay-merchant/src/lib/staffDisplay.ts`
- `apps/lazuar-pay-merchant/src/layout/OrgLayout.tsx`
- `apps/lazuar-pay-merchant/src/layout/DashboardChrome.tsx`
- `apps/lazuar-pay-merchant/src/layout/nav.ts`
- `apps/lazuar-pay-merchant/src/pages/HomePage.tsx`
- `apps/lazuar-pay-merchant/src/pages/CreateWorkspaceForm.tsx`
- `apps/lazuar-pay-merchant/src/pages/org/OverviewPage.tsx`
- `apps/lazuar-pay-merchant/src/pages/org/GatewayPage.tsx`
- `apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx`
- `apps/lazuar-pay-merchant/src/pages/org/PaymentsPage.tsx`
- `apps/lazuar-pay-merchant/src/pages/org/ReceiptsPage.tsx`
- `apps/lazuar-pay-merchant/src/locks.test.ts`
- `apps/lazuar-pay-merchant/README.md`
- `apps/lazuar-pay-merchant/package.json`
- `apps/lazuar-pay-checkout/src/App.tsx`
- `apps/lazuar-pay-checkout/src/locks.test.ts`
- `apps/lazuar-pay-checkout/README.md`

**Context (not re-invented as truth)**

- `plans/012-one-to-pay/04-pay-spec-contract.md` (historical: pay-spec was health-only; honesty pipeline must stay off Hub `task gen`)

---

## Live /v1 door inventory (method, path, auth, body, response) from source

Composition root (`Program.cs:82-92`):

```
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

JSON: snake_case on the wire (`Program.cs:25-29`). Errors: `{ status, title, detail }` (`PayErrors.cs:5-6`). Membership: Bearer required, then One `POST tenants/{orgId}/authz/check` relation `member` (`MemberGate.cs:8-43`, `OneClient.cs:69-90`). Writer: member **and** whoami tenant role `owner` or `admin` (`MemberGate.cs:45-71`). Problem `403` detail `"Writer role required"`.

Providers the host accepts (`PayProviders.TryNormalize`, `PayProviders.cs:26-30`): `stripe|chip|billplz|xendit|razorpay|test`. Listed processors add `test` only when `!IsProduction()` (`PayProviders.cs:18-22`). Capability constant: `"hosted_link"` (`PayProviders.cs:14`). Email required on start for every rail except Stripe and Test (`PayProviders.cs:35-36`).

### Door 1 — `GET /health`

- **Map:** `HealthEndpoints.cs:9`
- **Auth:** none
- **Body:** none
- **Response 200:** `{ "status": "ok" }`
- **Tests:** `HealthTests.Health_returns_ok`, `Health_does_not_call_one`
- **In TypeSpec?** No (unversioned process probe)
- **SPA?** No

### Door 2 — `GET /v1/health`

- **Map:** `HealthEndpoints.cs:10`
- **Auth:** none
- **Body:** none
- **Response 200:** `{ "status": "ok" }`
- **Tests:** `HealthTests.V1_health_returns_ok`
- **In TypeSpec?** Yes (`Health.check`)
- **SPA?** No

### Door 3 — `GET /ready`

- **Map:** `HealthEndpoints.cs:11-22`
- **Auth:** none
- **Body:** none
- **Response 200:** `{ "status": "ready" }` if `db.Database.CanConnectAsync`
- **Response 503:** `{ "status": "not_ready" }`
- **Tests:** **none** (HealthTests never hit `/ready`)
- **In TypeSpec?** No
- **SPA?** No
- **Note:** this is **Postgres liveness**, not `GET /v1/orgs/{orgId}/ready`.

### Door 4 — `GET /v1/whoami`

- **Map:** `WhoamiEndpoints.cs:10-22`
- **Auth:** Bearer. Optional header `X-Lazuar-Tenant-Id` forwarded to One as hint. No member check (Pay projects One `GET /me`).
- **Body:** none
- **Response 200:** `WhoamiResponse` (`WhoamiResponse.cs:3-21` + `OneMeMapper.cs:30-38`):
  - required: `user_id`, `is_platform_admin`, `tenants[]`
  - optional: `email`, **`name`**, `active_org_id`
  - tenant: `id`, `slug?`, `name?`, `role?`, `status?`
- **Errors:** 401 missing bearer / One rejected; 403 One forbade; 503 One unreachable or failed
- **Tests:** `WhoamiTests` (maps `name`, empty tenants, 401 skip One, One 401, timeout 503)
- **In TypeSpec?** Yes, **without `name`**
- **SPA:** merchant `getWhoami` (`payApi.ts:21-39`); `HomePage.tsx`, `OrgLayout.tsx`; `staffDisplay` prefers `who.name`

### Door 5 — `GET /v1/orgs/{orgId}/ready`

- **Map:** `OrgReadyEndpoints.cs:10-25`
- **Auth:** Bearer + **member** (not writer)
- **Body:** none
- **Response 200:** `{ org_id, ready: true }` always, once member. Dummy admin: membership is the whole check.
- **Tests:** `OrgReadyTests` (allow, allowed-false 403, One 403, One 500→503, no bearer 401 skips One, path org not header)
- **In TypeSpec?** Yes. Comment still says “Dummy admin”
- **SPA?** **No.** Org membership is inferred from whoami tenants (`OrgLayout.tsx:44-46`)

### Door 6 — `POST /v1/checkouts`

- **Map:** `CheckoutEndpoints.cs:14, 19-99`
- **Auth:** Bearer + **writer** on `body.org_id`
- **Headers:** optional `Idempotency-Key` (falls back to body `idempotency_key`, `CheckoutEndpoints.cs:76-80`)
- **Body (`CreateCheckoutRequest.cs:3-13`):**
  - `org_id?` (required in practice: empty org → 400 `"org_id is required"` via MemberGate)
  - **`provider?` required in practice** (`TryNormalize` on null → 400 `"unknown provider"`, `CheckoutTests.Create_without_provider_is_400`)
  - `product_id?`
  - `amount?` must be `> 0`
  - `currency?` default `MYR` uppercased
  - `success_url?`, `cancel_url?`
  - `idempotency_key?`
- **Behavior:** paused org → 403; unknown provider → 400; `test` allowed only when `AllowsTest`; other rails need vault row; persist Postgres; **201** `CheckoutSession`
- **Response 201 fields (`CheckoutSession.cs:3-21`):** `id`, `org_id`, `provider`, `product_id`, `payment_link_id`, `slot_key`, `amount`, `currency`, `status` (`"open"`), `public_token`, `interval` (`"one_off"`), `success_url`, `cancel_url`, `payer_name`, `payer_email`, `created_at`
- **Tests:** 401, 201 create+get, 404, 403 other org, idempotent 201 same id, default MYR, no provider 400, unknown 400, unconfigured rail 400, test without vault 201, amount 0 400, member 403, list newest-first
- **In TypeSpec?** Path yes. Body **missing `provider` and `product_id`**. Success documented as **200**. Session model missing most live fields.
- **SPA?** **No.** Merchant mints **payment-links**, not this door.

### Door 7 — `GET /v1/checkouts/{id}`

- **Map:** `CheckoutEndpoints.cs:15, 102-122`
- **Auth:** Bearer + member of **session.org_id** (lookup first; unknown id 404 **before** One call — `Get_unknown_is_404`)
- **Body:** none
- **Response 200:** full `CheckoutSession`
- **In TypeSpec?** Yes
- **SPA?** No

### Door 8 — `GET /v1/orgs/{orgId}/checkouts`

- **Map:** `CheckoutEndpoints.cs:16, 124-164`
- **Auth:** Bearer + member
- **Body:** none
- **Response 200:** array of `{ id, org_id, provider, amount, currency, status, public_token, created_at, label }` (`label` from product name)
- **Tests:** `List_returns_org_checkouts_newest_first`, `List_other_org_is_403`
- **In TypeSpec?** **No**
- **SPA?** **No** (Pay links page lists payment-links)

### Door 9 — `POST /v1/payment-links`

- **Map:** `PaymentLinkEndpoints.cs:14, 18-103`
- **Auth:** Bearer + **writer** on `body.org_id`
- **Body (`CreatePaymentLinkRequest.cs:3-13`):** `org_id?`, `provider?`, `product_id?`, `amount?` (>0), `currency?` (default MYR), `max_payers?` (default **1** unless `unlimited`), `unlimited` bool
- **Capacity:** `unlimited` → `max_payers` null; else `max_payers ?? 1`; `< 1` → 400
- **Response 201:** `PaymentLinkView` (`PaymentLinkEndpoints.cs:180-196`): `id`, `org_id`, `provider`, `amount`, `currency`, `status` (`open` or later `full`), `public_token`, `created_at`, `max_payers`, `unlimited`, `paid_count`, `taken_count`, `remaining`, `label?`
- **No Idempotency-Key**
- **Tests:** default one payer, unlimited null max, max 0 400, 401, list newest-first with capacity, other org 403, two-of-two then 409 full, same slot does not take two seats
- **In TypeSpec?** **No**
- **SPA:** **yes** — this is the merchant mint door (`CheckoutsPage.tsx:173-186`)

### Door 10 — `GET /v1/orgs/{orgId}/payment-links`

- **Map:** `PaymentLinkEndpoints.cs:15, 105-154`
- **Auth:** Bearer + member
- **Response 200:** array of `PaymentLinkView` (occupancy from child checkouts: `open`+`paid` count as taken; `status` becomes `"full"` when cap hit)
- **In TypeSpec?** **No**
- **SPA:** yes (`CheckoutsPage.tsx:118-120`)

### Door 11 — `POST /v1/orgs/{orgId}/products`

- **Map:** `CatalogEndpoints.cs:12, 16-63`
- **Auth:** Bearer + **writer**
- **Body (`CreateProductRequest`, `CatalogEndpoints.cs:88-95`):** `name` required, `description?`, `amount` > 0 required, `currency?` must be `MYR` (else 400 `"Bar B currency is MYR"`), `interval?` default `one_off`
- **Response 201:** `{ id, org_id, name, price_id, amount, currency, interval }`
- **Tests:** owner 201, member 403. No list test.
- **In TypeSpec?** Path yes. **No request body.** Return model is `{ id, org_id, name }` at **200**.
- **SPA:** yes (`CheckoutsPage.tsx:161-166` sends `{ name, amount, currency: "MYR" }`, reads `id`)

### Door 12 — `GET /v1/orgs/{orgId}/products`

- **Map:** `CatalogEndpoints.cs:13, 65-85`
- **Auth:** Bearer + member
- **Response 200:** array of `{ id, org_id, name, prices: [{ id, amount, currency, interval }] }`
- **In TypeSpec?** Path yes. Items are `Product` **without `prices`**.
- **SPA?** **No**

### Door 13 — `GET /v1/pay/{token}`

- **Map:** `PublicPayEndpoints.cs:23, 27-48`
- **Auth:** **none** (buyer, no One)
- **Query:** `slot_key?` (`string? slot_key` binds query; `PublicPayEndpoints.cs:29`)
- **Behavior:** token may be a **payment-link** public token **or** a **checkout** public token. Link + slot resumes that child; link full + max 1 paid returns the paid child; link full otherwise `{ status: "full", ... }`; missing → 404
- **Checkout view (`PublicPayEndpoints.cs:267-294`):** `token`, `amount`, `currency`, `status`, `payer_name`, `payer_email`, **`email_required`**, **`started`**, `provider`, `redirect_url` (only if started and still `open`), `remaining`, `max_payers`, `paid_count`, `taken_count`
- **Link view (`PublicPayEndpoints.cs:297-321`):** same plus no payer fields; `email_required` from link provider
- **Tests:** no bearer, 404, started+redirect after start, `email_required` true for chip / false for stripe, payment-link capacity
- **In TypeSpec?** Path yes. Model has `token, amount, currency, status, email_required?, started?, redirect_url?`. **Missing** `provider`, payer fields, capacity fields, `slot_key` query.
- **SPA:** checkout `App.tsx:35-36, 79-86` **always** sends `?slot_key=`

### Door 14 — `POST /v1/pay/{token}/start`

- **Map:** `PublicPayEndpoints.cs:24, 80-203`
- **Auth:** none
- **Body (`StartPayRequest`, `PublicPayEndpoints.cs:336-341`):** `name?`, `email?`, **`slot_key?`**
- **Payment-link token:** `slot_key` **required** (trim, length 8–128) else 400 `"slot_key is required"` (`MintOrResume` `PublicPayEndpoints.cs:219-223`). Mints or resumes a child checkout. Full → 409 `"This pay link is full"`.
- **Checkout token:** `slot_key` unused. Paid/expired → 409 `"Checkout is not open"`.
- **Email:** if `PayProviders.RequiresEmail(provider)` and email not usable (empty or `customer@example.com`) → 400 `"email is required"` (`PublicPayEndpoints.cs:146-149`, `BuyerEmail.cs:4-9`)
- **Idempotent PSP:** stored `PspRedirectUrl` returned without a second processor HTTP (`PublicPayTests.Start_twice_returns_same_url_without_second_psp_http`)
- **Test rail:** `CreateHostedUrl` returns checkout success URL; fulfillment runs inline (`PublicPayEndpoints.cs:176-186`, `TestHosted.cs:11-20`)
- **Response 200:** `{ redirect_url }`
- **Errors:** 404, 400 (email / slot_key / callback base), 403 paused, 409 not open / full, 503 rail not configured / Stripe rejected
- **In TypeSpec?** Path yes. Optional body `StartPayRequest { name?, email? }` — **no `slot_key`**. Dist OpenAPI has **no body at all**.
- **SPA:** checkout `App.tsx:129-132` POST `{ name, email, slot_key }`

### Door 15 — `PUT /v1/orgs/{orgId}/gateway`

- **Map:** `GatewayEndpoints.cs:16, 21-141`
- **Auth:** Bearer + **writer**
- **Body (`PutGatewayRequest`, `GatewayEndpoints.cs:253-262`):** `provider`, `secret`, `webhook_secret`, `public_merchant_id?`, `environment?` (`test|live`, default `test`), **`key_id?` + `key_secret?`** (concatenated into `secret` when secret empty)
- **Rules:** `test` → 400 `"test processor does not take secrets"`; secret required; webhook_secret required; Chip/Billplz require `public_merchant_id`; others reject it; Billplz requires explicit `environment`; Razorpay secret must be `key_id:key_secret`; never echoes secrets; capability `hosted_link`; does **not** set `OrgSettings.ActiveProvider`
- **Response 200:** `GatewayJson` (`GatewayEndpoints.cs:228-238`): `org_id`, `provider`, `last4`, `configured`, `capability`, `public_merchant_id`, `environment`, `webhook_configured`
- **Tests:** member 403, webhook required, no echo, chip brand id, unknown 400, member can GET metadata, list 6 processors, PUT test 400, billplz collection, razorpay colon
- **In TypeSpec?** Yes. `PutGateway` has `provider, secret, webhook_secret, public_merchant_id?, environment?`. Comment allow-list **omits `test`**. No `key_id`/`key_secret`. Dist OpenAPI **omits the whole Gateways interface**.
- **SPA:** yes (`GatewayPage.tsx:86-106`)

### Door 16 — `GET /v1/orgs/{orgId}/gateway`

- **Map:** `GatewayEndpoints.cs:17, 143-180`
- **Auth:** Bearer + member
- **Query:** `provider?`
- **If `provider` empty:** **aliases List** — returns `{ org_id, processors: [...] }` (`GatewayEndpoints.cs:158-160`, proven `GatewayTests` lines 164-168)
- **If `provider` set:** normalize; `test` + AllowsTest → `TestGatewayJson` (configured true, webhook_configured true, no last4); unknown → 400; missing row → `{ org_id, provider, configured: false }`; else `GatewayJson`
- **In TypeSpec?** Path yes as **singular `GatewayView`**. **No query param. No list envelope.**
- **SPA?** **No** (merchant always hits **`/gateways`**)

### Door 17 — `GET /v1/orgs/{orgId}/gateways`

- **Map:** `GatewayEndpoints.cs:18, 182-226`
- **Auth:** Bearer + member
- **Response 200:** `{ org_id, processors }` where `processors` is `PayProviders.Listed(env)` (5 rails, or 6 with `test` outside Production). Unconfigured real rails still appear with `configured: false`.
- **Tests:** `List_returns_all_five_and_put_does_not_default_pay_links` asserts **length 6** including `test` (Testing env)
- **In TypeSpec?** **No**
- **SPA:** **yes** — Overview, Gateway, Checkouts picker (`OverviewPage.tsx:14`, `GatewayPage.tsx:46`, `CheckoutsPage.tsx:125`)

### Door 18 — `POST /v1/webhooks/{provider}/{orgId}`

- **Map:** `WebhookEndpoints.cs:23-24`
- **Auth:** **not Bearer**. Provider-specific signature (Stripe `Stripe-Signature`, CHIP, Billplz query, Xendit, Razorpay, Test parse). Empty body 400. Unknown provider 400. Unconfigured rail 400. Test allowed only when `AllowsTest`.
- **Success bodies (all HTTP 200):**
  - `{ "ok": true }` fulfill path (`WebhookEndpoints.cs:172`)
  - `{ "duplicate": true }` replayed event id (`WebhookEndpoints.cs:90-92`, `158-159`)
  - `{ "ignored": "<reason>" }` setup / zero-amount / etc (`WebhookEndpoints.cs:95-98`)
- **Errors:** 400 verify / mismatch / checkout not found; 409 paused; 503 missing webhook secret; 500 fulfill failed
- **Tests:** `WebhookTests` completed+replay `duplicate`, setup `ignored`, zero ignored, bad signature 400, missing secret 503
- **In TypeSpec?** Path yes. Return type **only** `{ ok: boolean }`. Comment allow-list omits `test`. Dist matches tsp (`ok` only).
- **SPA:** does not POST it. Gateway UI **prints** the URL (`GatewayPage.tsx:304-308`)

### Door 19 — `GET /v1/orgs/{orgId}/payments`

- **Map:** `PaymentQueryEndpoints.cs:12, 17-62`
- **Auth:** Bearer + member
- **Response 200:** array `{ id, org_id, checkout_id, amount, currency, status, provider, payer_name, created_at, label }` from `charges` + checkout/product join
- **Tests:** `PaymentQueryTests.List_payments_includes_provider_and_label`
- **In TypeSpec?** **No**
- **SPA:** yes (`PaymentsPage.tsx:47-50`)

### Door 20 — `GET /v1/orgs/{orgId}/receipts`

- **Map:** `PaymentQueryEndpoints.cs:13, 64-118`
- **Auth:** Bearer + member
- **Response 200:** array `{ id, org_id, number` (`PENDING` if null), `title, checkout_id, amount, currency, payer_name, created_at, label, status` (`pending`|`issued`) `}`
- **Tests:** `List_receipts_includes_number_amount_and_payer`
- **In TypeSpec?** **No**
- **SPA:** yes (`ReceiptsPage.tsx:47-50`)

### Door 21 — `GET /v1/orgs/{orgId}/receipts/{id}`

- **Map:** `PaymentQueryEndpoints.cs:14, 120-144`
- **Auth:** Bearer + member
- **Response 200:** `{ id, org_id, number, title, checkout_id }` — **narrower** than list (no amount, payer, label, status)
- **404:** `"Receipt not found"`
- **Tests:** **none**
- **In TypeSpec?** **No**
- **SPA?** **No** (list only; no receipt detail route)

### Door 22 — `POST /v1/one/webhooks`

- **Map:** `OneWebhookEndpoints.cs:13-14`
- **Auth:** HMAC header `X-Lazuar-Signature` `t={unix},v1={hex}` over `{unix}.{body}` (`OneWebhookSignature.cs:7-18`). Missing `Pay:OneWebhookSecret` → 503. Bad HMAC → 401.
- **Body:** JSON with `type`, `id` (delivery), `org_id` or `tenant_id`. `tenant.suspended` pauses charges; `tenant.reactivated` unpauses.
- **Success 200:** `{ ok: true }` or `{ duplicate: true }`
- **Tests:** `OneWebhookTests`
- **In TypeSpec?** Path yes. Return `{ ok: boolean }` only. **No header, no body schema.**
- **SPA?** No

### Config that is not a door but is part of the public contract

`Pay:CheckoutBaseUrl` (`CheckoutUrls.cs:18-32`): required outside Testing; Development `appsettings.Development.json:11-13` sets `http://localhost:5179`; production `appsettings.json` does **not** set it; `.env.example:18` documents `Pay__CheckoutBaseUrl`; host README line 67 documents it. Payment-link mint writes success/cancel from this base (`PublicPayEndpoints.cs:244-259`). Missing value throws `InvalidOperationException("Pay:CheckoutBaseUrl is required")` which start maps to 400 (`PublicPayEndpoints.cs:194-197`). **Not in TypeSpec.**

---

## TypeSpec inventory

File: `packages/pay-spec/main.tsp` (195 lines). Service comment line 7: “Checkouts persist in Postgres; paid via verified PSP webhook.” Version `0.1.0`. Server `http://localhost:8081`. **No `@useAuth`, no security scheme, no `@statusCode` other than default 200, no problem-details model, no enums for provider/status/role.**

### Models

| Model | Fields | vs live |
|-------|--------|---------|
| `HealthResponse` | `status` | match |
| `WhoamiTenant` | `id`, `slug?`, `name?`, `role?`, `status?` | match |
| `WhoamiResponse` | `user_id`, `email?`, `is_platform_admin`, `active_org_id?`, `tenants` | live also **`name?`** |
| `OrgReadyResponse` | `org_id`, `ready` | match |
| `CreateCheckoutRequest` | `org_id`, `amount`, `currency?`, `success_url?`, `cancel_url?`, `idempotency_key?` | live **requires `provider`**; also `product_id?` |
| `CheckoutSession` | `id`, `org_id`, `amount`, `currency`, `status`, `success_url?`, `cancel_url?`, `created_at`, `public_token?` | live adds `provider`, `product_id`, `payment_link_id`, `slot_key`, `interval`, `payer_name`, `payer_email` |
| `PublicPay` | `token`, `amount`, `currency`, `status`, `email_required?`, `started?`, `redirect_url?` | live adds `provider`, payer fields, capacity fields |
| `StartPayRequest` | `name?`, `email?` | live adds **`slot_key?`** (required for payment-link tokens) |
| `GatewayView` | `org_id`, `provider?`, `last4?`, `configured`, `capability?`, `public_merchant_id?`, `environment?`, `webhook_configured?` | matches singular GET/PUT json; **not** the list envelope |
| `PutGateway` | `provider`, `secret`, `webhook_secret`, `public_merchant_id?`, `environment?` | live also `key_id`, `key_secret` |
| `Product` | `id`, `org_id`, `name` | live create 201 adds `price_id`, `amount`, `currency`, `interval`; list adds `prices[]` |
| `StartPayResponse` | `redirect_url` | match |

**No models** for: payment-link create/view, payments list, receipts list/detail, processors list envelope, webhook `{duplicate}`/`{ignored}`, problem JSON, `CreateProductRequest`.

### Operations (13)

| Op | TypeSpec | Comment / auth in tsp |
|----|----------|------------------------|
| `GET /v1/health` | `Health.check` | liveness |
| `GET /v1/whoami` | `Session.whoami` | “Requires Bearer” |
| `GET /v1/orgs/{orgId}/ready` | `Orgs.ready` | “Dummy admin… authz/check member” |
| `POST /v1/checkouts` | `Checkouts.create` | “Requires Bearer + **writer**”; optional header `Idempotency-Key`; body required |
| `GET /v1/checkouts/{id}` | `Checkouts.get` | “Merchant reads a checkout they are a member of” |
| `GET /v1/pay/{token}` | `PublicPayApi.get` | none |
| `POST /v1/pay/{token}/start` | `PublicPayApi.start` | **`@body body?: StartPayRequest`** (optional) |
| `POST /v1/orgs/{orgId}/products` | `Catalog.createProduct` | **path only — no `@body`** |
| `GET /v1/orgs/{orgId}/products` | `Catalog.listProducts` | none |
| `PUT /v1/orgs/{orgId}/gateway` | `Gateways.put` | “provider is stripe\|chip\|billplz\|xendit\|razorpay. **Writer only.**” — **no `test`** |
| `GET /v1/orgs/{orgId}/gateway` | `Gateways.get` | none; returns `GatewayView` |
| `POST /v1/webhooks/{provider}/{orgId}` | `Webhooks.psp` | same five-provider comment, no `test`; return `{ ok: boolean }` |
| `POST /v1/one/webhooks` | `Webhooks.one` | `{ ok: boolean }` |

**Absent from TypeSpec (present on host):** doors 1, 3, 8, 9, 10, 17, 19, 20, 21. Unversioned `/health` absence is consistent with Hub honesty (process probe). Unversioned `/ready` is the same class of probe. The other seven are **product doors**.

**Writer vs member in TypeSpec:** only two English comments (`Checkouts.create` writer; `Gateways.put` writer). Catalog create, payment-links (missing), and reads are not specified as OpenAPI security. `Orgs.ready` documents member in English. Generated OpenAPI cannot 403 a viewer because there is no scheme.

**`start` body (the asked-about case):** TypeSpec **does** declare an optional `StartPayRequest` as of this SHA (`main.tsp:69-72, 154-156`). It is **not** “no request body.” It is “optional `{name, email}` without `slot_key`.” The **on-disk OpenAPI** is the artifact that still has no requestBody. Treat those as two different lies (source tsp lagging live; dist lagging tsp).

---

## OpenAPI dist inventory (is it generated/stale?)

`tspconfig.yaml:1-8` emits `@typespec/openapi3` to `dist/openapi.yaml`. `package.json` `build` = `tsp compile .`. README: “OpenAPI lands in `dist/openapi.yaml` (gitignored).”

**Verdict: the on-disk yaml is a generated artifact that is stale relative to `main.tsp`, and would still be incomplete relative to the host even if recompiled.**

Evidence it is **stale vs current `main.tsp`:**

| | `main.tsp` now | `dist/openapi.yaml` now |
|--|----------------|-------------------------|
| `info.description` | “Checkouts persist in Postgres; paid via verified PSP webhook.” (`main.tsp:7`) | “Checkout is a **fixture (open session), not a charge.**” (`openapi.yaml:5`) |
| Tags | Health, Session, Orgs, Checkouts, Pay, Catalog, **Gateways**, Webhooks | Health, Session, Orgs, Checkouts, Pay, Catalog, Webhooks — **no Gateways tag** |
| `PUT/GET /v1/orgs/{orgId}/gateway` | present (`main.tsp:171-182`) | **absent** (no `/gateway` path) |
| `PublicPay` fields | `email_required?`, `started?`, `redirect_url?` (`main.tsp:64-66`) | only `token, amount, currency, status` (`openapi.yaml:306-322`) |
| `POST /v1/pay/{token}/start` body | optional `StartPayRequest` | **no `requestBody`** (`openapi.yaml:165-182`) |
| Schemas `StartPayRequest`, `GatewayView`, `PutGateway` | present | **absent** from `components.schemas` |

Dist **paths that exist** (11):

1. `POST /v1/checkouts` — 200 `CheckoutSession`; optional header `Idempotency-Key`; body `CreateCheckoutRequest` (no `provider`)
2. `GET /v1/checkouts/{id}` — 200 `CheckoutSession`
3. `GET /v1/health` — 200 `HealthResponse`
4. `POST /v1/one/webhooks` — 200 `{ ok }`
5. `POST /v1/orgs/{orgId}/products` — 200 `Product`, **no requestBody**
6. `GET /v1/orgs/{orgId}/products` — 200 `Product[]`
7. `GET /v1/orgs/{orgId}/ready` — 200 `OrgReadyResponse`
8. `GET /v1/pay/{token}` — 200 `PublicPay` (four fields)
9. `POST /v1/pay/{token}/start` — 200 `StartPayResponse`, no body
10. `POST /v1/webhooks/{provider}/{orgId}` — 200 `{ ok }`
11. `GET /v1/whoami` — 200 `WhoamiResponse` (no `name`)

**No** `securitySchemes`. **No** 201. **No** 4xx/5xx. **No** `/gateways`, payment-links, payments, receipts, unversioned probes.

**Freshness of `task pay:spec`:**

- Task exists (`Taskfile.yml:127-131`).
- CI job `pay` compiles TypeSpec (`ci.yml:117-118`) after host tests + Vite builds. That is a **compiler green**, not a contract gate.
- CI job `contracts` (`ci.yml:11-52`) runs Hub `task gen` + `check-openapi-minimal-honesty.mjs` against **`packages/api-spec`** and **`apps/lazuar-api`**. Pay is invisible there (script `SCAN_ROOTS` = `apps/lazuar-api/Modules` + Hub composition; `OPENAPI_PATH` = `packages/api-spec/dist/openapi.yaml`).
- Dist is gitignored, so there is no `git diff --exit-code` on Pay OpenAPI.
- Recompiling today would pick up Gateways + `StartPayRequest` + `email_required` from tsp, and would **still omit** payment-links, payments, receipts, list gateways, list checkouts, webhook variants, 201s, `slot_key`, product body, `test` provider, whoami `name`.

There is **no** `@repo/pay-types-ts`. Merchant and checkout hand-write types. IsolationTests only assert Vite `package.json` does not contain `@repo/api-types-ts`.

---

## SPA client inventory

### Merchant (`:5178`) — Pay HTTP

Shared helper `payFetch` (`payApi.ts:42-52`): `Authorization: Bearer`, `Accept: application/json`, optional `X-Lazuar-Tenant-Id`. **No cookies** (comment: localhost cookies are not port-scoped). **Never sends `Idempotency-Key`.** Base `VITE_PAY_API_URL ?? 'http://localhost:8081'`.

`oneApi.ts` calls **One** `POST {VITE_ONE_API_URL}/tenants` — correctly **not** a Pay `/v1` door. `CreateWorkspaceForm.tsx:38` uses it. pay-spec README forbids importing One routes; merchant obeyed.

| Call site | Method | Path | Send | Parse |
|-----------|--------|------|------|-------|
| `payApi.getWhoami` / `HomePage` / `OrgLayout` | GET | `/v1/whoami` | Bearer, optional tenant hint | `Whoami` including **`name?`** (`payApi.ts:11-18`) |
| `OverviewPage.tsx:14` | GET | `/v1/orgs/{orgId}/gateways` | Bearer + hint | `{ processors?: Processor[] }` |
| `GatewayPage.tsx:46` | GET | `/v1/orgs/{orgId}/gateways` | same | same |
| `GatewayPage.tsx:101` | PUT | `/v1/orgs/{orgId}/gateway` | `{ provider, webhook_secret, secret, public_merchant_id?, environment? }` | problem `detail` on error; refresh list |
| `CheckoutsPage.tsx:125` | GET | `/v1/orgs/{orgId}/gateways` | same | processors; `withTest` **unshifts test** if missing (`CheckoutsPage.tsx:32-38`) |
| `CheckoutsPage.tsx:118` | GET | `/v1/orgs/{orgId}/payment-links` | same | `PayLink[]` with capacity fields (`CheckoutsPage.tsx:71-85`) |
| `CheckoutsPage.tsx:161` | POST | `/v1/orgs/{orgId}/products` | `{ name, amount, currency: "MYR" }` | `{ id? }` |
| `CheckoutsPage.tsx:173` | POST | `/v1/payment-links` | `{ org_id, amount, currency, provider, product_id, max_payers?, unlimited }` | ok then reload list |
| `PaymentsPage.tsx:47` | GET | `/v1/orgs/{orgId}/payments` | same | `Payment[]` (`id, amount, currency, status, checkout_id, provider?, payer_name?, created_at?, label?`) |
| `ReceiptsPage.tsx:47` | GET | `/v1/orgs/{orgId}/receipts` | same | `Receipt[]` (`id, number, title, checkout_id, amount?, currency?, payer_name?, created_at?, label?, status?`) |

**Merchant does not call:** `/health`, `/v1/health`, `/ready`, `/v1/orgs/{id}/ready`, `POST /v1/checkouts`, `GET /v1/checkouts/{id}`, `GET /v1/orgs/{id}/checkouts`, `GET /v1/orgs/{id}/products`, `GET /v1/orgs/{id}/gateway`, `GET /v1/orgs/{id}/receipts/{id}`, public pay, webhooks.

**Writer UI:** `canWriteMoney` = role `owner|admin` (`roles.ts:1-4`). `OrgLayout` exposes `write`. Member sees “Member cannot create charges” / “Cannot paste keys.” Matches host `RequireWriterAsync`, **not** because TypeSpec encoded it.

**Processors (`processors.ts:1`):** `['test', 'stripe', 'chip', 'billplz', 'xendit', 'razorpay']`. Gateway page renders all six cards; test has no Edit. PUT never sends `provider: "test"` (host would 400).

**PUT payload honesty vs host:** Razorpay sends `secret: `${keyId}:${keySecret}`` not `key_id`/`key_secret` fields (host accepts both). Environment sent **only for billplz**; host defaults `"test"`. Chip/Billplz send `public_merchant_id`. Webhook URL displayed as `{payApi}/v1/webhooks/{editing}/{orgId}` — matches live.

### Checkout (`:5179`) — Pay HTTP

`App.tsx:8` same default origin. **No Bearer. No OIDC** (`locks.test.ts`).

| Call | Method | Path | Send | Parse |
|------|--------|------|------|-------|
| load + poll | GET | `/v1/pay/{token}?slot_key={uuid}` | none | `PayView`: `token, amount, currency, status, email_required?, started?, provider?, redirect_url?` (`App.tsx:10-19`) |
| Pay button | POST | `/v1/pay/{token}/start` | `{ name, email, slot_key }` | `{ redirect_url? }` or problem `detail`; 409 refetches GET |

`slotKey` persisted in `localStorage` key `lazuar-pay-slot:{token}` (`App.tsx:21-32`). Email gate: if `email_required` and not `usableEmail` (empty or `customer@example.com`) client-blocks (`App.tsx:119-122, 339-342`) matching `BuyerEmail`. Statuses handled: `paid`, `expired`, `full`, verifying poll 15×2s. **Does not type** `remaining` / `max_payers` / `paid_count` / `taken_count` / `payer_name` (host still sends them; extra JSON is ignored).

---

## Mismatches (table + evidence)

Live host is the reference column.

| # | Door / field | Live | TypeSpec | OpenAPI dist | SPA | Finding |
|---|--------------|------|----------|--------------|-----|---------|
| M1 | `POST /v1/pay/{token}/start` body | `{ name?, email?, slot_key? }`; slot_key **required** for payment-link tokens | optional `{ name?, email? }` no slot_key (`main.tsp:69-72,154-156`) | **no requestBody** (`openapi.yaml:165-182`) | checkout sends `{ name, email, slot_key }` | Dist stale vs tsp. Tsp incomplete vs host. SPA matches host. |
| M2 | `GET /v1/pay/{token}` query | `slot_key` | none | none | always `?slot_key=` | Spec lie. Host+SPA agree. |
| M3 | `PublicPay.email_required` | always boolean; true unless stripe/test | optional on model (`main.tsp:64`) | **omitted** from schema (`openapi.yaml:306-322`) | reads `email_required?` | Dist stale. Tsp almost honest. SPA matches host. |
| M4 | `PublicPay` extras | `started`, `provider`, `redirect_url`, capacity, payers | `started?`, `redirect_url?` only | none of those | types started/provider/redirect; ignores capacity | Spec under-describes. SPA under-types but functions. |
| M5 | Payments list | `GET /v1/orgs/{orgId}/payments` | **omitted** | omitted | merchant PaymentsPage | Spec lie (omission). Host+SPA agree. |
| M6 | Receipts list | `GET /v1/orgs/{orgId}/receipts` | omitted | omitted | merchant ReceiptsPage | Spec lie. |
| M7 | Receipt detail | `GET /v1/orgs/{orgId}/receipts/{id}` | omitted | omitted | unused | Spec lie. Host door with **no test and no client**. |
| M8 | Unversioned `/ready` | `GET /ready` db probe | omitted | omitted | unused | Same pattern as `/health` off-spec. Untested. |
| M9 | Webhook 200 variants | `{ok:true}` **or** `{duplicate:true}` **or** `{ignored: reason}` | `{ ok: boolean }` only (`main.tsp:190`) | `{ ok }` required (`openapi.yaml:198-208`) | n/a (PSP) | Spec lie. Host tests lock duplicate/ignored. |
| M10 | One webhook 200 | `{ok}` or `{duplicate}` + HMAC header | `{ ok }` no header | `{ ok }` | n/a | Spec lie. |
| M11 | `GET /v1/orgs/{id}/gateways` | `{ org_id, processors[5 or 6] }` | **omitted** | omitted | **this is the merchant list door** | Spec lie. Highest SPA impact after payment-links. |
| M12 | `GET /v1/orgs/{id}/gateway` | query `provider?`; empty query **= list envelope** | singular `GatewayView`, no query (`main.tsp:179-181`) | path **missing entirely** | SPA never calls singular | Dist stale vs tsp. Tsp wrong shape vs host. |
| M13 | Payment links | POST `/v1/payment-links` 201 + GET list with `max_payers, unlimited, remaining, taken_count, paid_count, status=full` | **omitted** | omitted | **primary merchant mint + table** | Spec lie. README curl still shows `POST /v1/checkouts`. |
| M14 | Test provider | `TryNormalize` includes `test`; listed when `!Production`; PUT test 400; start test fulfills | comments `stripe\|chip\|billplz\|xendit\|razorpay` **no test** (`main.tsp:174,187`) | no provider enum | `rails` includes `test`; `withTest` injects if list omits | Spec lie. SPA matches non-prod host; **production host omits test, SPA still injects** (SPA lie if that build is served in Production). |
| M15 | `email_required` rule | `RequiresEmail` = not stripe and not test | field exists, rule not documented | field missing | client trusts boolean | Spec gap (rule). Host+SPA agree. |
| M16 | `Pay:CheckoutBaseUrl` | required outside Testing; payment-link success/cancel | not in spec | not in spec | checkout origin is **Vite** `VITE_CHECKOUT_ORIGIN` default `:5179` (merchant copy URL) vs host config for PSP return | Two bases. README documents host config. Spec silent (config, not path) but public start 400 depends on it. |
| M17 | Writer vs member | writer = owner\|admin on mint/vault/catalog; member reads | English comments on 2 ops; no security scheme | no security | `canWriteMoney` | Spec under-documents. Host+SPA agree. Catalog/payment-links writer not even in comments. |
| M18 | `POST /v1/checkouts` | 201; **provider required**; extra session fields; Idempotency-Key | 200; no provider; fewer fields; header present (`main.tsp:136-139`) | 200; no provider; header present | unused | Spec lie (status + body). README curl **does** send provider — README matches host, not tsp. |
| M19 | `POST .../products` | 201; body name+amount; MYR; returns price fields | 200; **no body**; `Product` three fields | 200; no requestBody | sends name+amount+MYR | Spec lie. SPA matches host. |
| M20 | `GET .../products` | `prices[]` | `Product[]` no prices | same as tsp | unused | Spec lie. |
| M21 | Whoami `name` | mapped from One (`OneMeMapper.cs:34`, test asserts) | omitted (`main.tsp:25-31`) | omitted (`openapi.yaml:330-348`) | `Whoami.name?`; sidebar uses it | Spec lie. Host+SPA agree. |
| M22 | `GET /v1/orgs/{id}/checkouts` | exists | omitted | omitted | unused | Spec lie. Dead-ish to SPA (payment-links replaced it). |
| M23 | Error envelope | `{ status, title, detail }` | none | none | `problemDetail` reads `detail` | Spec lie. SPA matches host. |
| M24 | Status codes 401/403/404/409/503 | host + tests | default 200 only | 200 only | checkout handles 400/409/503 | Spec lie. |
| M25 | Idempotency | header or body on **checkouts only**; payment-links/products/start none | optional header on checkouts only | same as tsp | never sent | Spec matches the one door that has it. Kernel gap on the doors SPA actually POSTs. |
| M26 | Dist vs tsp | n/a | source of generate | **stale** (fixture blurb, no Gateways, no start body) | n/a | Generated artifact lie. |
| M27 | Merchant mint path vs README | SPA: products + payment-links | checkouts in spec; links not | checkouts | payment-links | README curl still true **as a host door**, false as “what the UI does.” |

---

## Kernel doors still missing (machine key, outbound payment.completed, idempotency headers)

These are **not** “spec forgot a Map*.” They are product-kernel doors that neither host nor pay-spec serve.

### Machine key (`lzr_sk_` / M2M)

- Pay host authenticates staff with **user Bearer** forwarded to One (`Bearer.cs`, `MemberGate`, `WhoamiEndpoints`). README: “Pay never holds a Zitadel PAT.”
- No `MapPost`/`MapGet` for API keys. `Rows.cs` has no key table. IsolationTests do not mention keys; they ban Hub payment factory types.
- TypeSpec has no `sk_` security scheme and no `/v1/orgs/{orgId}/keys`.
- Merchant is a PKCE SPA (`lazuar-pay-merchant/README.md`). Checkout is anonymous.
- **Missing:** a Pay-native machine credential that can mint checkouts / read payments without a human JWT. One owns `lzr_sk_` in **One’s** spec (012/04). Copying One key routes into pay-spec is refused. If Pay needs M2M, it is a **new Pay door** (or Pay-as-client of One keys) — not present on 8081 today.

### Outbound `payment.completed`

- Fulfillment on verified inbound webhook writes charge, journal, Official Receipt `RCPT-…`, audit action **`checkout.paid`** (`Fulfillment.cs:37-127`). No HTTP client, no outbox table, no HMAC to a merchant URL.
- IsolationTests **ban** `GatewayPaymentCompletedIntegrationEvent` (`IsolationTests.cs:9`) — Hub’s outbound event name is not to be resurrected as a type.
- TypeSpec has **inbound** `POST /v1/webhooks/{provider}/{orgId}` and `POST /v1/one/webhooks` only.
- **Missing:** merchant-configured egress `payment.completed` (and failed). Inbound PSP webhook is not that door.

### Idempotency headers

- Implemented: `POST /v1/checkouts` header `Idempotency-Key` or body `idempotency_key` → `idempotency_keys` row (`CheckoutStore.cs:11-50`). Tests: `Create_idempotent_on_key` (second POST still **201**, same id).
- TypeSpec documents the header on that one op. Dist does too.
- **Not implemented:** `POST /v1/payment-links`, `POST /v1/orgs/{orgId}/products`, `POST /v1/pay/{token}/start` (start uses stored `PspRedirectUrl` as a **different** idempotency: no second PSP session, not a client key).
- SPA POSTs products + payment-links **without** the header. Double-click create can mint two products and two links.
- Kernel gap: the header exists on the **unused** mint door and is absent on the **used** mint doors.

---

## Bugs

A **bug** here is a live lie, a generated artifact lie, or a client that will mis-talk the host — not merely “spec is a subset.”

1. **`dist/openapi.yaml` is stale vs `main.tsp`.** Description still says checkout is a fixture. Gateways interface and `StartPayRequest` exist in tsp and not in yaml. Anyone reading dist (or generating types from it without recompile) documents a host that does not exist. **Fix the artifact** by compiling after tsp is honest; do not treat dist as SSoT.

2. **`GET /v1/orgs/{orgId}/gateway` without `?provider=` returns the list envelope, while TypeSpec says `GatewayView`.** Same path, two shapes. A generated client of tsp would parse `{ processors: ... }` as a single gateway and lose every rail. Host chose aliasing (`GatewayEndpoints.cs:158-160`) after adding `/gateways`. Spec was not updated. **This is a host+spec collision on one URL.** Live is authority: document the query param and union, or stop aliasing and keep list on `/gateways` only.

3. **TypeSpec `Catalog.createProduct` has no body.** A generated client would POST empty. Host returns 400 `"name is required"` / `"amount must be greater than 0"`. Merchant happens to send the right JSON because it is hand-written. **Spec bug** (invalid contract), not a host bug.

4. **TypeSpec `CreateCheckoutRequest` omits `provider`, which live requires.** README curl includes `provider` (honest to host). A client generated from tsp would 400 `"unknown provider"`.

5. **Start without `slot_key` on a payment-link token is 400 live; TypeSpec optional body without that field; dist has no body.** Checkout SPA is correct. Any spec-generated buyer client is not.

6. **Merchant `withTest` synthesizes `{ provider: 'test', configured: true }` when the list omits test** (`CheckoutsPage.tsx:32-38`). Host **omits** test in Production (`PayProviders.Listed`). Creating a link with `provider: "test"` then 400 `"test processor is not enabled"`. Local/dev matches. A production merchant build with this helper is a **client bug**.

7. **OpenAPI / TypeSpec declare `POST /v1/checkouts` and `POST .../products` as 200; host returns 201.** Strict generated clients that only accept 200 fail on success. Tests lock 201 (`CheckoutTests.cs:58`, `CatalogTests.cs:36`, `PaymentLinkTests.cs:24`).

8. **Webhook spec `{ ok: boolean }` required** (`openapi.yaml:207-208` `required: [ok]`). Live duplicate/ignored 200 bodies **do not contain `ok`**. A generated integrator client treating 200 as `{ok:true}` is wrong; PSP adapters that only check HTTP 200 are fine.

9. **Whoami `name` lives on the wire and in the sidebar, not in tsp.** Not a runtime break (optional JSON). It is a contract bug if anyone generates `WhoamiResponse` from tsp and assumes name comes only from OIDC.

10. **`GET /v1/orgs/{orgId}/receipts/{id}` is mapped, untested, unused.** Narrower payload than list. Easy to ship a broken detail later. Honesty: a live door with no client and no test is a bug-shaped gap.

11. **Unversioned `GET /ready` is mapped and untested.** Orchestration that probes `/ready` has no unit lock.

None of these are “fix the spec in this task.” They are named so a later PR can pick a side.

---

## Gaps

Omissions that are not runtime-broken today because clients are hand-written against the host.

1. Seven product doors missing from TypeSpec: payment-links POST/GET, gateways list, payments list, receipts list, receipt GET, org checkouts list.
2. No problem-details model; SPA scrapes `detail` ad hoc.
3. No status enum (`open|paid|expired|full|pending|issued`).
4. No provider enum including `test`.
5. No query schemas (`slot_key`, `provider`).
6. No HMAC / PSP signature headers in OpenAPI.
7. No `@useAuth` Bearer for merchant doors; public vs staff is English only.
8. Writer vs member not encoded (catalog, payment-links undocumented).
9. Capacity fields on public GET and payment-link view.
10. Product prices / create body / 201 extra fields.
11. CheckoutSession extra fields (`provider`, `public_token` actually always set live, payers, interval).
12. `Idempotency-Key` not on the POST doors the SPA uses.
13. No machine-key door.
14. No outbound merchant webhook door.
15. No `@repo/pay-types-ts`; no Kiota/NSwag from pay-spec.
16. IsolationTests do not compile or scrape pay-spec.
17. Hub honesty script does not scrape `apps/lazuar-pay`.
18. `task pay:spec` does not fail if tsp ⊂ host.
19. pay-spec README “Grow `main.tsp` when a Pay `/v1` door exists” is process; the doors exist and were not grown.
20. Host README curl still teaches `POST /v1/checkouts` as the mint path; UI teaches payment-links.
21. Checkout `PayView` does not parse remaining capacity (only `status === 'full'`).
22. `GET .../products` unused by merchant (create-only sidecar for labels).
23. Org ready unused by merchant (whoami tenants instead).
24. `CheckoutBaseUrl` vs `VITE_CHECKOUT_ORIGIN` — two configs for “where the buyer page lives.” They match in default dev (`:5179`); nothing in spec says they must.

---

## How to solve (which side to change)

Do **not** shrink the host to the spec. Live is the product. SPA that already match live should not be rewritten to match tsp.

| Finding | Change this side | Do not |
|---------|------------------|--------|
| Missing payment-links, payments, receipts, `/gateways`, list checkouts | **TypeSpec** grow models + ops to the live JSON (201, capacity, processors envelope). Then `task pay:spec`. | Do not delete host doors. Do not import Hub `/public/commerce`. |
| Stale dist | Recompile after tsp is grown. Keep dist gitignored **or** commit it and dirty-check — pick one. Today: gitignored + stale leftover is the worst of both. | Do not hook `packages/pay-spec` into Hub `task gen` / `honesty-allowlist.yaml` / job `contracts` (012/04). |
| Honesty gate | **New Pay script** (or a flag on the existing one) that scrapes `MapGet\|Post\|Put` under `apps/lazuar-pay/src` vs `packages/pay-spec/dist/openapi.yaml`. Allowlist unversioned `/health` and `/ready` as host-only. CI job `pay` already compiles tsp — add the scrape there, not to Hub `contracts`. | Do not scan `apps/lazuar-api`. |
| `start` body / `slot_key` | **TypeSpec** required-for-links is host-shaped: document `slot_key` on body and query. Dist will follow compile. | Do not remove slot_key from host or checkout SPA. |
| Catalog create body | **TypeSpec** add `CreateProductRequest` and 201 body with `price_id`. | Do not make host accept empty POST. |
| Checkout create `provider` + 201 | **TypeSpec** add `provider` required (or default documented — host has no default). `@statusCode 201`. | Do not stop returning 201; tests lock it. |
| GET `/gateway` alias list | Prefer **host** stop aliasing (singular GET without provider should 400 or 404) **or** **spec** documents “no provider ⇒ list envelope.” Spec+host must pick one. SPA already uses `/gateways`. Lean: host keep `/gateways` as list; singular requires `provider`. | Do not make SPA call singular. |
| Webhook `{duplicate}`/`{ignored}` | **TypeSpec** union 200 bodies. | Do not change host replay to always `{ok:true}` (would hide idempotent PSP semantics). |
| Whoami `name` | **TypeSpec** add `name?: string`. | Do not drop mapping; sidebar uses it. |
| Test provider | **TypeSpec** allow-list comments + enum include `test`; document PUT 400 and Production omission. **SPA** `withTest`: only inject when list includes test **or** when `import.meta.env.DEV`. | Do not enable test in Production host. |
| Writer/member | **TypeSpec** `@useAuth` Bearer + doc 401/403; comment catalog + payment-links writer. Host already enforces. | Do not invent VIEWER. |
| Idempotency on SPA POSTs | **Host** add `Idempotency-Key` to payment-links (and maybe products) **then** spec it. SPA should send the header once the host honors it. | Do not pretend checkouts-only header covers the UI. |
| Machine key / outbound `payment.completed` | New program. Host first, then tsp. Not a documentation-only fix. | Do not copy Hub `GatewayPaymentCompletedIntegrationEvent` (IsolationTests ban). Do not copy One `/api-keys` into pay-spec. |
| `CheckoutBaseUrl` | Keep as config. Document in README (already). Optional tsp `@doc` on start 400. Align `VITE_CHECKOUT_ORIGIN` in deploy. | Do not put the URL in TypeSpec as a path. |
| README curl | Add a **second** example: POST products + POST payment-links (what `:5178` does). Keep checkouts curl if that door stays. | Do not delete a true host example. |
| Generated TS types | Later `@repo/pay-types-ts` from **pay-spec after it matches host**. Merchant/checkout switch off hand types. | Do not generate from Hub `api-spec`. Do not generate from today’s tsp (it would type-check lies). |
| Receipt GET by id / `/ready` | Either add tests + spec, or delete the Map if unused. Live-as-authority means **spec it** if it stays. | Do not leave mapped-untested forever. |

---

## Tests vs missing (contract tests)

**What exists (behavioral, host-shaped — good, not a substitute for OpenAPI honesty):**

| Area | File | Locks |
|------|------|-------|
| Isolation | `IsolationTests.cs` | no Hub csproj, no MediatR/Modules, Vite no `@repo/api-types-ts` |
| Health | `HealthTests.cs` | `/health`, `/v1/health`, no One. **Not `/ready`.** |
| Whoami | `WhoamiTests.cs` | mapping including `name`, 401, 503 |
| Org ready | `OrgReadyTests.cs` | member, 403, 503, path vs header |
| Checkouts | `CheckoutTests.cs` | 201, writer, provider required, test rail, idempotency, list |
| Payment links | `PaymentLinkTests.cs` | 201, capacity, slot, 409 full |
| Catalog | `CatalogTests.cs` | owner 201, member 403. **No list, no body-field lock beyond create.** |
| Gateways | `GatewayTests.cs` | writer, list length 6, alias GET `/gateway` → processors, PUT test 400 |
| Public pay | `PublicPayTests.cs` | anonymous GET, start body `{name,email}`, email_required chip/stripe, 409, 403, 503 |
| Webhooks | `WebhookTests.cs` | `duplicate`, `ignored` |
| Payments/receipts | `PaymentQueryTests.cs` | list fields. **No GET by id.** |
| One inbound | `OneWebhookTests.cs` | HMAC, pause |
| Merchant locks | `locks.test.ts` | string presence of `/gateways`, test rail, no Hub types — **not HTTP** |
| Checkout locks | `locks.test.ts` | `slot_key`, `email_required` placeholder, `/v1/pay/` — **not HTTP** |

**Missing contract tests:**

1. No scrape: Pay `Map*` ⊆ pay-spec OpenAPI ∪ allowlist; OpenAPI ⊆ Map*.
2. No test that `task pay:spec` output matches `main.tsp` (dist stale would be invisible even in CI: compile writes gitignored yaml and throws it away).
3. No golden OpenAPI fixture committed.
4. No consumer contract: merchant/checkout fetch paths vs OpenAPI path list.
5. No test that TypeSpec `start` body includes `slot_key`.
6. No test for `GET /ready`.
7. No test for `GET /v1/orgs/{orgId}/receipts/{id}`.
8. IsolationTests do not open `packages/pay-spec/main.tsp` (could at least ban `@repo/api-spec` imports / `/public/commerce` — README-only today).
9. Hub `check-openapi-minimal-honesty.mjs` cannot be reused as-is: it strips `/api/v1` and scans Hub Modules.

CI job `pay` (`ci.yml:96-118`) is the right place to hang a Pay honesty step after `tsp compile`. It already has Node + the host tests.

---

## Ranked findings

1. **Payment-links + payments + receipts + `/gateways` are live product doors the SPA uses and TypeSpec does not mention.** Dist does not mention them either. This is the headline spec lie. Host and both Vite apps are aligned with each other.
2. **`dist/openapi.yaml` is stale vs `main.tsp`** (fixture description, missing Gateways, missing start body, missing `email_required`). Compile task exists; freshness is not enforced.
3. **`POST /v1/pay/{token}/start`:** host `{name, email, slot_key}` with slot required on links; tsp optional `{name, email}`; dist no body. Checkout SPA is the honest client.
4. **Catalog create has no TypeSpec body; host requires name+amount and returns 201 with prices.** Merchant matches host.
5. **GET `/gateway` vs GET `/gateways`:** host has both; empty-query singular aliases list; tsp has only singular `GatewayView`; dist has neither. SPA uses list.
6. **Webhook 200 `{duplicate}` / `{ignored}` omitted** from tsp/OpenAPI; host tests depend on them.
7. **Writer vs member** is real on the host and in the SPA, and is almost invisible in OpenAPI (two comments, no security scheme). Catalog and payment-links writer not documented at all.
8. **Test processor** is a first-class non-prod rail on host+SPA; tsp comments exclude it; Production host vs SPA `withTest` can diverge.
9. **`email_required` + `CheckoutBaseUrl`:** tsp has the flag (dist dropped it); the boolean rule and the checkout base config are host behavior the public start door depends on.
10. **Kernel still missing:** machine key, outbound `payment.completed`, Idempotency-Key on the mint doors the UI actually uses.
11. **README curl `POST /v1/checkouts` is still a true host example** (writer, provider, 201) and is **not** what `:5178` sends. Whoami curl is true.
12. **`task pay:spec` / CI compile is a syntax gate**, not an honesty gate. Hub `contracts` job is the wrong tree.
13. **Whoami `name`, 201 vs 200, problem JSON, query params, extra checkout fields** — field-level spec lies under the path-level holes.
14. **Unversioned `/ready` and GET receipt-by-id** are live Maps with no tests (and no spec).
15. **No `@repo/pay-types-ts`.** Hand-written SPA types are currently **more honest** than pay-spec. Generating types from today’s tsp would make the UIs worse.

---

## Refuse

This paper does not:

- Edit `main.tsp`, OpenAPI, C#, Vite, tests, README, or Taskfile.
- Hook `packages/pay-spec` into Hub `task gen`, NSwag, Kiota, `honesty-allowlist.yaml`, or CI job `contracts`.
- Import One `/tenants`, `/me`, `/api-keys`, LHDN, or Hub `/public/commerce` into pay-spec.
- Invent an outbound dispatcher or machine-key vault in prose as if it existed.
- Shrink live doors to fit a stale yaml.
- Treat `plans/012` / `013` / `015` tracker cells as live inventory (they were used only as historical contrast; 012/04 described health-only tsp — that is **false on this SHA**).
- Claim IsolationTests are a contract suite.
- Claim CI `Compile pay-spec` means OpenAPI matches 8081.

---

## Appendix: quoted evidence

### A. Composition — every Map*

```82:92:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
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

### B. TypeSpec start + StartPayRequest (optional, no slot_key)

```69:72:packages/pay-spec/main.tsp
model StartPayRequest {
  name?: string;
  email?: string;
}
```

```149:156:packages/pay-spec/main.tsp
interface PublicPayApi {
  @get
  @route("/pay/{token}")
  get(@path token: string): PublicPay;

  @post
  @route("/pay/{token}/start")
  start(@path token: string, @body body?: StartPayRequest): StartPayResponse;
}
```

### C. Dist OpenAPI start — no requestBody; fixture blurb

```5:5:packages/pay-spec/dist/openapi.yaml
  description: Focused Pay HTTP contract. Not packages/api-spec. Checkout is a fixture (open session), not a charge.
```

```165:182:packages/pay-spec/dist/openapi.yaml
  /v1/pay/{token}/start:
    post:
      operationId: PublicPayApi_start
      parameters:
        - name: token
          in: path
          required: true
          schema:
            type: string
      responses:
        '200':
          description: The request has succeeded.
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/StartPayResponse'
      tags:
        - Pay
```

### D. Live start body + slot_key required on links

```336:341:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
public sealed class StartPayRequest
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? SlotKey { get; set; }
}
```

```219:223:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
        var slot = NormalizeSlotKey(body?.SlotKey);
        if (slot is null)
        {
            return (null, PayErrors.Status(400, "Bad Request", "slot_key is required"));
        }
```

### E. Checkout SPA sends the live shape

```129:132:apps/lazuar-pay-checkout/src/App.tsx
      const response = await fetch(`${payApi}/v1/pay/${token}/start`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, email, slot_key: slotKey(token) }),
      })
```

### F. Live GET public pay includes email_required; TypeSpec has it; dist does not

```275:289:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
        var emailRequired = PayProviders.TryNormalize(provider, out var p) && PayProviders.RequiresEmail(p);
        var started = !string.IsNullOrWhiteSpace(row.PspRedirectUrl);
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
            provider,
            redirect_url = started && row.Status == "open" ? row.PspRedirectUrl : null,
```

```59:67:packages/pay-spec/main.tsp
model PublicPay {
  token: string;
  amount: decimal;
  currency: string;
  status: string;
  email_required?: boolean;
  started?: boolean;
  redirect_url?: string;
}
```

```306:322:packages/pay-spec/dist/openapi.yaml
    PublicPay:
      type: object
      required:
        - token
        - amount
        - currency
        - status
      properties:
        token:
          type: string
        amount:
          type: number
          format: decimal
        currency:
          type: string
        status:
          type: string
```

```35:36:apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs
    public static bool RequiresEmail(string provider) =>
        provider is not Stripe and not Test;
```

### G. Webhook duplicate / ignored vs spec `{ ok }`

```90:98:apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs
        if (await db.PspWebhookEvents.FindAsync([orgId, name, parsed.EventId], ct) is not null)
        {
            return Results.Ok(new { duplicate = true });
        }

        if (parsed.Ignored)
        {
            await InsertEventAsync(db, orgId, name, parsed.EventId, ct);
            return Results.Json(new { ignored = parsed.IgnoreReason }, OneClient.Json);
```

```184:190:packages/pay-spec/main.tsp
interface Webhooks {
  /** Plane B. provider is stripe|chip|billplz|xendit|razorpay. */
  @post
  @route("/webhooks/{provider}/{orgId}")
  psp(@path provider: string, @path orgId: string): { ok: boolean };
```

### H. Gateways: live list + singular alias; tsp singular only; dist neither

```16:18:apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs
        app.MapPut("/v1/orgs/{orgId}/gateway", Put);
        app.MapGet("/v1/orgs/{orgId}/gateway", Get);
        app.MapGet("/v1/orgs/{orgId}/gateways", List);
```

```158:160:apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs
        if (string.IsNullOrWhiteSpace(provider))
        {
            return await List(orgId, request, one, db, env, ct);
```

```171:181:packages/pay-spec/main.tsp
@tag("Gateways")
interface Gateways {
  /** BYOK keys. provider is stripe|chip|billplz|xendit|razorpay. Writer only. */
  @put
  @route("/orgs/{orgId}/gateway")
  put(@path orgId: string, @body body: PutGateway): GatewayView;

  @get
  @route("/orgs/{orgId}/gateway")
  get(@path orgId: string): GatewayView;
}
```

### I. Payment-links — live + SPA, absent from spec

```14:15:apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs
        app.MapPost("/v1/payment-links", Create);
        app.MapGet("/v1/orgs/{orgId}/payment-links", List);
```

```173:185:apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx
    const checkout = await payFetch(token, '/v1/payment-links', {
      method: 'POST',
      orgHint: orgId,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        org_id: orgId,
        amount: Number(amount),
        currency: 'MYR',
        provider,
        product_id: product.id,
        max_payers: capacity === 'one' ? 1 : capacity === 'limited' ? limited : undefined,
        unlimited: capacity === 'unlimited',
      }),
    })
```

### J. Payments + receipts — live + SPA, absent from spec

```12:14:apps/lazuar-pay/src/Lazuar.Pay/Money/Queries/PaymentQueryEndpoints.cs
        app.MapGet("/v1/orgs/{orgId}/payments", List);
        app.MapGet("/v1/orgs/{orgId}/receipts", ListReceipts);
        app.MapGet("/v1/orgs/{orgId}/receipts/{id}", Receipt);
```

### K. Unversioned /ready

```11:21:apps/lazuar-pay/src/Lazuar.Pay/Hosting/HealthEndpoints.cs
        app.MapGet("/ready", async (PayDbContext db, CancellationToken ct) =>
        {
            try
            {
                await db.Database.CanConnectAsync(ct);
                return Results.Ok(new { status = "ready" });
            }
            catch
            {
                return Results.Json(new { status = "not_ready" }, statusCode: 503);
            }
        });
```

### L. Catalog TypeSpec no body vs live CreateProductRequest

```161:168:packages/pay-spec/main.tsp
interface Catalog {
  @post
  @route("/orgs/{orgId}/products")
  createProduct(@path orgId: string): Product;

  @get
  @route("/orgs/{orgId}/products")
  listProducts(@path orgId: string): Product[];
}
```

```26:41:apps/lazuar-pay/src/Lazuar.Pay/Catalog/CatalogEndpoints.cs
        var name = body?.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return PayErrors.Status(400, "Bad Request", "name is required");
        }
        // ...
        if (body?.Amount is null || body.Amount <= 0)
        {
            return PayErrors.Status(400, "Bad Request", "amount must be greater than 0");
        }
```

### M. Test provider — host allow-list vs tsp comment vs SPA rails

```16:30:apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs
    public static readonly string[] All = [Stripe, Chip, Billplz, Xendit, Razorpay];

    public static IReadOnlyList<string> Listed(IHostEnvironment env) =>
        AllowsTest(env) ? [..All, Test] : All;

    public static bool AllowsTest(IHostEnvironment env) =>
        !env.IsProduction();
    // ...
        return provider is Stripe or Chip or Billplz or Xendit or Razorpay or Test;
```

```1:1:apps/lazuar-pay-merchant/src/lib/processors.ts
export const rails = ['test', 'stripe', 'chip', 'billplz', 'xendit', 'razorpay'] as const
```

```32:38:apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx
function withTest(list: Processor[]): Processor[] {
  const ready = list.filter((p) => p.configured && isRail(p.provider))
  if (!ready.some((p) => p.provider === 'test')) {
    ready.unshift(testProcessor)
  }
  return ready
}
```

### N. Writer vs member

```45:69:apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs
    public static async Task<IResult?> RequireWriterAsync(
        HttpRequest request,
        OneClient one,
        string orgId,
        CancellationToken cancellationToken)
    {
        var denied = await RequireMemberAsync(request, one, orgId, cancellationToken);
        // ...
        var role = who.Value.Tenants.FirstOrDefault(t => t.Id == orgId)?.Role;
        if (role is not ("owner" or "admin"))
        {
            return PayErrors.Status(403, "Forbidden", "Writer role required");
        }
```

```1:4:apps/lazuar-pay-merchant/src/lib/roles.ts
/** One tenant roles. Pay: owner/admin write money; member is read-only. */
export function canWriteMoney(role: string | undefined | null): boolean {
  return role === 'owner' || role === 'admin'
}
```

```133:134:packages/pay-spec/main.tsp
  /** Merchant creates a checkout. org_id is the One tenant id. Requires Bearer + writer. */
```

```174:174:packages/pay-spec/main.tsp
  /** BYOK keys. provider is stripe|chip|billplz|xendit|razorpay. Writer only. */
```

Host README (`apps/lazuar-pay/README.md:69`): “`POST /v1/checkouts` requires writer.” “`/v1/orgs/{orgId}/ready` checks `member`.” Merchant README: “`member` is read-only on money.” Catalog and payment-links writer are host-enforced and **not** in TypeSpec comments.

### O. CheckoutBaseUrl

```18:31:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/CheckoutUrls.cs
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

```11:13:apps/lazuar-pay/src/Lazuar.Pay/appsettings.Development.json
  "Pay": {
    "CheckoutBaseUrl": "http://localhost:5179"
  }
```

```17:18:apps/lazuar-pay/.env.example
# Buyer return origin for hosted success/cancel defaults. Not the Billplz callback.
# Pay__CheckoutBaseUrl=http://localhost:5179
```

### P. README curl still true as host doors

```50:63:apps/lazuar-pay/README.md
```bash
curl -sS -H "Authorization: Bearer $ACCESS_TOKEN" http://localhost:8081/v1/whoami
# no header → 401
```

Create a workspace in **lazuar-app** (`:5174`) first if `tenants` is empty, then:

```bash
curl -sS -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"org_id":"'"$ORG_ID"'","amount":10.00,"currency":"MYR","provider":"stripe","success_url":"https://example.test/ok","cancel_url":"https://example.test/no"}' \
  http://localhost:8081/v1/checkouts
# GET /v1/checkouts/{id} with the same Bearer
```
```

Whoami curl: **true**. Checkouts curl: **true against the host** if the caller is writer, Stripe is vaulted, amount > 0 — response is **201**, not documented. The JSON includes `provider`, which TypeSpec omits. The merchant UI does **not** run this curl; it POSTs `/v1/payment-links`.

### Q. Idempotency only on checkouts

```76:80:apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs
        var idempotency = request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotency))
        {
            idempotency = body.IdempotencyKey;
        }
```

```136:138:packages/pay-spec/main.tsp
    @header("Idempotency-Key") idempotencyKey?: string,
    @body body: CreateCheckoutRequest,
```

Merchant `payApi.ts` / `CheckoutsPage.tsx`: no `Idempotency-Key` string anywhere (grep empty).

### R. Fulfillment is not outbound payment.completed

```121:127:apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs
        db.AuditEvents.Add(new AuditEventRow
        {
            Id = Guid.NewGuid().ToString("N"),
            OrgId = checkout.OrgId,
            Action = "checkout.paid",
            At = DateTimeOffset.UtcNow
        });
```

```8:10:apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs
        "IPaymentGatewayAdapter", "PaymentGatewayFactory",
        "IPaymentGatewayFactory", "AddPaymentsModule", "GatewayPaymentCompletedIntegrationEvent", "Modules.Payments",
```

### S. CI compiles tsp; Hub honesty does not see Pay

```117:118:.github/workflows/ci.yml
      - name: Compile pay-spec
        run: pnpm --filter @repo/pay-spec exec tsp compile .
```

```31:35:scripts/check-openapi-minimal-honesty.mjs
const OPENAPI_PATH = path.join(ROOT, "packages/api-spec/dist/openapi.yaml");
const ALLOWLIST_PATH = path.join(ROOT, "packages/api-spec/honesty-allowlist.yaml");
const SCAN_ROOTS = [
  path.join(ROOT, "apps/lazuar-api/Modules"),
  path.join(ROOT, "apps/lazuar-api/src/Lazuar.Api/Composition"),
];
```

```13:13:packages/pay-spec/README.md
OpenAPI lands in `dist/openapi.yaml` (gitignored). Grow `main.tsp` when a Pay `/v1` door exists. Not Hub `task gen`.
```

### T. Whoami name — live + SPA, not tsp

```3:11:apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiResponse.cs
public sealed class WhoamiResponse
{
    public required string UserId { get; init; }
    public string? Email { get; init; }
    public string? Name { get; init; }
    public bool IsPlatformAdmin { get; init; }
    public string? ActiveOrgId { get; init; }
    public IReadOnlyList<WhoamiTenant> Tenants { get; init; } = [];
}
```

```25:31:packages/pay-spec/main.tsp
model WhoamiResponse {
  user_id: string;
  email?: string;
  is_platform_admin: boolean;
  active_org_id?: string;
  tenants: WhoamiTenant[];
}
```

```11:17:apps/lazuar-pay-merchant/src/lib/payApi.ts
export type Whoami = {
  user_id: string
  email?: string
  name?: string
  is_platform_admin: boolean
  active_org_id?: string
  tenants: WhoamiTenant[]
}
```

---

**End of paper.** Live `:8081` is a 22-door host. TypeSpec describes 13. Dist describes 11 of an older 13. The two Vite apps talk to the host, not to the spec. Grow pay-spec toward the host; do not shrink the host toward the yaml.
