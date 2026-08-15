# Architecture: who does what

**Audience:** Architects, tech leads, and integrators building a **Payments M2M** (cashier) client.  
**Status:** draft  

Domain objects (orders, bookings, invoices, unlock rules) stay in **your app**. Hub moves money rails and notifies when payment is real. This page is the single home for responsibility matrices **M1–M7**.

## At a glance

| Domain | Owner |
|--------|--------|
| Product catalog / bookings / invoices / unlock rules | **Your app** |
| Hosted payment UI + processor settlement | **Gateway** (merchant account) |
| Adapter selection, BYOK vault, session durability, normalized events | **Hub** |
| Guest UX after pay (“thank you”) | **Your app** (`success_url`) |
| Source of truth for “money received” | **Hub outbound webhook** (after gateway → Hub) |

## Actors

| Actor | Role |
|-------|------|
| **Your app** | Domain pricing, metadata, Bearer `sk_*`, webhook verify + fulfill, guest return UX |
| **Lazuar Hub** | Workspace isolation, BYOK vault, checkout session, gateway adapters, Hop-1 verify, Hop-2 signed `payment.*` |
| **Gateway** | Hosted pay page, settlement, provider webhooks (Billplz / Stripe / CHIP / Razorpay) |
| **Human OrgAdmin** | Configure gateway secrets in Ops; mint/revoke keys when needed |
| **Guest** | Pays on gateway page only — never holds Hub machine keys |

### Teaching callouts

- Your app verifies **Hub** signatures (`X-Lazuar-Signature`), not Billplz/Stripe processor signatures.
- Hub is **not** Merchant of Record for guest GMV under BYOK — settlement stays on the merchant processor account.
- Docs use placeholders only (`sk_test_…`, `whsec_…`) — never real secrets.

---

### M1 — Who does what: create payment

| Step | Your app | Lazuar Hub | Gateway |
|------|----------|------------|---------|
| Choose amount / currency | ✅ Domain pricing | Validates amount & currency rules | Enforces processor minimums |
| Attach domain ids | ✅ Opaque `metadata` (`order_id`, …) | Stores/stamps `checkout_id`, `hub_workspace_id`, `hub_checkout_kind` | May strip metadata (e.g. Billplz) — Hub session survives |
| Authenticate | ✅ `Authorization: Bearer sk_*` | Resolves workspace from key; enforces scopes | — |
| Idempotency | ✅ Send `Idempotency-Key` (or body) | Fingerprint + replay / 409 conflict | — |
| Hosted page | Redirect guest to `checkout_url` | Calls adapter `GenerateAsync` | Hosts pay UI |
| Return URLs | ✅ Absolute `success_url` / `cancel_url` | Passes to gateway | Redirects guest after pay |
| Mark paid | ❌ Not on redirect | Session → completed on **inbound** money event | Reports paid/failed |
| Fulfill domain | ✅ After signed outbound webhook | Emits `payment.completed` / `payment.failed` | — |
| Poll status | Optional GET checkout | Returns session status | — |
| Store gateway API keys | ❌ | ✅ BYOK vault (`TenantPaymentConfiguration`) | Issues keys to merchant |

**Scopes:** create requires `payments.checkouts:write`; poll requires `payments.checkouts:read` (write implies read in product policy).

**Sample app implements:** metadata, Bearer key, Idempotency-Key, redirect, optional poll, **never** gateway keys.

See also: [Create a checkout](/integrations/create-checkout).

---

### M2 — Who does what: webhook hops

| Concern | Your app | Lazuar Hub | Gateway |
|---------|----------|------------|---------|
| **Hop 1 URL** public | — | ✅ `App:ApiBaseUrl` must reach Hub from internet | POSTs provider webhook/callback |
| **Hop 1 signature** | — | ✅ Verify provider-specific signature | Signs as provider docs require |
| **Hop 1 idempotency** | — | ✅ `PaymentWebhookLog` / business keys | May retry |
| Map provider → session | — | ✅ `IntegrationCheckoutSession` by `checkout_id` or provider session id | — |
| **Hop 2 URL** | ✅ Register HTTPS (or tunnel) endpoint | Stores `TenantWebhookEndpoints` | — |
| **Hop 2 signature** | ✅ Verify `X-Lazuar-Signature` | ✅ `OutboundWebhookSignature` HMAC | — |
| **Hop 2 headers** | Read Event / Delivery-Id / Webhook-Id | Sets all four `X-Lazuar-*` headers | — |
| Raw body | ✅ Buffer unmodified bytes | Signs exact payload string | — |
| ACK / retry | Return 2xx to stop retries | Outbox retries on non-2xx | Provider retries Hop 1 |
| Fulfillment | ✅ Idempotent unlock | Delivers at-least-once | — |
| Trust browser `success_url` | ❌ Never as sole signal | Does not treat redirect as paid | Redirect is UX only |

