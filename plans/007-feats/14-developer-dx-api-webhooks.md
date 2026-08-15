# 14 — Developer DX, APIs, and webhooks

**Program:** Lazuar Pay competitor-feature analysis (subagent 14 of 20)  
**Workspace:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Written:** 2026-08-16  
**Status:** Full uncondensed analysis. Do not summarize this file.  
**Does not ship product code.** Tracker IDs below are a promotion catalog for a later checklist, not a commitment to implement.

This chapter answers one product question:

> If an integrator who already knows Stripe, Xendit, Billplz, Paddle, or Polar sits down to integrate **Lazuar Pay / Lazuar Hub**, what do they actually get — keys, auth, contracts, events, SDKs, sandbox, idempotency, versioning, workbench, logs — and how far is that from Stripe-class DX?

It is **not** an Aura salon-ops chapter. Aura is treated here as **the first-party consumer** of Hub’s public surface (guest money / System B), not as the product being documented. Guest-pay soak honesty from the Aura tracker (`PY-001`…`PY-008`) still applies: production guest fulfillment is not claimed. That is a **consume** honesty rule, not a Hub DX feature gap.

---

## Method

### What was read (primary)

| Surface | Absolute path | Why |
|---------|---------------|-----|
| Developer Hub app | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-developers/` | Public docs shell, guides, Scalar mounts |
| TypeSpec / OpenAPI | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/` | Contract SSoT, product docs entrypoints, honesty allowlist |
| One credentials + webhooks | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/` | Platform `ApiCredential`, workspace webhooks, provision, dispatcher |
| Payments M2M cashier | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/IntegrationEndpoints.cs` + outbound handler | Integrator checkouts, `/me`, payment.* events |
| LHDN façade | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/` | Product API, leftover webhook registry, taxpayer validate |
| Host auth | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs`, `Composition/AuthAndCorsExtensions.cs` | Machine auth + policy catalog |
| Ops Developer console | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/{ApiKeysPage,DeveloperSettingsPage,DeliveryLogsPage}.tsx` | Human key + webhook UX |
| Sample app | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/examples/hub-cashier-next/` | Second-app cashier proof |
| VitePress docs | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/` | Human integration narrative |
| SDKs | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/lhdn-sdk-ts/`, `packages/lhdn-sdk-dotnet/` | Only published-shape external SDKs |
| Versioning | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/api-versioning.md` | Written v1 policy |
| Payments quickstart | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/payments-integration-quickstart.md` | Cashier SSoT for humans |

### What was read (historical — treat as **stale unless re-verified**)

The `docs/001-gaps/` series (`03-api-auth-credentials.md`, `04-developers-page-dx.md`, `13-typespec-api-contracts.md`, `18-outbound-customer-webhooks.md`, `02-payment-webhooks.md`) was written against an earlier `lazuar-hub` tree. Many P0 items in those files are **now implemented**. This chapter treats them as a **pre-platform-credentials snapshot**, not as current truth.

Verified as **superseded** (do not repeat as if still true):

- API keys are no longer LHDN-table-only. Platform store is `one.ApiCredentials`. Middleware is One-only.
- `GET /one/api-keys` exists. Ops has API Keys UI. Create response includes id / prefix / hint / scopes / plain_key once.
- Scopes exist and are enforced. `API_CLIENT` is **not** in `OrgAdmin`.
- `docs-commerce.tsp` is in the build. Scalar `/commerce` exists. `docs-one` / `docs-ops` no longer import billing.
- LHDN TypeSpec route is `@route("/lhdn")` (double `/api/v1` prefix **fixed**).
- OpenAPI `info.version` is **1.0.0**, not `0.0.0`.
- Workspace webhooks are **multi-endpoint**, event-filtered, Standard Webhooks–style signed (`t=,v1=`), secret rotate exists, secrets encrypted at rest, dispatcher uses `FOR UPDATE SKIP LOCKED`.
- Silent product-URL equality drop is **gone**. Fan-out is all active endpoints that `AcceptsEvent`.
- `payment.completed` / `payment.failed` exist for M2M checkouts.
- Developers hub is no longer Scalar-only: Auth, Event catalog, LHDN quickstart, Payments cashier guides exist.
- LHDN SDKs prepend `Bearer ` automatically.
- Taxpayer validate **is** mapped (`POST /lhdn/taxpayer/validate`).
- LHDN dispatch publishes onto One’s durable path (fire-and-forget `WebhookSenderService` **retired** as the send path).

Verified as **still true or newly true**:

- No Payments SDK. No Commerce SDK. No shared `verifyLazuarSignature` published package (only sample + docs snippets).
- No last-used, no key rotate endpoint, no dual-key grace, no expiry, no IP allowlist, no inbound rate limit on product APIs.
- No Stripe CLI / Workbench / test clocks / status page / request log explorer.
- No webhook redeliver API. Delivery logs have no response body / HTTP status.
- `POST /lhdn/webhooks` still writes `lhdn.WebhookSubscriptions` while dispatch fans out **only** to `one.TenantWebhookEndpoints` — a live honesty hole.
- TypeSpec `PaymentWebhookPayloadDto` does **not** match the runtime envelope.
- Catalog pages list `invoice.submitted` / `invoice.cancelled` / `payment.refunded` that are not MVP-emitted.
- `sk_test_` / `sk_live_` collide with Stripe merchant secret prefixes (documented; Prefix decision = B).
- Billplz sandbox vs live follows Hub `App:ApiBaseUrl`, **not** the K1 prefix.

### Competitor method

Compared against **integrator DX**, not merchant checkout UX:

| Competitor | Why they are in the set |
|------------|-------------------------|
| **Stripe** | Global gold standard for keys, webhooks, CLI, Workbench, test clocks, versioning, SDKs, event catalog |
| **Xendit** | SEA PSP closest to “dashboard Developers + test/live keys + SDKs + webhooks” that Malaysian integrators already know |
| **Billplz** | The local rail Hub wraps; the DX we are supposed to **replace** for integrators |
| **Paddle** | MoR / SaaS-billing DX (notifications, sandbox, versioned Billing API) — relevant as the **other** money plane Aura already uses (System A), not as a Hub clone |
| **Polar** | Modern OSS-adjacent merchant-of-record DX (Standard Webhooks, clean OpenAPI, first-party SDKs) — the “small Stripe” bar |

Sources: current Stripe / Xendit docs (as of mid-August 2026 crawl), plus this repo’s first-party contracts and UIs. No competitor dashboard was logged into.

### Honesty rules used in this file

1. **Code wins** over VitePress, Scalar copy, and `001-gaps`.  
2. **Guides may be ahead of TypeSpec** and **TypeSpec may be ahead of runtime** — those are called out as honesty defects, not features.  
3. “Shipped” means a human or machine can use it on a running Hub without SQL.  
4. “Partial” means a slice exists but an integrator would still hit a lie, a missing primitive, or a second undocumented hop.  
5. Aura Connect is **one** provision client. Multi-app cashier is the DX claim. The sample (`external_product: sample-shop`) is the evidence path.

---

## Stripe-class DX baseline

This is the bar, not a feature request list. Stripe in 2026 is the reference because every other PSP in this set either copies a subset of it or deliberately stays thinner.

### 1. Credentials

Stripe’s model (2026 docs):

| Artifact | Prefix / shape | Role |
|----------|----------------|------|
| Publishable key | `pk_test_` / `pk_live_` | Browser / mobile; cannot do secret operations |
| Secret key | `sk_test_` / `sk_live_` | Server; full account power unless restricted |
| Restricted key | `rk_test_` / `rk_live_` | Server; **permission matrix** the merchant picks. Stripe now **recommends RAKs over long-lived secret keys** for live |
| Webhook signing secret | `whsec_…` | **Not an API key.** Per-endpoint. Used only to verify Stripe → you |
| Connect / OAuth | platform keys + connected-account headers | Platforms mint access for connected merchants |
| Sandbox keys | Per-sandbox key set | Isolated test worlds; objects do not leak into live |

Lifecycle Stripe productizes:

- Create / reveal once / roll / expire  
- Last-used  
- IP allowlist  
- Permission editor (RAKs)  
- Test vs live (and now **multiple sandboxes**, not one shared test mode)  
- Dashboard **and** API for key management  
- Clear copy: “this is a Stripe secret; do not paste it into another vendor”

What “Stripe-class” means for a **payments cashier** like Hub:

1. Machine secrets are **not** dashboard JWTs.  
2. Secrets are hashed at rest, shown once, listed as `prefix…hint`.  
3. Test and live are **hard partitions** of data and of processor environment.  
4. Scopes / restricted keys exist so a leaked checkout key cannot mint more keys or rewrite BYOK vaults.  
5. Webhook secrets are a **different object** from API keys.  
6. There is a **Connect** story for platforms (Aura, a second SaaS, an ERP) that is not “email us for a key.”

### 2. Auth model

Stripe: `Authorization: Bearer sk_…` (or restricted key) on every secret request. Publishable keys on client. Connect uses `Stripe-Account` header. No “use your dashboard cookie against the API.”

Xendit: secret API keys per test/live; public keys only tokenize cards. Dashboard → Settings → Developers → API Keys.

Billplz: a single API key + collection id. Signature is HMAC over **form fields**, not a versioned webhook standard. No scope catalog. Test is a **different host** (`billplz-sandbox.com`).

Paddle: vendor auth / API keys against a Billing API; sandbox is a separate account/environment; notifications are a first-class destination object.

Polar: org access tokens + Standard Webhooks; sandbox; OpenAPI-first.

Stripe-class rule: **three planes, never mixed**

| Plane | Actor | Credential |
|-------|-------|------------|
| Human console | Merchant / ops | Session (cookie / SSO) |
| Machine product API | Integrator server | Secret / restricted key |
| Platform provision | First-party platform (Aura, sample shop) | Provision secret or OAuth app |