**Headers (Hop 2) — runtime:**

| Header | Set by Hub |
|--------|------------|
| `X-Lazuar-Signature` | `t={unix},v1={hmac_hex}` |
| `X-Lazuar-Event` | e.g. `payment.completed` |
| `X-Lazuar-Delivery-Id` | Outbox delivery id |
| `X-Lazuar-Webhook-Id` | Endpoint id |

See also: [Webhooks](/integrations/webhooks).

---

### M3 — Who holds which secrets

| Secret | Format / prefix | Your app | Hub | Gateway portal | Notes |
|--------|-----------------|----------|-----|----------------|-------|
| Provision secret | env `INTEGRATOR_PROVISION_SECRET` | Optional (bootstrap only) | ✅ Config | — | Header `X-Lazuar-Provision-Key` |
| Machine API key | `sk_test_` / `sk_live_` | ✅ Server env only | Hashes / stores metadata | — | Reveal once |
| Webhook signing secret | `whsec_…` | ✅ Server env only | Stores on endpoint | — | Reveal once on create/provision |
| Billplz Collection + X-Signature secret | provider-specific | ❌ if Hub is cashier | ✅ BYOK encrypted | Merchant account | Ops UI configure |
| Stripe Secret Key | `sk_…` Stripe | ❌ if Hub is cashier | ✅ BYOK | Merchant account | Not Hub `sk_` |
| Human Ops JWT / cookie | session | — | AuthN human | — | BYOK write is human |
| Guest session | cookies of your app | ✅ your session | No Hub guest login for M2M | Gateway page session | |

**Never log** full `sk_` or `whsec_`. Prefer last-4 / prefix only in UI.

**Rotation (today):** mint new machine key → deploy → revoke old. Dual-key window not productized. Webhook secret rotation = new endpoint or product rotate flow (document carefully; avoid silent remint on provision re-call).

See also: [API keys & scopes](/integrations/api-keys).

---

### M4 — Multi-tenant & BYOK

| Concern | Your app | Hub | Gateway |
|---------|----------|-----|---------|
| Tenant identity in *your* product | ✅ `external_org_id` stable string | Binds workspace on `(external_product, external_org_id)` | — |
| Product slug | ✅ `external_product` (not only `aura`) | Stored on Organization | — |
| Workspace isolation | Map 1 tenant → 1 workspace | Tenant filters on payments/one data | Separate merchant accounts per business as you configure |
| API key binding | Store key per tenant | Key → single workspace | — |
| Gateway credentials | Not shared across tenants in app | Per-workspace `TenantPaymentConfiguration` | Per merchant account |
| Soft-disable gateway | — | `IsActive=false` blocks new checkouts | Keys retained |
| Cross-tenant checkout | Must not reuse another tenant’s sk_ | Rejects wrong tenant session ids | — |
| Webhook endpoint filter | One URL can serve multi-tenant if you route by metadata | Fan-out all matching workspace endpoints | — |
| Test vs live | Separate env files | `is_test_mode` → sk_test_ bootstrap; live keys separate | Sandbox vs live credentials |

**Sample app simplification:** single-tenant demo (one sk_ + one whsec_ in `.env`). Multi-tenant mapping is **documented** but not required in MVP sample.

See also: [Concepts](/guide/concepts), [Provision a workspace](/integrations/provision), [Environments](/integrations/environments).

---

### M5 — Errors & retries

| Situation | HTTP / code | Your app should | Hub does | Gateway |
|-----------|-------------|-----------------|----------|---------|
| Missing/invalid sk_ | 401 `UNAUTHORIZED` | Fix key; no retry storm | Reject | — |
| Missing scope | 403 `FORBIDDEN` | Mint key with correct scopes | Reject | — |
| No BYOK | 422 `PAYMENTS_NOT_CONFIGURED` | Prompt human Ops setup | Reject before half-open if possible | — |
| Bad amount | 400 `AMOUNT_*` | Fix request | Validate | May also reject |
| Bad URLs | 400 `URLS_REQUIRED` | Absolute http(s) only | Validate | — |
| Idempotency conflict | 409 `IDEMPOTENCY_CONFLICT` | New key or same body | Detect fingerprint mismatch | — |
| Provider create fail | 502 `GATEWAY_ERROR` | Retry with backoff + new/same idempotency policy | Mark session failed; surface detail | Upstream error |
| Checkout not found | 404 | Wrong id or tenant | Tenant-scoped get | — |
| Bad webhook signature | 401 from **your** app | Fix whsec_; alert | Does **not** retry; operator redrives from Delivery Logs | — |
| Your handler 5xx | — | Fix crash; keep idempotent | Retry outbox with backoff | — |
| Duplicate webhook | 200 + no-op | Dedupe event_id / delivery_id / checkout_id | At-least-once delivery | Provider at-least-once |
| Browser success only | — | Show “processing…”; wait webhook | — | Redirect |

Stable codes: see [Error codes](/reference/error-codes).

---

### M6 — Billplz vs Stripe vs Hub (responsibility)

| Capability | If DIY Billplz in your app | If DIY Stripe in your app | With Hub cashier |
|------------|----------------------------|---------------------------|------------------|
| Create payment intent/bill | You call Billplz API | You call Stripe API | You call **one** Hub API |
| Signature verify | Billplz X-Signature rules | Stripe-Signature / whsec Stripe | **One** Hub algorithm for all gateways |
| Metadata quirks | You handle Billplz stripping | You handle PaymentIntent metadata limits | Hub `IntegrationCheckoutSession` + stamped metadata |
| Multi-gateway | You build adapters | You build adapters | Hub adapters (STRIPE, BILLPLZ, CHIP, RAZORPAY) |
| Merchant settlement | Merchant Billplz account | Merchant Stripe account | **Same** (BYOK) — Hub not MoR for GMV |
| Credential storage | Your vault | Your vault | Hub vault per workspace |
| Ops UI for keys | You build | You build | Hub Ops payment settings |
| Outbound to *your* domain | You map provider events → domain | Same | Hub normalizes `payment.completed|failed` |
| SaaS seat billing (platform fee) | Separate product | Separate product | **Still separate** (e.g. Paddle) — not this path |

**Docs rule:** this comparison only. Do **not** treat DIY Billplz/Stripe as a supported primary integration path in Hub guides. For a short decision page, see [Hub vs DIY gateways](/integrations/hub-vs-diy).

---

### M7 — Anti-patterns (do not)

| Anti-pattern | Why it fails | Do instead |
|--------------|--------------|------------|
| Put `sk_*` in Next.js client bundle / `NEXT_PUBLIC_*` | Key theft = free checkout creation on your merchant rails | Server-only env; Route Handlers |
| Trust `success_url` query as paid | User can open URL without paying | Unlock only after verified `payment.completed` |
| Verify webhook after `request.json()` without raw body | HMAC mismatch; false 401s | `request.text()` / raw buffer first |
| Re-implement Billplz signature in app while using Hub | Double complexity; wrong hop | Only Hub `X-Lazuar-Signature` |
| Grant machine key payment-config write | Key leak rewrites BYOK | Human OrgAdmin for gateway secrets |
| Mix Commerce events with M2M checkouts | Wrong fulfillment model | `payment.*` only for cashier |
| Share one sk_ across all tenants | Cross-tenant data access | Per-workspace keys |
| Log full whsec_ / sk_ | Secret sprawl | Prefix + last4 |
| Assume provision re-call returns plain_key | Idempotent null | Store on first success only |
| Use relative success/cancel URLs | 400 URLS_REQUIRED | Absolute http(s) |
| Skip Idempotency-Key on flaky networks | Double gateway sessions | Always send stable key per order |
| Expect email/Resend config for M2M | Commerce gate only | Configure BYOK, not email, for cashier |
| Treat Aura dual-run as required architecture | Legacy insurance | New apps: Hub-only |

---

## Related

| Topic | Guide |
|-------|--------|
| Product boundaries | [Product lines](/guide/product-lines) |
| Core vocabulary | [Concepts](/guide/concepts) |
| Cashier overview | [Payments cashier](/integrations/payments-cashier) |
| Hub vs DIY (condensed) | [Hub vs DIY gateways](/integrations/hub-vs-diy) |
| Create checkout | [Create a checkout](/integrations/create-checkout) |
| Webhooks | [Webhooks](/integrations/webhooks) |
| Keys & scopes | [API keys & scopes](/integrations/api-keys) |
| Multi-app proof | [Second-app checklist](/integrations/second-app-checklist) |
| Aura pattern | [Aura as a reference client](/integrations/aura-reference) |

Run-the-sample guide: coming in a later slice (S50).