### 3. Contracts and honesty

Stripe-class contract DX:

- OpenAPI (or equivalent) that **is** the public API  
- Per-product docs (Payments, Billing, Tax, Connect) — not one mega-spec of internal chat  
- Error model with **stable codes**  
- Request IDs on every response  
- Idempotency-Key on mutating POSTs, documented, with conflict semantics  
- Versioning: account-pinned API version + `Stripe-Version` header + changelog. Breaking changes do not silently hit old integrations  
- Webhook payload version can be pinned **per endpoint**

Honesty means: if Scalar or an SDK method exists, the route exists, the auth on the route is the auth in the docs, and the JSON the webhook sends is the JSON the schema shows.

### 4. Events

Stripe-class outbound webhooks:

- Event catalog with types, sample payloads, and “when it fires”  
- Multi-endpoint, per-endpoint event filters  
- `Stripe-Signature: t=…,v1=…` (timestamp + HMAC; replay window)  
- Automatic retries with a long tail  
- Dashboard: delivery attempts, request/response bodies, **Resend**  
- CLI: `stripe listen` + `stripe trigger` + `stripe events resend`  
- Disable / fail an endpoint after repeated errors  
- SSRF / HTTPS policy  
- Separate signing secret per endpoint; rotation  
- Idempotency guidance: process by `event.id`

### 5. SDKs and samples

Stripe ships official SDKs in many languages, generated + hand-written helpers (`constructEvent`, idempotency, retries, telemetry). Quickstarts are **copy-paste to first successful charge**. Sample apps exist per stack.

Xendit: Node, PHP, Python, Java, Go — invoice / VA / e-wallet shaped.

Paddle: Billing SDKs + notification verification helpers.

Polar: first-party SDKs + Standard Webhooks verify.

Billplz: community / thin official surface; form-urlencoded; integrators typically write raw HMAC. This is the **anti-baseline**.

### 6. Sandbox, test clocks, workbench, CLI, status, logs

| Primitive | Stripe | Why integrators notice |
|-----------|--------|------------------------|
| Sandboxes | Isolated test worlds + copy-from-live settings | Teams do not share one polluted test mode |
| Test clocks | Advance subscription time | Dunning / renewals without waiting |
| Workbench | Replaces Developers Dashboard: inspect requests, events, Inspector | Debug without grepping prod logs |
| CLI | `stripe listen`, `trigger`, `logs tail`, sandbox provision | Local webhooks without ngrok theatre |
| Request logs | Every API call, filterable | “What did we send?” |
| Status page | status.stripe.com | Distinguish “us” vs “them” during an incident |
| Idempotency | Header on POST; 24h cache; conflict if body changes | Safe retries |
| Versioning | Pinned + explicit upgrade | No surprise breaks |

Xendit / Paddle / Polar have **subsets** (test/live, webhook retries, sandbox accounts, decent docs). None of them fully match Workbench + CLI + test clocks + request logs together. Billplz has almost none of this.

### 7. The integrator journey Stripe trained the market on

```text
1. Sign up
2. Land in a sandbox with test keys already visible
3. Developers → API keys (copy sk_test once)
4. Install SDK or curl from the Quickstart
5. Create a Checkout Session / PaymentIntent
6. stripe listen → forward webhooks to localhost
7. Trigger payment_intent.succeeded
8. Verify signature in 15 lines
9. Open Workbench, see the request + the event
10. Switch to live keys + live webhook endpoint
11. Watch logs / status if something breaks
```

Time-to-first-successful-webhook is the metric. Everything else is retention.

### 8. What “good enough for SEA cashier” still requires

Even if Hub refuses to become Stripe, a Malaysian multi-app cashier that wants to beat **Billplz DIY** must still have:

1. Named test/live keys, reveal once, revoke, list  
2. Scopes so a salon key cannot submit e-invoices and an LHDN key cannot create checkouts  
3. A provision story for platforms (Aura and a second app)  
4. Signed webhooks with timestamp + retries + logs  
5. Honest OpenAPI for the **three** integrator products (Payments, Commerce public, LHDN)  
6. A sample that fulfills **only** on the signed webhook  
7. Documented idempotency on money POSTs  
8. A versioning promise in writing  

Workbench, CLI, test clocks, status page, last-used, and official Payments SDKs are **the Stripe-class gap**, not the MVP bar. This chapter tracks both.

---

## Our surfaces (honest)

This section is a ground-truth inventory of what exists in `lazuar-pay` on 2026-08-16. Paths are absolute.

### A. Product map (three integrator products + two human planes)

| Product | Audience | Auth | Primary paths | Docs |
|---------|----------|------|---------------|------|
| **Payments (M2M cashier)** | Any server app | Bearer `sk_` + `payments.checkouts:*` | `POST/GET /api/v1/integrations/payments/checkouts`, `GET /integrations/payments/me` | Scalar `/payments`, VitePress integrations/*, Hub `/payments-cashier` |
| **Commerce (CaaS v1)** | Storefronts + unlock/revoke | Public (no key) + workspace webhooks | `/api/v1/public/commerce/*` | Scalar `/commerce`, Hub event catalog |
| **LHDN (e-invoice)** | ERP / compliance | Bearer `sk_` + `lhdn.documents:*` | `/api/v1/lhdn/documents`, `/taxpayer/validate`, cert/config | Scalar `/lhdn`, Hub `/quickstart` |
| **One (platform)** | Humans + provision | Cookie JWT / provision secret | `/api/v1/one/*` (auth, workspaces, keys, webhooks, provision) | Scalar `/one`, Hub `/auth` |
| **Ops / Billing / Platform admin** | First-party consoles | Cookie JWT + OrgAdmin / SUPER_ADMIN | `/admin/*`, `/ops/*`, `/api/v1/platform/*` | Scalar `/ops` (labeled Internal), `/billing` (Admin) |

Inbound **gateway** webhooks (`POST /api/v1/webhooks/payments/{gatewayType}/{tenantId}`) are **not** a product API. They are allowlisted as impl-only. Integrators never call them.

### B. Developer Hub (`apps/lazuar-developers`)

App: Next.js 16, port **3002**, production mount **`hub.lazuar.com/docs*`** (`NEXT_BASE_PATH=/docs`). README is still create-next-app boilerplate. Product narrative lives in the pages, not the README.

**Landing** (`app/page.tsx`) is honest about the intended story:

- Start here: `/quickstart` (LHDN), `/payments-cashier`, `/auth`, `/webhooks`  
- API references: LHDN (Primary), Payments (Cashier), One (Platform), Commerce (v1), Billing (Admin), Ops (Internal, dashed card)  
- Footer: production API `https://hub.lazuar.com/api/v1`; SDKs `@lazuar/lhdn-sdk` · `Lazuar.Lhdn.Sdk`  
- Copy tells humans to mint keys in **Ops → Developer → API Keys** and call with `Bearer sk_…`

**Guides that exist (not Scalar):**

| Route | File | What it actually contains |
|-------|------|---------------------------|
| `/auth` | `app/auth/page.tsx` | Two-credential model; closed scope catalog; Aura default scopes; revoke/cache 5-minute note; “never JWT in ERP” |
| `/quickstart` | `app/quickstart/page.tsx` | First e-invoice: curl + TS + .NET; Idempotency-Key; signature sketch |
| `/payments-cashier` | `app/payments-cashier/page.tsx` | Provision → checkout → verify webhooks; product-line split; second-app pointer |
| `/webhooks` | `app/webhooks/page.tsx` | Commerce + payment + LHDN catalogs; Node/C# verify snippets |

**Scalar mounts** (spec loaded at module start from `packages/api-spec/dist/<module>/openapi.yaml` or `OPENAPI_SPEC_ROOT`):

| Route | Spec | Title |
|-------|------|--------|
| `/lhdn` | `dist/lhdn/openapi.yaml` | Lazuar LHDN API |
| `/payments` | `dist/payments/openapi.yaml` | Lazuar Payments Integration API |
| `/one` | `dist/one/openapi.yaml` | Lazuar Platform API |
| `/commerce` | `dist/commerce/openapi.yaml` | Lazuar Commerce API |
| `/billing` | `dist/billing/openapi.yaml` | Lazuar Billing API |
| `/ops` | `dist/ops/openapi.yaml` | Lazuar Ops API (Internal) |

**Hub chrome gaps vs Stripe:**

- `HubShell` nav is LHDN-centric (`Hub / Quickstart / Authentication / Event catalog / LHDN API`). Payments cashier and Commerce are **not** in the top nav — only on the landing grid.  
- No login, no “try it with my workspace key,” no environment switcher beyond OpenAPI `servers`.  
- No changelog, no status widget, no SDK version badge that is live (footer hardcodes package names, not versions).  
- VitePress (`lazuar-docs`) is a **second** docs site. Hub is Scalar + four guides. Integrators must discover both.

### C. TypeSpec / OpenAPI honesty

**Pipeline** (`Taskfile.yml` `task gen`):

```text
packages/api-spec/*.tsp
  → tsp compile main.tsp            → dist/openapi.yaml          (internal codegen)
  → tsp compile docs-one            → dist/one/openapi.yaml
  → tsp compile docs-ops            → dist/ops/openapi.yaml
  → tsp compile docs-billing        → dist/billing/openapi.yaml
  → tsp compile docs-lhdn           → dist/lhdn/openapi.yaml
  → tsp compile docs-commerce       → dist/commerce/openapi.yaml
  → tsp compile docs-payments       → dist/payments/openapi.yaml
        ├─ openapi-typescript → packages/api-types-ts
        ├─ NSwag              → packages/api-types-dotnet
        └─ Kiota (LHDN only)  → lhdn-sdk-ts / lhdn-sdk-dotnet
```

CI: `task gen --force` + `git diff --exit-code` on generated clients + `node scripts/check-openapi-minimal-honesty.mjs` (R25).

**Honesty allowlist** (`packages/api-spec/honesty-allowlist.yaml`):

- `openapi_only_exceptions: []` — **no known phantom OpenAPI paths**. This is a real quality win versus the 001-gaps era (portal cancel, list keys, taxpayer validate were phantoms then).  
- `impl_only` (intentional host-only): billing signed final PDF, inbound payment webhooks, messaging notify/logs, communications unsubscribe + Resend webhook, communications legacy-cleanup.

**Product docs purity (ADR 007) — current:**

| Entrypoint | Imports | Purity |
|------------|---------|--------|
| `docs-one.tsp` | One only | **Clean** (billing leak removed) |
| `docs-ops.tsp` | Ops only | **Clean**; marked Internal |
| `docs-billing.tsp` | Billing only | Clean; audience is admin |
| `docs-lhdn.tsp` | LHDN + One key DTO aliases | Clean; dual auth annotation |
| `docs-commerce.tsp` | Commerce + Communications | Wired; admin + public both published |
| `docs-payments.tsp` | Payments only | Clean; best external posture |

**Versions:** TypeSpec `@info(#{ version: "1.0.0" })` on product docs. Policy: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/api-versioning.md`. Additive = non-breaking; breaking = `/api/v2` or deprecation ≥90 days.

**Remaining contract honesty defects (live):**

1. **`PaymentWebhookPayloadDto` is not the wire format.**  
   TypeSpec models a **flat** object: `event_id`, `event_type`, `checkout_id`, `workspace_id`, `amount`, …  
   Runtime One dispatcher wraps **any** payload as:

   ```json
   { "id": "<uuid v7>", "event_type": "payment.completed", "created_at": "…", "data": { … } }
   ```

   Runtime `data` (from `IntegrationCheckoutGatewayEventsHandler.BuildPayload`) is:

   ```json
   {
     "event_id": "…",
     "checkout_id": "…",
     "gateway": "STRIPE",
     "gateway_transaction_id": "pi_…",
     "provider_session_id": "cs_…",
     "amount": 50.00,
     "currency": "MYR",
     "status": "completed",
     "metadata": { },
     "description": "…",
     "customer_email": "…"
   }
   ```

   VitePress (`apps/lazuar-docs/docs/integrations/webhooks.md`) documents the **envelope**. Developers hub `/webhooks` documents the **envelope + data**. TypeSpec documents a **third, flat** shape with `workspace_id` / `occurred_at` that the handler does not emit. Scalar “Payments” therefore **lies about webhooks** (and webhooks are not even operations on that interface — the DTO is an orphan model).

2. **Commerce webhook models are documentation-only.**  
   `modules/commerce/models/webhooks.tsp` says “doc model — not a POST body.” Runtime does wrap `data` in the same envelope. The TypeSpec union omits `order.completed` and `payment_link.paid` even though those fire. Integrators who generate clients from Commerce OpenAPI do **not** get a webhook receiver type that matches the dispatcher.

3. **LHDN webhook register `events[]` is still fiction.**  
   `RegisterWebhookCommand` stores URL + secret only. `ListWebhooksQuery` **hardcodes** `["invoice.valid","invoice.invalid"]`. TypeSpec still requires/advertises `events` on register.

4. **LHDN `/lhdn/webhooks` CRUD vs dispatch.**  
   Register writes `lhdn.WebhookSubscriptions`. Dispatch (`DispatchExternalWebhookCommand`) publishes `OutboundWebhookRequestedIntegrationEvent` with `TargetUrl: null`. One handler fans out to **`one.TenantWebhookEndpoints` only**.  
   Therefore: an integrator who follows the LHDN SDK / Hub quickstart “or `POST /lhdn/webhooks`” can persist a subscription that **never receives a delivery**. The working path is Ops → Outbound Webhooks or `POST /one/workspaces/{id}/webhooks`.

5. **Catalog oversell.**  
   Hub `/webhooks` lists `invoice.submitted`, `invoice.cancelled` (“when emitted”). Convergence lock (`plans/005-remaining/webhook-convergence-decisions.md`) says those are **out of MVP**. Poller only dispatches VALID/INVALID.  
   VitePress events.md lists `payment.refunded` as “maturing.” No M2M outbound refund event is implemented.

6. **Auth annotations vs cookies.**  
   One routes are `@useAuth(BearerAuth)`. Browsers use HttpOnly cookies (`lazuar_auth`). Scalar “Try it” with a Bearer JWT is possible but not the product flow. Cookie scheme is not modeled.

7. **`X-Tenant-Id` still not in TypeSpec** for admin surfaces. Humans in ops-page send it; API keys skip tenant middleware. Scalar will not prompt.

8. **ProblemDetails status union** in `common/models.tsp` is `400 | 401 | 403 | 404 | 500`. Runtime Payments uses **409, 422, 502**. Provision uses **429**. The shared error model under-specifies the cashier.

9. **Commerce docs still ship `/admin/commerce/*` and Communications admin** to a public hub card labeled “integrator v1.” The `@doc` on `docs-commerce.tsp` *says* admin is console-only, but Scalar still renders the whole admin surface. Audience mismatch ADR 007 warned about, partially recreated.

10. **LHDN SDK still generates key-management + taxpayer + webhooks** against a façade that is only half-true (keys façade is real and points at One; webhooks façade is a zombie registry).

### D. API keys (scoped, live/test, rotate, last-used)

#### Domain

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/ApiCredential.cs`

| Field | Present? | Notes |
|-------|----------|-------|
| Id | Yes | UUIDv7 |
| OrganizationId | Yes | Workspace-bound |
| Name | Yes | Human label |
| Prefix | Yes | `sk_test_` or `sk_live_` |
| KeyHash | Yes | SHA-256 hex of **full** plain key |
| KeyHint | Yes | Last 4 of plain key |
| Scopes | Yes | Space-separated closed catalog |
| IsActive | Yes | Soft revoke |
| CreatedAt | Yes | |
| CreatedByUserId | Yes | Null for system/legacy |
| **LastUsedAt** | **No** | Documented backlog (`docs/payments-integration-quickstart.md` §5; VitePress api-keys.md) |
| ExpiresAt | No | |
| RevokedAt / RevokedBy | No | Only `IsActive=false` |
| IP allowlist | No | |
| Rate-limit tier | No | |

#### Scope catalog (`PlatformApiScopes`)

Closed allowlist:

| Scope | Purpose |
|-------|---------|
| `lhdn.documents:write` | Submit / cancel |
| `lhdn.documents:read` | Status + TIN validate (write implies read) |
| `payments.checkouts:write` | Create M2M checkout |
| `payments.checkouts:read` | Poll checkout (write implies read) |
| `payments.config:read` | Connection status, no secrets |
| `webhooks.endpoints:manage` | Register/rotate/disable workspace webhooks via API |

**Not scopes (correctly denied to machines):**

- Key mint / list secrets / revoke (`OrgAdmin` JWT only)  
- Payment-config **write** (BYOK vault)  
- Superadmin / platform  
- Ops chat  

Defaults:

- Omit scopes on mint → **LHDN document default** (legacy trap). Ops UI forces explicit selection. Provision bootstrap uses Payments integrator set.  
- `DefaultAuraIntegratorScopes` = checkout write + read + webhook manage. **No LHDN.**

Duplicate leftover: `Modules.Lhdn.Domain.ApiKeyScopes` still exists (LHDN-only subset) and is used when the LHDN façade splits scope strings. Source of truth for validation is One.

#### Mint / list / revoke

| Method | Path | Auth | Impl |
|--------|------|------|------|
| GET | `/api/v1/one/api-keys` | OrgAdmin JWT | `ApiCredentialEndpoints` |
| POST | `/api/v1/one/api-keys` | OrgAdmin JWT | Generate; returns `plain_key` once |
| DELETE | `/api/v1/one/api-keys/{id}` | OrgAdmin JWT | Soft revoke + `ApiKeyRevokedIntegrationEvent` |
| GET/POST/DELETE | `/api/v1/lhdn/api-keys` | OrgAdmin JWT | **Façade** over `IApiCredentialService` (same table) |

There is **no** `POST /one/api-keys/{id}/rotate`. Rotation procedure is mint new → deploy → revoke old (single-cut). No dual-valid window.

#### Middleware

`ApiKeyAuthenticationMiddleware`:

- Accepts `Authorization: Bearer sk_test_|sk_live_…` **or** raw `Authorization: sk_…`  
- Hashes full token; lookup **only** `one.ApiCredentials` (`IsActive=true`)  
- 5-minute `IMemoryCache`; tenant reverse index 10 minutes  
- Claims: `NameIdentifier=api_client`, `CredentialId`, `TenantId`, `IsTestMode`, `Role=API_CLIENT`, `scope`*  
- Invalid key → 401 `{ error: "Invalid or revoked API Key." }` (not RFC7807)  
- Legacy `lhdn.DeveloperApiKeys` dual-read **removed**. Residual LHDN-only keys 401.  
- **Does not write LastUsedAt.**  
- Cache eviction on revoke is in-process. Multi-pod worst case ≤5 minutes without the local event handler.

`TenantSecurityMiddleware` still skips membership for `AuthenticationType == "ApiKey"`. Tenant comes from the key.

#### Ops UI

`/developer/api-keys` (`ApiKeysPage.tsx`):

- Table: name, prefix…hint, test/live badge, scopes, active/revoked, created, revoke  
- Create modal: name, test/live, scope checkboxes, presets **LHDN documents** and **Payments integrator**  
- One-time reveal + QuickCopy  
- Explicit warning: this is a **Lazuar Pay** `sk_`, not a Stripe `sk_`  
- Deep links to `/docs/lhdn`, `/docs/one`, `/docs/auth`  
- **No last-used column. No rotate. No expiry. No IP allowlist.**

Sidebar (`Sidebar.tsx`): Developer → API Keys / Outbound Webhooks / Delivery Logs.

#### Prefix collision (Prefix decision = B)

Hub mints `sk_test_` / `sk_live_`. Stripe merchant BYOK secrets use the same prefixes. Aura P04 must `GET /integrations/payments/me` and treat 401/403 as `PAY_KEY_INVALID`, not regex-accept. Documented in payments quickstart §8.1. This is a **permanent DX footgun** we chose to keep for Stripe-familiarity.

### E. Auth model (JWT vs secret vs Connect provision)

Three planes, implemented:

| Plane | Credential | Who | What it can do |
|-------|------------|-----|----------------|
| **Human session** | HttpOnly JWT cookie `lazuar_auth` (workspace) / `lazuar_admin_auth` (platform path) | Ops, portal, admin | OrgAdmin surfaces, key mint, BYOK, webhook UI |
| **Machine key (K1)** | `Authorization: Bearer sk_test_|sk_live_…` | Integrator servers | Only routes with matching **scope policies** |
| **Provision (K0)** | `X-Lazuar-Provision-Key` or `Authorization: Bearer <provision-secret>` or SUPER_ADMIN JWT | Platforms (Aura Connect, sample) | `POST /one/integrations/workspaces/provision` — create workspace + first key + optional webhook |

**Not implemented:**

- OAuth2 client_credentials  
- Stripe Connect OAuth / Account Links  
- Publishable `pk_` keys (Hub has no client-side Payments.js; guests go to **gateway** hosted pages)  
- Per-integrator provision secrets (today often **one env secret per Hub deployment**)

**Policy catalog** (`AuthAndCorsExtensions`):

| Policy | Humans | Machines |
|--------|--------|----------|
| `OrgAdmin` | SUPER_ADMIN, ADMIN | **Denied** (API_CLIENT removed — this was the P0 security fix) |
| `IntegrationLhdnDocumentsWrite/Read` | Admins bypass | `lhdn.documents:*` |
| `IntegrationPaymentsCheckoutsWrite/Read` | Admins bypass | `payments.checkouts:*` |
| `IntegrationPaymentsConfigRead` | Admins bypass | `payments.config:read` |
| `IntegrationWebhooksEndpointsManage` | Admins bypass | `webhooks.endpoints:manage` |
| `IntegrationPaymentsMe` | **Denied** | Any `payments.*` scope |

`GET /integrations/payments/me` is the introspect: `workspace_id`, `organization_id`, `is_test_mode`, `key_id`, `key_name`, `scopes`, `has_active_gateway`, `gateway_names`. Never echoes `sk_` or `whsec_`.

**JWT details (humans):** HMAC-SHA256; production **fails boot** if `Jwt:Secret` is missing or the hardcoded dev default. Expiry hours from config (default 24h historically). Security-stamp re-check is still `/auth/me`-centric, not every request — irrelevant to machine DX if integrators stay off JWT.

**Connect provision** (`IntegrationProvisionEndpoints` + `ProvisionAuraWorkspaceCommandHandler`):

- Canonical body: `external_product` + `external_org_id`  
- Legacy: only `aura_org_id` → product `aura`  
- `aurabook` alias folds to `aura`  
- Idempotent on `(external_product, external_org_id)`  
- First materialization returns `api_key.plain_key` + `webhook.secret_key`; repeats do **not** re-reveal  
- Optional `webhook_url`, `webhook_enabled_events` (default `payment.completed` + `payment.failed`)  
- Optional `owner_email` attaches membership if the user already exists  
- In-memory token-bucket rate limiter (`IntegratorProvisionRateLimiter`) per secret identity + per (product, org) — **single-instance**, not Redis  
- This is **Connect-lite**, not OAuth. Fine for Aura + a second app. Not a marketplace of third-party apps with delegated scoped tokens.

### F. Event catalog, signatures, retries, redrive

#### Catalog that actually fires

| Event | Emitter | Path | Payload richness |
|-------|---------|------|------------------|
| `payment.completed` | `IntegrationCheckoutGatewayEventsHandler` | M2M checkout paid | **Rich:** amount, currency, gateway, txn id, provider session, description, customer_email, metadata |
| `payment.failed` | same | M2M checkout failed | Same shape; amount/currency from session row |
| `subscription.activated` | Commerce lifecycle | CaaS | **Enriched vs 001-gaps:** customer_id, email, amount, currency, interval, current_period_end, metadata, is_first_payment |
| `subscription.resumed` | Commerce lifecycle | CaaS | Same builder |
| `subscription.suspended` | Commerce + dunning | CaaS | Same |
| `subscription.canceled` | Commerce + dunning | CaaS | Same |
| `subscription.past_due` | Billing engine | CaaS | Same |
| `order.completed` | OrderCompleted handler | CaaS one-time | Exists (historically thinner; not the P09 subscription builder) |
| `payment_link.paid` | Gateway completed + custom_payment_link | CaaS | Amount/currency/gateway tx (was an explicit skip; now emits) |
| `invoice.valid` | LHDN poller → Dispatch → One | Compliance | internal_id, lhdn_uuid, status, qr_link, error_message |
| `invoice.invalid` | same | Compliance | same |

**Does not fire (but docs mention):**

| Event | Where mentioned | Reality |
|-------|-----------------|---------|
| `invoice.submitted` | Hub catalog (“when emitted”) | Not emitted |
| `invoice.cancelled` | Hub catalog | Not emitted |
| `payment.refunded` | VitePress events.md | Not M2M outbound |
| `subscription.updated` | Explicitly forbidden (P09) | Correctly absent |
| `payment.succeeded` | Old gap docs | Renamed to `payment.completed` |

#### Signing (workspace / One path — the real path)

`OutboundWebhookSignature.cs`:

- Header: `X-Lazuar-Signature: t=<unix>,v1=<hex>`  
- Signed material: `{t}.{rawBody}`  
- HMAC-SHA256, lowercase hex, **full** `whsec_…` string as UTF-8 key (do not strip prefix)  
- Verify helper: 300s skew, fixed-time compare  
- Additional headers: `X-Lazuar-Event`, `X-Lazuar-Delivery-Id`, `X-Lazuar-Webhook-Id`  
- Secrets: minted `whsec_` + token; **encrypted at rest** via `ISecretVault`; lazy-encrypt leftover plaintext; rotate remints immediately (old secret dies — no dual-verify window)

This is **Standard Webhooks–style**, not the official Standard Webhooks library (no `webhook-id` / `webhook-timestamp` / `webhook-signature` header names). Close enough that a careful integrator can copy Stripe-like verify code. Not close enough that `standardwebhooks` npm verifies without an adapter.

Sample verify: `examples/hub-cashier-next/lib/webhook-verify.ts` (unit vectors via `test:webhook`). Hub `/webhooks` and VitePress duplicate the algorithm. **No published `@lazuar/webhooks` package.**

#### Delivery / retries

`WebhookDeliveryOutbox` + `OutboundWebhookDispatcherJob`:

- Status: `PENDING` → `SUCCESS` / `FAILED`  
- Max **5** attempts; backoff `2^AttemptCount` minutes  
- 4xx → **permanent failure** immediately (401/422 policy — do not hide secret bugs in backoff)  
- 5xx / transport → retry  
- Claim: `FOR UPDATE SKIP LOCKED`, batch 50, lease on `NextAttemptAt`  
- Named HttpClient `DeveloperWebhooks`  
- Metrics: `LazuarMetrics.RecordWebhookFailed("outbound")`  
- **No jitter. No multi-day tail. No auto-disable endpoint. No DLQ table beyond FAILED status.**

URL validation (`WebhookUrlValidator`): absolute URL, no userinfo, HTTPS, or HTTP **loopback only**, max 2048. **Not a full SSRF block** (HTTPS to `169.254.169.254` / metadata / RFC1918 still possible).

#### Redrive

**None.** Delivery Logs UI says so explicitly:

> Redeliver / resend is not available yet (API residual).

No `POST /one/workspaces/{id}/webhooks/logs/{deliveryId}/redeliver`. No test ping (`test.ping`). Ops can rotate secret and refresh the log table. That is it.

Logs DTO: id, event_type, status, attempt_count, last_error, created_at. **No HTTP status, no response body, no request body, no endpoint URL, no attempt timeline.** Last 50 (query service). Expandable row is the error string.

#### Dual registry leftover (LHDN)

Still in the tree:

- `POST/GET/DELETE /lhdn/webhooks` → `lhdn.WebhookSubscriptions`  
- List lies about stored `events[]`  
- Dispatch **does not read this table**  
- Migrator exists to copy **legacy** rows into One (`LegacyWebhookSubscriptionMigrator`)  
- New LHDN SDK `registerWebhook` still hits the zombie API  

Integrator-visible conclusion: **configure webhooks in One / Ops**, not via LHDN SDK webhook CRUD, until that façade is deleted or dual-writes.

### G. SDKs

| Package | Path | Version | Generator | Status |
|---------|------|---------|-----------|--------|
| `@lazuar/lhdn-sdk` | `packages/lhdn-sdk-ts` | **0.1.0** | Kiota from `dist/lhdn/openapi.yaml` | Factory prepends `Bearer `; **no** auto Idempotency-Key |
| `Lazuar.Lhdn.Sdk` | `packages/lhdn-sdk-dotnet` | **0.1.0** | Kiota | Factory prepends `Bearer `; **auto** Idempotency-Key on POST |
| `@repo/api-types-ts` | `packages/api-types-ts` | internal | openapi-typescript of **monolith** | Frontends only |
| `Lazuar.ApiContracts` | `packages/api-types-dotnet` | internal | NSwag | Backend DTOs |

**There is no Payments SDK. There is no Commerce SDK. There is no webhook verification SDK.**

Publish: ADR 011 is a **manual** npm/NuGet runbook. No CI publish-on-tag was found in this pass. Hub footer names the packages as if they are the public story; whether they are actually on the public registry is an ops fact this analysis did not verify by hitting npmjs.org.

Kiota preview dependencies on TS (`1.0.0-preview.20`) are a DX smell for a “0.1.0 official SDK.”

`task gen` only runs `gen:sdk-lhdn`. Adding a Payments SDK would be a new Taskfile target + new package under `@lazuar/payments-sdk` (naming doctrine: publishable SDKs are `@lazuar/*`).

The sample cashier **intentionally** uses raw `fetch` and no `@repo/*` — that is the second-app proof, not an SDK.

### H. Quickstarts and sample apps

**Human guides (two sites):**

| Guide | Location |
|-------|----------|
| LHDN first invoice | Hub `/quickstart` |
| Payments cashier | Hub `/payments-cashier` + `docs/payments-integration-quickstart.md` + VitePress `/integrations/payments-cashier` |
| Auth & scopes | Hub `/auth` + VitePress `/integrations/api-keys` |
| Webhooks | Hub `/webhooks` + VitePress `/integrations/webhooks` |
| Provision | VitePress `/integrations/provision` |
| Environments / hops | VitePress `/integrations/environments` |
| Payment flow SSoT | VitePress `/integrations/payment-flow` |
| Second-app checklist | VitePress `/integrations/second-app-checklist` |
| Run sample | VitePress `/integrations/run-sample-app` |
| Error codes | VitePress `/reference/error-codes` |
| Event catalog (short) | VitePress `/reference/events` |
| OpenAPI pointer | VitePress `/reference/openapi` |

**Sample app:** `examples/hub-cashier-next` (`@examples/hub-cashier-next`, port **3020**, `pnpm example:cashier`).

Proves: server-side M2M checkout, redirect, signed webhook unlock, envelope honesty, no gateway SDKs, success_url never unlocks.  
Does not prove: multi-instance store, Commerce, LHDN, production HA.

Includes: `lib/webhook-verify.ts`, `scripts/test-webhook-verify.mjs`, `scripts/send-fake-webhook.mjs` (dev only). File store under `.data/`.

**Not present:**

- Postman collection for **Hub** (repo `docs/postman/` is still **MyInvois government** OAuth — easy to confuse; called out in 001-gaps and still true)  
- Second language sample (Python/PHP/Go)  
- Commerce unlock sample  
- LHDN ERP sample beyond curl on the hub  

### I. Sandbox / test clocks

What exists:

- Key prefix `sk_test_` vs `sk_live_` → claim `IsTestMode`  
- LHDN test keys **skip credit balance + deduction** and flag documents `IsTestMode`  
- Provision `is_test_mode` defaults true → bootstrap `sk_test_`  
- Payments `CreateIntegrationCheckoutCommand` receives `RequestIsTestMode`  
- `KEY_MODE_MISMATCH` (409) if Stripe-shaped **K2** prefix disagrees with K1 test/live  
- Billplz: Hub calls **sandbox host** unless `App:ApiBaseUrl` contains `lazuar.com`. A `sk_live_` against a non-prod Hub still hits Billplz **sandbox**. Documented; still a partition lie versus Stripe  
- Local hop-1 still needs a **public tunnel** (`task tunnel:status` greps ngrok). No `lazuar listen`  
- Fake webhook script on the sample is **not** a sandbox clock; it is a handler unit path  

What does not exist:

- Isolated Stripe-style **sandboxes** (multiple named test worlds)  
- **Test clocks** (advance subscription time for dunning / renewals)  
- Test clock API or Ops UI  
- Magic test card numbers owned by Hub (cards belong to Stripe/Billplz sandboxes)  
- Hub-owned simulated gateway that does not need BYOK  

Honest sentence: **Hub test mode is a key prefix + a few billing skips + whatever sandbox the BYOK processor is pointed at.** It is not a Hub sandbox product.

### J. Idempotency keys on POST

| Surface | Idempotency | Notes |
|---------|-------------|-------|
| `POST /lhdn/documents` | **Required** header `Idempotency-Key` | TypeSpec required; .NET SDK auto-injects GUID; TS SDK does **not** |
| `POST /integrations/payments/checkouts` | **Optional** header or `body.idempotency_key` | Same key + same fingerprint → same session; same key + different body → **409 `IDEMPOTENCY_CONFLICT`**. Unique index `(OrganizationId, IdempotencyKey)` |
| Billing credit deduct | Internal log `(OrganizationId, IdempotencyKey)` | LHDN live submits use this; not a public header on billing admin |
| Commerce public checkout | **No** public Idempotency-Key in TypeSpec | Replay can create duplicate sessions |
| Webhook register / key mint / provision | Provision is idempotent on external identity; key mint is **not** idempotent (new row every POST) | |
| Generic POST middleware | **None** | Not a platform feature |

Stripe-class would be: every create-money or create-object POST accepts `Idempotency-Key`, 24h cache, documented conflict. We have it where it hurts (LHDN submit, M2M checkout) and nowhere else.

### K. Versioning

Written policy exists and is better than 001-gaps claimed:

- Path `/api/v1/…`  
- OpenAPI `1.0.0`  
- Additive fields / new optional scopes / new event types = non-breaking  
- Breaking = new major or ≥90 day deprecation  
- Source of truth: TypeSpec + runtime honesty CI  

Missing vs Stripe:

- No `Lazuar-Version` / `Stripe-Version` request header  
- No per-account pinned version  
- No webhook endpoint API-version pin  
- No public changelog surface on the hub  
- SDK 0.1.0 vs OpenAPI 1.0.0 vs package `api-spec` 1.0.0 — three version numbers  
- Cannot version Payments without versioning the shared host path prefix  

### L. Workbench / CLI

**None as a product.**

Closest internals:

- `task gen`, `task contracts:honesty` — for **us**, not integrators  
- `task tunnel:status` — ngrok helper  
- Sample `send-fake-webhook.mjs` — local only  
- Ops Delivery Logs — shallow  
- Structured API logs for inbound payment webhooks (support SQL path; Phase C notes: no timeline UI)  

No `lazuar` CLI on npm. No `lazuar listen` that forwards Hub events to localhost. No Inspector. No “trigger `payment.completed`” from Ops (test ping residual).

### M. Status page, logs

| Need | Exists? |
|------|---------|
| Public status page (status.lazuar.com) | **No** |
| API request log explorer (Workbench-like) | **No** |
| Per-key last-used / request count | **No** |
| Webhook delivery logs | **Yes**, shallow, Ops only |
| Inbound gateway webhook forensic store | Thin `PaymentWebhookLog` (event id + processed_at); **no raw body** |
| Correlation id API → outbox → commerce | Not a product UI |
| Metrics | Some (`RecordWebhookFailed`); not customer-visible |

Phase C acceptance notes are explicit: support answers “was this payment fulfilled?” with **SQL + structured logs**, not a timeline UI.

### N. Rate limits

- **No** ASP.NET `UseRateLimiter` on product API-key routes  
- Provision endpoint: in-memory token bucket (staging-grade)  
- LHDN **outbound** to MyInvois: in-process token buckets (not inbound Hub limits)  
- Live LHDN submits: billing credits as commercial throttle  

A leaked `sk_live_` can flood checkout creates until the gateway or the process falls over.

---

## Integrator journey

Reconstruct the journey a competent backend engineer actually walks today, then score each step against the Stripe-trained expectation.

### Journey 1 — Payments cashier (the product we are selling to Aura and a second app)

```text
0. Discover that Hub ≠ Commerce ≠ LHDN ≠ Paddle
1. Get a Hub workspace
      a. Human: sign up / be invited → Ops
      b. Platform: POST /one/integrations/workspaces/provision
         (need INTEGRATOR_PROVISION_SECRET — not self-serve from the hub)
2. Human: Ops → Payment settings → paste BYOK (Billplz/Stripe/…)
3. Human: Ops → Developer → API Keys → preset “Payments integrator”
   OR use the bootstrap key from provision (shown once)
4. Store sk_ + whsec_ in a secrets manager
5. Optional: GET /integrations/payments/me  (probe; catch Stripe-key paste)
6. Read VitePress payment-flow + Hub /payments-cashier + Scalar /payments
7. POST /integrations/payments/checkouts with Idempotency-Key
8. Redirect guest to checkout_url (gateway hosted page)
9. Do NOT trust success_url
10. Receive POST payment.completed, verify t=,v1=, unlock domain
11. Replay → must not double-unlock
12. If it fails: Ops Delivery Logs (no resend); check hop 1 tunnel; check BYOK
```

**Where this journey is real:** steps 3–11 are implemented and the sample executes them. Provision is implemented. `/me` is implemented. Signature verify is documented in three places plus a sample library.

**Where this journey is not Stripe-class:**

| Step | Friction |
|------|----------|
| 0 | Two docs sites + Scalar + a repo markdown quickstart. Easy to open LHDN first (landing still says LHDN Primary). |
| 1b | Provision secret is an **ops-shared root credential**, not a developer-dashboard “Create platform.” |
| 2 | BYOK is a **human** step with no Connect onboarding API for processors. CHIP auto-registers inbound webhooks; Stripe/Billplz/Razorpay inbound URLs are documented, not wizarded. |
| 3 | Good UI. No last-used. Prefix looks like Stripe. |
| 6 | OpenAPI checkout is honest; webhook DTO is not. |
| 10 | Must copy-paste verify (no official SDK). LHDN catalog page still mentions a second signing dialect for the zombie path. |
| 12 | No CLI listen, no resend, no request logs, no status page. Local hop 1 still needs ngrok. |

**Time-to-first-webhook (local, Hub already running, BYOK already set):** a careful engineer + the sample can do this in one sitting. **Time-to-first-webhook (greenfield, no one handed you a provision secret):** blocked on Hub ops access. That is the opposite of Stripe’s “sign up, sandbox keys on the home page.”

### Journey 2 — LHDN e-invoice

```text
1. Workspace with LHDN entitlement
2. Human: upload certificate + MyInvois config (OrgAdmin; no Ops UI if invoicing still lobotomized)
3. Mint key with LHDN scopes (or omit scopes and get LHDN default — trap if you meant Payments)
4. POST /lhdn/documents + Idempotency-Key
5. Poll GET /lhdn/documents/{internalId}
6. Optional TIN validate
7. Wait for invoice.valid / invoice.invalid on a **One** webhook endpoint
```

**Works** if the human configured One webhooks. **Fails silently** if they only called `POST /lhdn/webhooks` (SDK-shaped). Certificate UX may be API-only (invoicing module still MVP-hidden per ADR 023). Test keys skip credits — good. SDK exists but 0.1.0 / Kiota preview / TS missing auto idempotency.

### Journey 3 — Commerce unlock/revoke (CaaS)

```text
1. Human creates products in Ops (no M2M product admin in v1 — documented)
2. Public buy link / POST /public/commerce/checkout (no API key)
3. Guest pays via BYOK gateway
4. Workspace webhook: subscription.activated / order.completed / …
5. Unlock in the integrator app; revoke on suspended/canceled
```

Honest v1: **webhooks + public checkout**, not “Stripe Billing API.” Hub `/webhooks` states this. Scalar still dumps admin CRUD. No Commerce SDK. No sample app for this path (cashier sample explicitly excludes Commerce).

### Journey 4 — What an integrator who opens `/docs` first sees

1. LHDN is **Primary**. Payments is **Cashier**. Commerce is **v1**. Ops is Internal.  
2. Top nav on guide pages is LHDN-weighted.  
3. They can complete an LHDN curl without ever learning provision.  
4. They can complete a Payments curl only if someone already minted a key and set BYOK.  
5. They will find three webhook stories (One, LHDN API, inbound gateway) and may register the wrong one.

This is a **curation** problem, not a missing endpoint problem. The code for a Payments-first hub exists; the information architecture still leads with compliance.

### Journey 5 — Aura as first-party consumer (not the general integrator)

Aura Connect: provision with `aura_org_id` / product `aura`, paste K1 into Guest payments, `GET /me`, `POST …/webhooks`, fulfill on `payment.completed`. That path is **live** in Hub and is the consume table in `docs/payments-integration-quickstart.md` §8.

Aura soak (`PY-001`…`PY-008`) is **outside this chapter’s ship list**. Hub can be DX-complete for a second app while Aura production guest pay is still Partial. Do not conflate.

---

## Gap table

Legend: **Y** = Stripe-class / sold. **P** = partial or honesty hole. **N** = absent.  
**Ours** = Hub depth (`shipped` / `partial` / `none`).

### Credentials and auth

| ID | Capability | Stripe | Xendit | Billplz | Paddle | Polar | Hub now | Ours |
|----|------------|:------:|:------:|:-------:|:------:|:-----:|---------|------|
| DX-001 | Secret keys test/live | Y | Y | P (key + sandbox host) | Y | Y | `sk_test_` / `sk_live_`; hashed; hint | shipped |
| DX-002 | Restricted / scoped keys | Y (RAKs) | P | N | P | P | Closed 6-scope catalog; enforced | shipped |
| DX-003 | Publishable vs secret | Y | Y (public tokenize) | N | P | P | N (no pk_; guests hit gateway) | n/a |
| DX-004 | Dashboard create / reveal once / revoke | Y | Y | P | Y | Y | Ops API Keys page | shipped |
| DX-005 | List keys with prefix…hint | Y | Y | N | Y | Y | Yes | shipped |
| DX-006 | Last-used | Y | Y | N | P | P | Not persisted | none |
| DX-007 | Rotate with dual-valid window | Y | P | N | P | P | Mint+revoke only | none |
| DX-008 | Expiry / IP allowlist | Y | P | N | P | P | None | none |
| DX-009 | Machine keys cannot mint keys | Y | Y | — | Y | Y | OrgAdmin-only mint (fixed) | shipped |
| DX-010 | JWT not used for integrations | Y | Y | Y | Y | Y | Documented + true if you follow docs | shipped |
| DX-011 | Connect / provision | Y (deep) | P | N | P | P | K0 provision + webhook + bootstrap key | partial |
| DX-012 | OAuth2 client_credentials | Y | P | N | P | P | None | none |
| DX-013 | Prefix collision hygiene | Y (own prefixes) | Y | Y | Y | Y | Chose Stripe `sk_` prefixes (decision B) | partial |
| DX-014 | Per-app provision secrets | Y (Connect apps) | — | N | — | P | Often one env secret per deploy | partial |
| DX-015 | Introspect key (`/me`) | Y (many GETs) | P | N | P | P | `GET /integrations/payments/me` | shipped |

### Contracts and honesty

| ID | Capability | Stripe | Xendit | Billplz | Paddle | Polar | Hub now | Ours |
|----|------------|:------:|:------:|:-------:|:------:|:-----:|---------|------|
| DX-020 | Product-scoped OpenAPI | Y | Y | N | Y | Y | six docs-*.tsp; commerce+payments wired | shipped |
| DX-021 | OpenAPI path = runtime path | Y | Y | — | Y | Y | R25 CI; allowlist empty of phantoms | shipped |
| DX-022 | Webhook schema = wire body | Y | P | N | Y | Y | Payments DTO flat vs envelope; LHDN events[] fiction | partial |
| DX-023 | Error codes stable | Y | Y | P | Y | Y | Cashier codes yes; LHDN mixed 400/402; key 401 not RFC7807 | partial |
| DX-024 | Versioning policy | Y (pinned) | P | N | Y | P | Written v1 additive; no header pin | partial |
| DX-025 | Changelog / upgrade guide | Y | P | N | Y | P | None on hub | none |
| DX-026 | Internal APIs not sold as public | Y | Y | — | Y | Y | Ops dashed; Billing Admin; Commerce admin still in Scalar | partial |
| DX-027 | Postman / official collection | Y | Y | P | Y | P | MyInvois collection only (wrong product) | none |

### Events and webhooks

| ID | Capability | Stripe | Xendit | Billplz | Paddle | Polar | Hub now | Ours |
|----|------------|:------:|:------:|:-------:|:------:|:-----:|---------|------|
| DX-030 | Multi-endpoint + filters | Y | Y | N | Y | Y | One workspace endpoints + enabled_events | shipped |
| DX-031 | Timestamped signatures | Y | P | N (form HMAC) | Y | Y (Standard Webhooks) | `t=,v1=` HMAC | shipped |
| DX-032 | Official verify helper | Y | Y | N | Y | Y | Snippets + sample only | partial |
| DX-033 | Automatic retries | Y | Y | N | Y | Y | 5 × exponential minutes | partial |
| DX-034 | Delivery log + bodies | Y | P | N | Y | Y | Status + last_error only | partial |
| DX-035 | Redeliver / resend | Y | Y | N | Y | Y | **None** | none |
| DX-036 | Test trigger / CLI listen | Y | P | N | P | P | Fake script on sample only | none |
| DX-037 | Secret rotate | Y | Y | N | Y | Y | Yes; immediate cut | shipped |
| DX-038 | Auto-disable on failure | Y | P | N | P | P | None | none |
| DX-039 | SSRF / HTTPS policy | Y | P | N | P | P | HTTPS + loopback; no private-IP block | partial |
| DX-040 | Unified registry (one table) | Y | Y | — | Y | Y | One dispatcher **plus** LHDN zombie CRUD | partial |
| DX-041 | Money events for M2M | Y | Y | P (callback) | Y | Y | payment.completed / failed | shipped |
| DX-042 | Refund / dispute outbound | Y | P | N | P | P | Not M2M | none |
| DX-043 | LHDN terminal events | — | — | — | — | — | valid/invalid only; catalog oversells | partial |
| DX-044 | Rich payloads | Y | Y | N | Y | Y | Payments rich; subscriptions enriched; some orders thinner | partial |

### SDKs, samples, onboarding

| ID | Capability | Stripe | Xendit | Billplz | Paddle | Polar | Hub now | Ours |
|----|------------|:------:|:------:|:-------:|:------:|:-----:|---------|------|
| DX-050 | Payments / cashier SDK | Y | Y | N | Y | Y | **None** (raw HTTP) | none |
| DX-051 | Commerce SDK | Y (Billing) | — | — | Y | Y | None | none |
| DX-052 | LHDN / tax SDK | Stripe Tax | — | — | — | — | TS + .NET 0.1.0 | partial |
| DX-053 | SDK published on registry | Y | Y | P | Y | Y | Manual runbook; 0.1.0; not CI | partial |
| DX-054 | Quickstart 5 minutes | Y | Y | N | Y | Y | Exists if BYOK+key already there | partial |
| DX-055 | Official sample app | Y | P | N | P | P | `examples/hub-cashier-next` | shipped |
| DX-056 | Second-language samples | Y | Y | N | P | P | None | none |
| DX-057 | Docs IA (one story) | Y | Y | N | Y | Y | Hub + VitePress + repo md | partial |

### Platform primitives

| ID | Capability | Stripe | Xendit | Billplz | Paddle | Polar | Hub now | Ours |
|----|------------|:------:|:------:|:-------:|:------:|:-----:|---------|------|
| DX-060 | Idempotency on money POST | Y | P | N | P | P | LHDN required; checkout optional; not global | partial |
| DX-061 | Isolated sandbox worlds | Y | P (test mode) | P (sandbox host) | Y (sandbox acct) | Y | Prefix + processor sandbox; Billplz env ≠ key | partial |
| DX-062 | Test clocks | Y | N | N | N | N | None | none |
| DX-063 | Workbench / request logs | Y | P | N | P | P | None | none |
| DX-064 | Official CLI | Y | N | N | N | N | None | none |
| DX-065 | Status page | Y | Y | P | Y | P | None | none |
| DX-066 | Inbound rate limits | Y | Y | P | Y | Y | Provision only (in-memory) | none |
| DX-067 | Try-it authenticated | Y | P | N | P | P | Anonymous Scalar | none |

### Highest-leverage honesty bugs (not “missing Stripe features”)

These are **lies or traps in what we already ship**. They outrank Workbench.

| # | Bug | Why it hurts |
|---|-----|----------------|
| H1 | `POST /lhdn/webhooks` does not feed the dispatcher | SDK + quickstart “or register here” is false |
| H2 | `PaymentWebhookPayloadDto` ≠ envelope+data | Generated clients will parse webhooks wrong |
| H3 | Hub catalog lists unemitted invoice/refund events | Integrators wait for events that never come |
| H4 | `sk_` prefix = Stripe merchant secrets | Aura and every new integrator will paste the wrong secret; `/me` is the mitigation |
| H5 | Billplz live/sandbox follows hostname, not `sk_live_` | Test/live story is false for the #1 MY rail |
| H6 | Omit scopes on mint → LHDN default | A Payments engineer who hits the raw API gets an e-invoice key |
| H7 | Two docs sites + LHDN-primary landing | Cashier product is easy to miss |
| H8 | MyInvois Postman in `docs/postman/` | Looks like “our API collection” |
| H9 | Commerce Scalar includes admin CRUD | Contradicts “no M2M admin in v1” |
| H10 | No redeliver | Support cannot replay hop 2; engineers re-pay or SQL |

---

## Tracker IDs

Promotion catalog for a later Lazuar Pay / Hub DX checklist.  
**Family `DX`.** Do not reuse Aura salon IDs (`GB`, `CL`, `PS`, …) for these rows.  
Cross-links to Aura consume rows are notes, not the same ticket.

Schema (compatible with `20-sequencing-and-tracker-schema.md` vocabulary):

- **V** = Ours / Theirs / Both / Partial / Later / Never / N/A  
- **W** = suggested wave (DX-local, not Aura floor waves)  
- **P** = priority inside that wave (0 = first)  
- **Class** = `honesty` (lies in what we ship) · `table-stakes` (SEA cashier bar) · `stripe-class` (Workbench/CLI/clocks) · `later-nice`

Wave meaning for **this family only**:

| W | Intent |
|---|--------|
| 0 | Honesty — stop shipping lies (webhook registry, schemas, catalog, Postman label) |
| 1 | Cashier onboarding — one IA, Payments SDK or official verify package, redeliver, last-used |
| 2 | Key lifecycle — rotate window, last-used, inbound rate limits, provision-secret hygiene |
| 3 | Sandbox honesty — Billplz env tied to key or explicitly labeled in every UI |
| 4 | Stripe-class extras — CLI listen, test ping, request logs, status page, test clocks |
| — | Never / N/A |

### Implement-later queue (sorted by wave then pain)

| ID | Feature | Hub now | V | W | P | Class | Why |
|----|---------|---------|---|--:|--:|-------|-----|
| DX-040 | Kill or dual-write LHDN webhook CRUD | zombie registry | Partial | 0 | 0 | honesty | SDK path is a no-op vs dispatcher |
| DX-022 | Align Payment/Commerce webhook TypeSpec to envelope | DTO lie | Partial | 0 | 0 | honesty | Generated clients will be wrong |
| DX-043 | Catalog only emitted LHDN/refund events | oversell | Partial | 0 | 1 | honesty | `invoice.submitted` / `cancelled` / `payment.refunded` |
| DX-027 | Replace or banner MyInvois Postman | wrong collection | Later | 0 | 2 | honesty | First-hour confusion |
| DX-026 | Gate Commerce admin out of public Scalar (or tab it Console) | mixed | Partial | 0 | 2 | honesty | ADR 007 audience |
| DX-057 | Single docs IA (Payments-first hub nav) | two sites | Partial | 0 | 1 | table-stakes | Landing still LHDN-primary |
| DX-035 | Redeliver delivery by id (API + Ops) | none | Later | 1 | 0 | table-stakes | Support cannot replay hop 2 |
| DX-032 | Publish `@lazuar/webhooks` verify (TS + .NET) | snippets | Partial | 1 | 0 | table-stakes | Every integrator reimplements HMAC |
| DX-050 | Payments cashier SDK (TS first) | none | Later | 1 | 1 | table-stakes | curl-only is Billplz-grade |
| DX-006 | Persist `LastUsedAt` (throttled) | none | Later | 1 | 1 | table-stakes | Cannot hunt leaked/orphaned keys |
| DX-034 | Delivery log HTTP status + body snippet | error string | Partial | 1 | 2 | table-stakes | Debug hop 2 |
| DX-036 | Test ping (`test.ping`) from Ops | none | Later | 1 | 2 | table-stakes | Cheaper than CLI |
| DX-054 | Greenfield 5-minute cashier quickstart (no tribal provision) | blocked on ops | Partial | 1 | 1 | table-stakes | Stripe home-page keys |
| DX-007 | Key rotate + optional dual-valid window | single-cut | Later | 2 | 0 | table-stakes | Rotate without downtime |
| DX-066 | Inbound rate limit per key / tenant | none | Later | 2 | 0 | table-stakes | Abuse / credit burn |
| DX-014 | Per-integrator provision secrets | one root secret | Partial | 2 | 1 | table-stakes | K0 is a shared root today |
| DX-008 | Optional key expiry + IP allowlist | none | Later | 2 | 2 | later-nice | Stripe RAK hygiene |
| DX-061 | Test/live processor env follows key (or UI screams) | Billplz hostname | Partial | 3 | 0 | honesty | `sk_live_` on staging ≠ live Billplz |
| DX-013 | Keep prefix B **and** hard `/me` copy everywhere | documented | Partial | 3 | 1 | honesty | Collision is permanent |
| DX-064 | `lazuar` CLI listen/trigger | none | Later | 4 | 0 | stripe-class | Local hop 2 without sample hacks |
| DX-063 | Request log / mini-workbench | none | Later | 4 | 1 | stripe-class | After logs exist internally |
| DX-065 | Public status page | none | Later | 4 | 2 | stripe-class | Incident comms |
| DX-062 | Test clocks | none | Later | 4 | 3 | stripe-class | Only if Commerce dunning is sold to integrators |
| DX-012 | OAuth2 client_credentials | none | Later | 4 | 3 | later-nice | When third-party *apps* appear |
| DX-003 | Publishable `pk_` | n/a | N/A | — | — | — | No client-side Hub.js; do not invent |
| DX-051 | Commerce M2M admin SDK | out of v1 | Never* | — | — | — | *Until D5 is reopened; public+webhooks is the v1 |

\*Never **for v1**. Reopen if Commerce grows key-authenticated product admin.

### Full DX catalog (stable IDs)

Use these IDs in later trackers. Do not invent a second taxonomy.

#### Credentials — `DX-001`–`DX-019`

| ID | Feature | Hub depth | V | W | P | Class |
|----|---------|-----------|---|--:|--:|-------|
| DX-001 | `sk_test_` / `sk_live_` hashed secrets | shipped | Both | — | — | table-stakes |
| DX-002 | Closed scope catalog + policy enforcement | shipped | Both | — | — | table-stakes |
| DX-003 | Publishable keys | n/a | N/A | — | — | — |
| DX-004 | Ops create / reveal once / revoke | shipped | Both | — | — | table-stakes |
| DX-005 | List with hint / prefix / scopes | shipped | Both | — | — | table-stakes |
| DX-006 | Last-used | none | Later | 1 | 1 | table-stakes |
| DX-007 | Rotate + dual-valid window | none | Later | 2 | 0 | table-stakes |
| DX-008 | Expiry + IP allowlist | none | Later | 2 | 2 | later-nice |
| DX-009 | Machines cannot mint keys / write BYOK | shipped | Ours | — | — | table-stakes |
| DX-010 | JWT forbidden for ERP | shipped | Both | — | — | table-stakes |
| DX-011 | Workspace provision (Connect-lite) | partial | Partial | 2 | 1 | table-stakes |
| DX-012 | OAuth2 M2M | none | Later | 4 | 3 | later-nice |
| DX-013 | `sk_` vs Stripe K2 collision UX | partial | Partial | 3 | 1 | honesty |
| DX-014 | Per-integrator provision secrets | partial | Later | 2 | 1 | table-stakes |
| DX-015 | `GET /integrations/payments/me` | shipped | Both | — | — | table-stakes |
| DX-016 | Distributed key-cache / revoke | partial (memory) | Later | 2 | 2 | table-stakes |
| DX-017 | Key audit log (who minted/revoked) | none | Later | 2 | 2 | later-nice |
| DX-018 | LHDN key façade stays One-backed | shipped | Ours | — | — | hygiene |
| DX-019 | Default-on-omit-scopes ≠ LHDN for cashier APIs | partial | Later | 0 | 1 | honesty |

#### Contracts — `DX-020`–`DX-029`

| ID | Feature | Hub depth | V | W | P | Class |
|----|---------|-----------|---|--:|--:|-------|
| DX-020 | Product-scoped OpenAPI + Scalar | shipped | Both | — | — | table-stakes |
| DX-021 | Path honesty CI | shipped | Ours | — | — | table-stakes |
| DX-022 | Webhook DTO = wire envelope | partial | Partial | 0 | 0 | honesty |
| DX-023 | Stable ProblemDetails codes (409/422/429/502 in spec) | partial | Partial | 0 | 2 | honesty |
| DX-024 | Written versioning + `/api/v1` | partial | Partial | 4 | 2 | table-stakes |
| DX-025 | Public changelog | none | Later | 4 | 2 | later-nice |
| DX-026 | Internal/admin surfaces not in public Scalar | partial | Partial | 0 | 2 | honesty |
| DX-027 | Official Hub Postman / Bruno collection | none | Later | 0 | 2 | honesty |
| DX-028 | Cookie vs Bearer documented in TypeSpec | none | Later | 1 | 3 | later-nice |
| DX-029 | `X-Tenant-Id` modeled on admin ops | none | Later | 1 | 3 | later-nice |

#### Webhooks — `DX-030`–`DX-049`

| ID | Feature | Hub depth | V | W | P | Class |
|----|---------|-----------|---|--:|--:|-------|
| DX-030 | Multi-endpoint + event filters | shipped | Both | — | — | table-stakes |
| DX-031 | `t=,v1=` signatures + extra headers | shipped | Both | — | — | table-stakes |
| DX-032 | Official verify library | partial | Later | 1 | 0 | table-stakes |
| DX-033 | Retries with longer tail + jitter | partial | Later | 4 | 2 | stripe-class |
| DX-034 | Delivery bodies + HTTP status | partial | Later | 1 | 2 | table-stakes |
| DX-035 | Redeliver | none | Later | 1 | 0 | table-stakes |
| DX-036 | Test ping / trigger | none | Later | 1 | 2 | table-stakes |
| DX-037 | Secret rotate (immediate) | shipped | Both | — | — | table-stakes |
| DX-038 | Auto-disable after N failures | none | Later | 4 | 2 | later-nice |
| DX-039 | SSRF private-range block | partial | Later | 2 | 0 | table-stakes |
| DX-040 | Single webhook registry | partial | Partial | 0 | 0 | honesty |
| DX-041 | `payment.completed` / `payment.failed` | shipped | Both | — | — | table-stakes |
| DX-042 | `payment.refunded` outbound | none | Later | 3 | 2 | later-nice |
| DX-043 | Honest LHDN event catalog | partial | Partial | 0 | 1 | honesty |
| DX-044 | Payload enrichment (orders parity) | partial | Partial | 1 | 3 | table-stakes |
| DX-045 | Standard Webhooks header names (optional adapter) | none | Later | 4 | 3 | later-nice |
| DX-046 | Dual-verify window on secret rotate | none | Later | 2 | 2 | later-nice |
| DX-047 | Endpoint-level API version pin | none | Later | 4 | 3 | stripe-class |
| DX-048 | `invoice.submitted` / `invoice.cancelled` | none | Later | 3 | 3 | later-nice |
| DX-049 | Delivery claim already SKIP LOCKED | shipped | Ours | — | — | hygiene |

#### SDKs and samples — `DX-050`–`DX-059`

| ID | Feature | Hub depth | V | W | P | Class |
|----|---------|-----------|---|--:|--:|-------|
| DX-050 | Payments SDK (TS) | none | Later | 1 | 1 | table-stakes |
| DX-051 | Commerce admin SDK | none | Never | — | — | — |
| DX-052 | LHDN SDK polish (idempotency TS, 1.x, CI publish) | partial | Partial | 1 | 2 | table-stakes |
| DX-053 | Registry publish automation | none | Later | 1 | 2 | table-stakes |
| DX-054 | 5-minute cashier quickstart | partial | Partial | 1 | 1 | table-stakes |
| DX-055 | Official sample cashier | shipped | Both | — | — | table-stakes |
| DX-056 | Python/PHP sample | none | Later | 4 | 3 | later-nice |
| DX-057 | Docs IA consolidation | partial | Partial | 0 | 1 | table-stakes |
| DX-058 | Commerce unlock sample | none | Later | 3 | 2 | later-nice |
| DX-059 | HubShell nav includes Payments + Commerce | partial | Later | 0 | 1 | honesty |

#### Platform primitives — `DX-060`–`DX-069`

| ID | Feature | Hub depth | V | W | P | Class |
|----|---------|-----------|---|--:|--:|-------|
| DX-060 | Idempotency on all money POSTs + Commerce checkout | partial | Partial | 2 | 1 | table-stakes |
| DX-061 | Test/live processor partition honesty | partial | Partial | 3 | 0 | honesty |
| DX-062 | Test clocks | none | Later | 4 | 3 | stripe-class |
| DX-063 | Workbench / API request logs | none | Later | 4 | 1 | stripe-class |
| DX-064 | Official CLI | none | Later | 4 | 0 | stripe-class |
| DX-065 | Status page | none | Later | 4 | 2 | stripe-class |
| DX-066 | Inbound rate limits | none | Later | 2 | 0 | table-stakes |
| DX-067 | Authenticated Scalar try-it | none | Later | 4 | 3 | later-nice |
| DX-068 | Request id on every API response | none | Later | 2 | 2 | table-stakes |
| DX-069 | Public incident / version badge on hub | none | Later | 4 | 2 | later-nice |

### Cross-links (do not merge IDs)

| Other ID | Relationship |
|----------|----------------|
| Aura `PY-001`…`PY-008` | Consume / soak of Hub guest pay. Not Hub DX tickets. |
| Aura `PY-007` | Public webhook URL per env — Aura hop-2 reachability. Hub equivalent is VitePress environments + ngrok, not a missing Hub endpoint. |
| Aura `PY-010` | `payment.failed` honesty on **Aura** fulfill. Hub already emits `payment.failed`. |
| Backend checklist B.4 residuals | Map onto `DX-034`, `DX-035`, `DX-036`, `DX-039`, `DX-040`, `DX-043` — prefer DX IDs going forward. |

### Suggested first slice (if a DX wave is opened)

Do **not** start with a CLI or a Payments SDK. Start with honesty:

1. **DX-040** — Make `POST /lhdn/webhooks` write One endpoints **or** delete it from TypeSpec/SDK/quickstart in the same PR.  
2. **DX-022** — Replace `PaymentWebhookPayloadDto` with the real envelope; add Commerce envelope to a docs-only but accurate model.  
3. **DX-043** + **DX-059** + **DX-057** — Catalog and nav match what fires and what we sell (cashier).  
4. **DX-035** + **DX-032** — Redeliver + published verify helper. That pair does more for “feels like Stripe” than a Workbench clone.

Refuse:

- Building `pk_` / Elements because Stripe has them (guests already leave Hub for Billplz/Stripe hosted pages).  
- Commerce M2M admin “to match Stripe Billing” while D5 is webhooks-first.  
- Status page / test clocks before hop-2 redeliver and schema honesty.  
- Treating Aura production soak as a Hub DX miss.

---

## Appendix — file evidence (absolute)

### Developer Hub

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-developers/app/page.tsx`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-developers/app/auth/page.tsx`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-developers/app/quickstart/page.tsx`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-developers/app/payments-cashier/page.tsx`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-developers/app/webhooks/page.tsx`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-developers/app/{lhdn,payments,one,commerce,billing,ops}/route.ts`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-developers/lib/openapi.ts`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-developers/app/components/HubShell.tsx`

### Contracts

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/main.tsp`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/docs-{one,ops,billing,lhdn,commerce,payments}.tsp`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/honesty-allowlist.yaml`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/one/routes.tsp`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/one/models/{api-keys,webhook,provision}.tsp`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/payments/{routes,models}.tsp`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/lhdn/routes.tsp`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/commerce/models/webhooks.tsp`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/api-versioning.md`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/contracts/openapi-vs-minimal-api.md`

### Auth, keys, provision

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/ApiCredential.cs`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/PlatformApiScopes.cs`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Application/Commands/GenerateApiCredentialCommand.cs`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Application/Commands/RevokeApiCredentialCommand.cs`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/ApiCredentialEndpoints.cs`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/IntegrationProvisionEndpoints.cs`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Services/IntegratorProvisionRateLimiter.cs`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/ApiKeysPage.tsx`

### Webhooks

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/TenantWebhookEndpoint.cs`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/WebhookDeliveryOutbox.cs`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/WebhookUrlValidator.cs`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WebhookEndpoints.cs`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookSignature.cs`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookDispatcherJob.cs`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/EventHandlers/OutboundWebhookEventHandlers.cs`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/IntegrationCheckoutGatewayEventsHandler.cs`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Application/Commands/DispatchExternalWebhookCommand.cs`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Application/Commands/WebhookCommands.cs`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Endpoints/AdminWebhookEndpoints.cs`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Application/CommerceWebhookPayload.cs`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/DeveloperSettingsPage.tsx`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/DeliveryLogsPage.tsx`

### Payments integration + sample

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/IntegrationEndpoints.cs`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/payments-integration-quickstart.md`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/examples/hub-cashier-next/`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/integrations/`

### SDKs

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/lhdn-sdk-ts/src/index.ts`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/lhdn-sdk-dotnet/src/LhdnClientFactory.cs`  
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/011-sdk-publishing-runbook.md`

---

## Closing judgment

Lazuar Pay’s integrator surface in August 2026 is **no longer the prototype described in `docs/001-gaps/03`, `04`, and `18`.** Platform keys, scopes, OrgAdmin/machine split, One webhook fan-out, Standard Webhooks–style signatures, provision, `/me`, M2M checkouts, a cashier sample, honesty CI, and two human docs sites are real.

It is also **not Stripe-class**, and it is not done being honest.

Versus **Billplz DIY**, Hub already wins: scoped keys, signed retries, a sample that refuses to unlock on redirect, and a written cashier contract.  
Versus **Xendit / Paddle / Polar**, Hub is a thin cashier + a thin CaaS + a real LHDN gateway — missing SDKs (except LHDN 0.1.0), missing redeliver, missing last-used, missing a single docs home.  
Versus **Stripe**, the gap is the entire second half of the journey: Workbench, CLI, sandboxes, test clocks, request logs, status, version pins, restricted-key editor, official verify/SDK, and a home page that hands you a test key in 30 seconds.

The correct sequence is **honesty → cashier onboarding → key lifecycle → processor test/live truth → Stripe-class toys**. Shipping a CLI on top of a zombie LHDN webhook registry and a webhook DTO that does not match the wire would be performing DX, not building it.
