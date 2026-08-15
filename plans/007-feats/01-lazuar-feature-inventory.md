# 01 — Lazuar Pay feature inventory (ground truth)

**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Inventory date:** 2026-08-16  
**Product:** Lazuar Pay / Lazuar Hub — Checkout-as-a-Service / Compliance CaaS (not Aura salon software). Public production host is `https://hub.lazuar.com`. Aura uses this repo as its payment hub.  
**Method:** live tree walk of `apps/`, `packages/`, `docs/`, `examples/`, `plans/`, plus reading of ADRs 019/021/022/023, gap reports (dated 2026-08-03), maintenance decisions (2026-08-09), Phase C close-out (2026-08-04), TypeSpec, module endpoints, workers, and merchant/buyer/admin UIs. Earlier gap reports are **historical snapshots**. This file describes **what exists in the tree today**.

**Honesty rule used here:** a capability is **SHIPPED** only when a merchant or buyer (or a documented integrator with a key) can complete the loop without internal SQL, unrouted pages, or “console-log WhatsApp.” Backend code that is real but unrouted, hidden by `[MVP-HIDE]`, or parked is **BACKEND-ONLY** or **PARTIAL**. Types/tables/comments without a working loop are **SCAFFOLD**. Roadmap names with no adapter are **ABSENT**.

---

## Scope and method

### What was opened

| Surface | Path | Role |
|---------|------|------|
| Merchant console | `apps/lazuar-ops/src/` | Vite CSR at `:3003` / prod `/` |
| Hosted checkout + buyer portal | `apps/lazuar-portal/src/` | Next.js SSR at `:3004` / prod `/portal` |
| Platform control plane | `apps/lazuar-admin/src/` | Vite CSR at `:3005` / prod `/admin` |
| Scalar OpenAPI hub | `apps/lazuar-developers/` | Next.js at `:3002` / prod `/docs` |
| Product guides | `apps/lazuar-docs/docs/` | VitePress |
| API monolith | `apps/lazuar-api/` | .NET 10 modular monolith |
| TypeSpec SSoT | `packages/api-spec/` | Generated clients in `packages/api-types-ts` and `packages/api-types-dotnet` |
| LHDN SDKs | `packages/lhdn-sdk-ts/`, `packages/lhdn-sdk-dotnet/` | Kiota-generated document/TIN/key clients |
| Integrator sample | `examples/hub-cashier-next/` | Next.js on `:3020` |
| ADRs | `docs/architecture-decision-log/` | Especially 019, 021, 022, 023 |
| Gap analyses | `docs/001-gaps/` | 2026-08-03 uncondensed reports; still useful as *then*, not *now* |
| Maintenance lock | `plans/004-maintenance/decisions.md` | WhatsApp freeze, deferred revenue park, key SSoT |
| Remaining work | `plans/005-remaining/` | Key cutover, LHDN webhook converge |

### Modules registered in the host (today)

`apps/lazuar-api/src/Lazuar.Api/Composition/ModuleRegistrationExtensions.cs` registers **nine** product modules and maps their HTTP:

1. One  
2. Messaging  
3. CRM  
4. Payments  
5. Ops  
6. Billing  
7. Lhdn  
8. Commerce  
9. Communications  

Community and Vault **are not registered**. They are not present under `apps/lazuar-api/Modules/`. ADR 022’s “Phase 1 hide” has been followed by actual module deletion from the API composition. Residual frontend orphans remain (portal `modules/community/`, ops `filterHiddenFulfillmentTargets`).

### Status vocabulary

| Status | Meaning in this inventory |
|--------|---------------------------|
| **SHIPPED** | Closed loop: UI (or documented M2M) + backend + persistence + a buyer/merchant/integrator can finish the job. Honest to sell with the caveats listed. |
| **PARTIAL** | Real code on more than one layer, but the loop is incomplete, dishonest in marketing, or gated (hidden route, console-only channel, gateway-specific hole). |
| **BACKEND-ONLY** | Endpoints/workers/domain exist; no merchant/buyer surface, or surface is `[MVP-HIDE]` / unrouted. |
| **SCAFFOLD** | Entity, table, comment, or job body exists but is unregistered, unused, or cannot run a real path. |
| **ABSENT** | Named in README/ADR/roadmap; no implementation. |

### Important dating caveat

`docs/001-gaps/*` (2026-08-03) described a weaker product: LHDN-only API keys, silent outbound URL match, WhatsApp claimed as native, developers hub as Scalar dump, vaulted failures not entering PAST_DUE. **Those P0s were subsequently worked.** Live evidence now includes:

- Platform API keys in One (`ApiCredentialEndpoints.cs`, `one.ApiCredentials`)
- Scoped policies in `AuthAndCorsExtensions.cs`
- M2M cashier at `/integrations/payments/checkouts` (`IntegrationEndpoints.cs`)
- Failed-payment → PAST_DUE handler (`GatewayPaymentFailedIntegrationEventHandler.cs`)
- Multi-endpoint outbound webhooks + HMAC (`WebhookEndpoints.cs`, `OutboundWebhookDispatcherJob.cs`)
- Ops Developer nav: API Keys, Outbound Webhooks, Delivery Logs (`Sidebar.tsx`)
- `Messaging:WhatsAppEnabled` default `false` (`appsettings.json`)
- LHDN lifecycle customer webhooks routed through One dispatcher (Lhdn README §5)

This inventory **does not** treat August-3 gap reports as current truth. It cites them only when the live tree still matches.

---

## Product identity and non-goals (ADRs)

### What Lazuar Pay is

Root `README.md` and ADR 019/021 define the product as:

- **Checkout-as-a-Service / Compliance CaaS**, not a 15-app creator suite.
- **BYOK, not Merchant of Record.** Creators plug Stripe / Billplz / CHIP / Razorpay keys. Money lands in the merchant’s gateway account. Lazuar does not hold settlement funds.
- **Headless.** Landing pages stay on Framer/Webflow/Next. Lazuar powers the Buy button, ledger, dunning, and (backend) LHDN.
- **Utility wallet monetization.** LHDN submits and (future) WhatsApp deduct prepaid `TenantCreditBalance` credits. Checkout GMV is not taxed by Lazuar.
- **Aura hop.** Same engine is Hub for Aura (`hub.lazuar.com`). Provision secret + M2M checkouts + `payment.completed` webhooks.

ADR 019 (`docs/architecture-decision-log/019-checkout-as-a-service-pivot.md`): abandon website builders; become the cash register.

ADR 021 (`docs/architecture-decision-log/021-compliance-caas-pivot.md`): three pillars (B2C consolidation, B2B TIN+invoice, cross-border zero-rate). Kill community DRM, vault hosting, giveaways, link-in-bio. Keep WhatsApp dunning and Xero as “keep” items. **Xero is still ABSENT. WhatsApp is frozen (decision 00.4).**

ADR 022 (`docs/architecture-decision-log/022-remove-community-vault-modules.md`): hide then delete Community/Vault. Phase 1 hide is done; API modules are gone from `AddAllModules`. Frontend still filters `internal:community` / `internal:vault` in `apps/lazuar-ops/src/lib/utils.ts`. Portal still contains leftover `modules/community/` files.

ADR 023 (`docs/architecture-decision-log/023-pure-caas-mvp-ui-lobotomy.md`): hide B2B/LHDN UX with `// [MVP-HIDE]`. Backend stays. Ops App.tsx comments list the floating islands: invoicing pages, billing profile, ops AI chat. Portal quote route `notFound()`s. Checkout TIN fields stripped. Tax-invoice download stripped from buyer portal.

README watermark (must be treated as product truth, not marketing):

> Honest capability today: BYOK gateways + commerce subscriptions + double-entry billing ledger + email dunning templates + LHDN **backend** pipeline. WhatsApp dunning and full compliance UI are roadmap (Phase D), not guaranteed demoable surfaces on every deploy.

### Explicit non-goals (do not inventory as shipping)

From ADR 021 kill list + `plans/004-maintenance/decisions.md` + README Phase 2/3:

- Website / funnel / link-in-bio builders  
- Community Telegram/Discord bouncers, Vault DRM / R2 course files as a product  
- Viral giveaways  
- Fiuu, SenangPay, Xendit, Midtrans, Cashfree (named in ADR 020, **no adapters**)  
- India GSTN, Indonesia Coretax, Singapore InvoiceNow  
- Xero / QuickBooks sync  
- Escrow.com, DocuSign/PandaDoc, Keygen, Wise MassPay, Capchase BNPL, BTCPay/Web3, Singpass  
- Multi-replica workers (deploy docs: keep `hub-api` replica=1)

---

## Surface map (apps, modules, routes)

### Runtime topology

| App | Local | Prod path (Caddy) | Auth |
|-----|-------|-------------------|------|
| `lazuar-api` | `:8080` (Aura hop A uses `:8090`) | `/api/*`, `/health` | JWT cookies + `sk_live_`/`sk_test_` |
| `lazuar-ops` | `:3003` | `/` | `lazuar_auth` cookie → `/one/auth/*` |
| `lazuar-portal` | `:3004` | `/portal*` | Magic link token; optional `/one/auth/me` |
| `lazuar-developers` | `:3002` | `/docs*` | Public Scalar + guides |
| `lazuar-admin` | `:3005` | `/admin/` | `lazuar_admin_auth` cookie → `/platform/auth/*` |
| `hub-cashier-next` | `:3020` | not in prod Caddy | Server-side `sk_` + `whsec_` |
| Optional Caddy | `:9080` | path-mirrors prod | — |

Evidence: root `README.md` port table; `deploy/dev/README.md`; `TODO.md` (`https://hub.lazuar.com`).

### Merchant console — live nav

`apps/lazuar-ops/src/components/Sidebar.tsx` + `apps/lazuar-ops/src/App.tsx`:

**Commerce**

| Label | Route | Page file |
|-------|-------|-----------|
| Dashboard | `/commerce/dashboard` | `modules/commerce/pages/DashboardPage.tsx` |
| Checkout Links | `/commerce/products` | `modules/commerce/pages/ProductsPage.tsx` |
| Subscribers | `/commerce/subscribers` | `modules/commerce/pages/SubscribersPage.tsx` |
| Transaction Logs | `/commerce/transactions` | `modules/commerce/pages/TransactionsPage.tsx` |
| Promotions | `/commerce/coupons` | `modules/commerce/pages/CouponsPage.tsx` |
| Dunning Campaigns | `/commerce/dunning-campaigns` (+ `/new`, `/:id`) | `DunningCampaignsPage.tsx`, `CampaignBuilderPage.tsx` |
| Notification Templates | `/commerce/templates` | `modules/commerce/pages/TemplatesPage.tsx` |

**Developer**

| Label | Route | Page file |
|-------|-------|-----------|
| API Keys | `/developer/api-keys` | `modules/workspace/pages/ApiKeysPage.tsx` |
| Outbound Webhooks | `/developer/webhooks` | `modules/workspace/pages/DeveloperSettingsPage.tsx` |
| Delivery Logs | `/developer/logs` | `modules/workspace/pages/DeliveryLogsPage.tsx` |

**Workspace**

| Label | Route | Page file |
|-------|-------|-----------|
| General Settings | `/workspace/general` | `modules/workspace/pages/GeneralSettingsPage.tsx` |
| Payment Gateways | `/workspace/payment-gateways` | `modules/workspace/pages/PaymentSettingsPage.tsx` |
| Email Provider | `/workspace/email` | `modules/workspace/pages/EmailSettingsPage.tsx` |

**Routed but not in the sidebar** (reachable only by URL):

- `/workspace/billing` → `BillingSettingsPage.tsx` (credit wallet + top-up)
- `/workspace/ledger` → `UtilityLedgerPage.tsx` (credit history)

**Unrouted `[MVP-HIDE]` floating islands** (`App.tsx` comment block):

- `modules/invoicing/pages/QuotesPage.tsx`
- `modules/invoicing/pages/TaxInvoicesPage.tsx`
- `modules/invoicing/pages/CreditNotesPage.tsx`
- `modules/workspace/pages/BillingProfilePage.tsx`
- `components/OpsChatWorkspace.tsx` + `ConversationsDirectory.tsx`

### Buyer portal — live routes

`apps/lazuar-portal/src/app/`:

| Route | File | Status |
|-------|------|--------|
| `/` | `app/page.tsx` | Static “use your magic link” landing |
| `/{tenantSlug}/checkout/{productSlug}` | `checkout/[productSlug]/page.tsx` | Live hosted checkout |
| `/{tenantSlug}/checkout/{productSlug}/success` | `success/page.tsx` | Success / poll |
| `/{tenantSlug}/portal` | `portal/page.tsx` | Magic-link subscription dashboard + cancel |
| `/{tenantSlug}/update-payment/{subId}` | `update-payment/[subId]/page.tsx` | Arrears / update card |
| `/{tenantSlug}/pay/{sessionId}` | `pay/[sessionId]/page.tsx` | **`notFound()` — MVP-HIDE quotes** |
| `/legal/privacy`, `/legal/terms`, `/legal/refund` | `app/legal/*` | Static legal copy |

Leftover: `modules/community/` still on disk; not mounted as a buyer route that ships Community.

### Platform admin

`apps/lazuar-admin/src/App.tsx` + `components/Sidebar.tsx`:

- Login → `/platform/auth/*`
- Single live page: `/platform/gateways` → `modules/platform/pages/PlatformPaymentSettingsPage.tsx` (system-tenant payment config via `GET/PUT /platform/payment-config`)
- No tenant list UI, no credit grant UI, no LHDN cert UI, no user admin UI

### Developer hub (Scalar + guides)

`apps/lazuar-developers/app/page.tsx`:

- Guides: `/quickstart` (LHDN), `/payments-cashier`, `/auth`, `/webhooks`
- API refs: `/lhdn`, `/payments`, `/one`, `/commerce`, `/billing`, `/ops` (ops marked Internal)

VitePress `apps/lazuar-docs/docs/` is a **second** docs surface (integrator narrative: cashier vs Commerce vs LHDN, Aura hop, sample app). Local preview `:5180`.

### API prefix

All product HTTP is under `/api/v1` (`MapAllModuleEndpoints`). Inbound gateway webhooks: `POST /api/v1/webhooks/payments/{gatewayType}/{tenantId}`. Platform group: `/api/v1/platform`. Health: `/health`, `/health/ready`, `/health/metrics` (not in TypeSpec; allowlisted).

### TypeSpec product trees

`packages/api-spec/main.tsp` imports One, Messaging (models only), Ops, Commerce, Communications, Billing, Lhdn, Payments, CRM (models), Platform. Community/Vault TypeSpec **not imported**. Honesty allowlist: `packages/api-spec/honesty-allowlist.yaml` (impl-only: inbound payment webhooks, messaging notify/logs, unsubscribe HTML, Resend webhook, template cleanup, signed final PDF redirect).

---

## Feature-by-feature inventory (tables with Status | Evidence | Honesty notes)

### 1. Identity, workspaces, multi-tenant, auth (JWT vs API keys)

| Capability | Status | Evidence | Honesty notes |
|------------|--------|----------|---------------|
| Global user + password login (ops) | **SHIPPED** | `Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs` `POST /one/auth/login`; cookie `lazuar_auth`; ops `LoginPage.tsx` | HttpOnly JWT via `AuthAndCorsExtensions.cs`. Dual cookie realm: ops `lazuar_auth`, admin `lazuar_admin_auth`. |
| Public self-serve register + first workspace | **SHIPPED** | `POST /one/public/register` → `RegisterPublicUserCommand`; ops `LoginPage.tsx` signup mode | Creates user + workspace + slug validation (`OrganizationSlugMustBeValidRule`). |
| Session `/me` + logout | **SHIPPED** | `GET/POST /one/auth/me`, `/one/auth/logout`; ops `App.tsx` `verifySession` | Security stamp invalidates cookie on password change. |
| Platform superadmin login | **SHIPPED** | `PlatformAuthEndpoints.cs` `/platform/auth/login|logout|me`; admin `LoginPage.tsx` | Separate cookie path `/api/v1/platform`. Seeded in Development (`README.md`: `admin@lazuar.com`). |
| Superadmin can operate any workspace in ops | **SHIPPED** | `WorkspaceEndpoints.cs` `/me/entitlements` returns all active orgs for `IsSystemAdmin` | Ops requires ≥1 entitlement. |
| Email verification + resend | **BACKEND-ONLY** | `POST /one/auth/verify-email`, `/one/auth/resend-verification` | No ops/portal UI for verify flow. |
| Forgot / reset password | **BACKEND-ONLY** | `POST /one/auth/forgot-password`, `/one/auth/reset-password` | No “Forgot password” on ops `LoginPage.tsx`. |
| Profile name + password change | **BACKEND-ONLY** | `ProfileEndpoints.cs` `PUT /one/me/profile`, `/one/me/security/password` | No account-settings page in ops sidebar. |
| Multi-tenant organizations (workspaces) | **SHIPPED** | `One/Domain/Organization.cs`, `TenantMembership.cs`; create/update/archive in `WorkspaceEndpoints.cs` | Tenant bound by `X-Tenant-Id` / slug (`TenantSecurityMiddleware.cs`). |
| Workspace switcher in ops | **SHIPPED** | `App.tsx` `ops_active_workspace_id` + entitlements | Switching navigates to dashboard. |
| Workspace name/slug edit | **SHIPPED** | `GeneralSettingsPage.tsx` → `PUT /one/workspaces/{id}` | Warns slug change breaks public links. |
| Workspace member list / invite / accept / remove | **BACKEND-ONLY** | `WorkspaceEndpoints.cs` members + invites | No ops Members UI. Invites exist as domain events (`WorkspaceInvitationCreatedDomainEvent`). |
| App entitlements toggle (COMMERCE etc.) | **BACKEND-ONLY** | `GET/POST /one/workspaces/{id}/apps` OrgAdmin + system admin | Superapp leftover; Community/Vault apps no longer exist. |
| Tenant isolation (HTTP fail-closed) | **SHIPPED** (hardened) | `TenantSecurityMiddleware.cs`; tests `CrossTenantIdorTests.cs`, `TenantIsolationHardeningTests.cs` | Missing `X-Tenant-Id` on tenant routes → 400. Membership mismatch → 403. API keys bind TenantId in middleware. August-3 “fail-open” finding is **stale** relative to current middleware. |
| Human JWT vs machine API keys | **SHIPPED** | JWT: `AuthAndCorsExtensions.cs`. Keys: `ApiKeyAuthenticationMiddleware.cs` One-only lookup on `one.ApiCredentials` | Keys: `Authorization: Bearer sk_live_|sk_test_`. Role `API_CLIENT` + `scope` claims. Humans cannot pass `IntegrationPaymentsMe`. |
| Platform API key mint/list/revoke in ops | **SHIPPED** | `ApiCredentialEndpoints.cs`; `ApiKeysPage.tsx`; scopes in `PlatformApiScopes.cs` | Create-once reveal. Presets: LHDN docs, Payments integrator. OrgAdmin only (never API_CLIENT). |
| LHDN `/lhdn/api-keys` | **SHIPPED** as façade | `AdminApiKeyEndpoints.cs` delegates to `IApiCredentialService` | Does not write `lhdn.DeveloperApiKeys`. Dual-read closed (R05). Residual Lhdn-only keys 401. |
| Legacy Lhdn key table | **SCAFFOLD / dual-read closed** | `Program.cs` optional `LegacyApiKeyMigrationHostedService`; Lhdn README §6 | Table may still exist; not a product surface. Drop is R06. |
| Integrator provision (Aura hop) | **SHIPPED** | `IntegrationProvisionEndpoints.cs` `POST /one/integrations/workspaces/provision` | Auth: `X-Lazuar-Provision-Key` / Bearer provision secret or SUPER_ADMIN. Rate-limited. Returns workspace + `sk_` + webhook secret once. |

**Honesty:** Identity for humans is a real product. Machine identity is a real product (this is the biggest delta vs August-3 gaps). What is **not** sold as complete: password reset UX, member invites UX, email-verify UX, multi-user RBAC beyond ADMIN membership.

---

### 2. Payment gateways: claimed vs implemented

Allowed inbound types (`Payments/Infrastructure/Endpoints.cs`): `STRIPE`, `BILLPLZ`, `RAZORPAY`, `CHIP`. Factory: `PaymentGatewayFactory.cs`. Ops + admin UIs offer exactly those four (`PaymentSettingsPage.tsx`, `PlatformPaymentSettingsPage.tsx`).

| Gateway | Checkout | Webhook verify | Off-session vault charge | Refund API | Customer billing portal | Status |
|---------|----------|----------------|--------------------------|------------|-------------------------|--------|
| **Stripe** | Yes (`StripeGatewayAdapter.cs` Checkout Session `mode=payment`) | Stripe-Signature | Yes `PaymentIntent` off_session | Yes `RefundService` | Yes Stripe Billing Portal | **SHIPPED** (best closed loop) |
| **Billplz** | Yes bills API + public callback base | `x_signature` HMAC (with/without extra fields) | **Throws `NotSupportedException`** | **`return false`** | Throws not supported | **PARTIAL** — FPX checkout + inbound paid is the SEA hero path; **no vaulted renewals, no API refunds** |
| **CHIP Collect** | Yes `gate.chip-in.asia` purchases | RSA `X-Signature` PEM | Yes (re-purchase from token) | Interface implemented | Not a Stripe-style portal | **PARTIAL** — checkout + webhook real; less battle-tested than Stripe/Billplz in docs/TODO |
| **Razorpay** | Payment links + registration links | `X-Razorpay-Signature`; EventId fail-closed | Implemented (order + pay) | Interface present | Not Stripe portal | **PARTIAL** — India path exists; EventId no longer invents Guids (gap 02 fix) |
| Fiuu / SenangPay / Xendit / Midtrans / Cashfree | — | — | — | — | — | **ABSENT** (README Phase 1 list is dishonest) |
| BTCPay / USDC / Web3 | — | — | — | — | — | **ABSENT** |
| Escrow.com | — | — | — | — | — | **ABSENT** |

Additional gateway facts:

- Secrets stored encrypted (`ISecretVault` / `AesSecretVault`); GET returns hints + `has_*` flags, never plaintext (`PaymentSettingsPage.tsx` comments).
- Soft-disable: `IsActive` on config (`20260804120000_AddPaymentConfigIsActive`). Inbound webhooks **still process** when soft-disabled (`ProcessGatewayWebhookCommandHandler.cs` comment). Off-session **refuses** inactive config and publishes failure.
- Billplz callback base must be public HTTPS (`BillplzPublicBase.cs`, `CALLBACK_BASE_NOT_PUBLIC`). Prod hosts `hub.lazuar.com` / `api.lazuar.com` / `pay.lazuar.com` select production Billplz (`TODO.md`).
- Platform (system) tenant can hold its own BYOK for **utility credit top-ups** (`PlatformEndpoints.cs` + `GenerateSystemCheckoutSessionQuery`).

**Honesty:** Sell “BYOK Stripe + Billplz (FPX) checkout.” Do **not** sell “local Asian gateways: Billplz, Fiuu, CHIP, Xendit, Razorpay” as a set. Do **not** sell “automatic card-on-file renewals” unless the product is on Stripe (or CHIP, with less evidence). Billplz subscriptions are reminder / magic-link / update-payment, not silent MIT.

---

### 3. Hosted checkout, custom checkout sessions, payment links, promo codes

| Capability | Status | Evidence | Honesty notes |
|------------|--------|----------|---------------|
| Hosted product checkout page | **SHIPPED** | Portal `/{tenant}/checkout/{slug}` → `GET /public/commerce/{tenantSlug}/products/{slug}` → `CheckoutForm.tsx` → `POST /public/commerce/checkout` (`PublicCheckoutEndpoints.cs`, `InitiateCheckoutCommandHandler.cs`) | Guest checkout, address/phone flags, quantity. TIN/company **stripped** (`[MVP-HIDE]`). |
| Redirect to gateway hosted pay | **SHIPPED** | `GenerateCheckoutSessionQuery` + adapters | Success URL back to portal success page. |
| Zero-amount / 100% coupon bypass | **SHIPPED** | `CheckoutResponse.Is_zero_amount_bypass`; `ProcessZeroAmountCheckoutCommand`; Billing `ZeroAmountCheckoutHandler.cs` | Completes without gateway. |
| Checkout status poll | **SHIPPED** | `GET /public/commerce/{tenantSlug}/checkout/{sessionId}/status` (+ legacy query-param path) | Does **not** mint portal tokens (comment in endpoint). |
| Promo codes on checkout | **SHIPPED** | `PromoCodeInput.tsx`; `GET /public/commerce/{tenantSlug}/validate-coupon`; `Coupon.cs` + `CouponEndpoints.cs`; ops `CouponsPage.tsx` | Percent/amount, max uses, min price, product allowlist, expiry. |
| Pay-what-you-want | **PARTIAL** | Product `PricingModel` FIXED/PWYW (`Product.cs`, `CreateProductForm.tsx`) | UI collects recommended + minimum. Portal `CheckoutForm.tsx` does not show a PWYW amount field. |
| Custom checkout / payment request / quote session | **BACKEND-ONLY** | `POST /admin/commerce/custom-checkouts`, `GET` list, `mark-paid`; public `GET /{tenant}/custom-checkouts/{sessionId}` + draft PDF | Portal `/pay/[sessionId]` calls `notFound()`. Ops Quotes page unrouted. |
| M2M ad-hoc checkout (integrator) | **SHIPPED** | `POST /integrations/payments/checkouts`, `GET .../{id}`, `GET .../me` | This is the Aura/Hub cashier. Idempotency-Key. Not Commerce catalog. |
| Checkout session expiry job | **SHIPPED** (backend) | `CheckoutSessionExpiryJob.cs` hosted in Commerce DI | Cleans pending sessions; no merchant UI. |
| Session + subscription metadata JSON | **SHIPPED** | Migration `20260814184123_AddSubscriptionAndCheckoutMetadataJson`; `Subscription.SetMetadataJson` | Survives expiry so renewals can emit Aura metadata on webhooks. |

**Honesty:** The sellable checkout is **product slug links** + **M2M amount checkouts**. Quote/pay links are built but **hidden**. PWYW is half-wired in the product form.

---

### 4. Products / checkout links / pricing (one-time, recurring, trials)

| Capability | Status | Evidence | Honesty notes |
|------------|--------|----------|---------------|
| Create/list/update/archive/restore products | **SHIPPED** | `ProductEndpoints.cs`; `ProductsPage.tsx`; `Product.cs` | Called “Checkout Links” in nav. |
| Price + currency + interval | **SHIPPED** | Intervals in form: `one_time`, `mo`, `yr` (`CreateProductForm.tsx`) | Currency hardcoded MYR in create form. |
| Per-product gateway | **SHIPPED** | `Product.GatewayName`; update command | Required non-empty. |
| Checkout UX flags (address, phone) | **SHIPPED** | `CheckoutConfiguration` VO | Tax ID flag hidden, always `false`. |
| Fulfillment targets (webhook URLs) | **PARTIAL** | Product stores `FulfillmentTargets`; ops filters `internal:community`/`internal:vault` (`utils.ts`) | HTTP targets can still be stored; Community/Vault internals are dead. Outbound workspace webhooks are the real unlock path. |
| Copy public checkout URL | **SHIPPED** | `ProductsPage.tsx` + `QuickCopy` using workspace slug | |
| Trials | **ABSENT** | No trial fields on Product; `TRIALING` only appears as a status string in CRM anonymize handler | Do not sell trials. |
| Usage / metered pricing | **ABSENT** | No usage records, no meter API | Credit wallet is **platform utility**, not customer usage billing. |
| Multi-currency catalog | **PARTIAL** | Product has `Currency` field | Ops create form hardcodes MYR. |

---

### 5. Subscriptions lifecycle

Domain: `Modules/Commerce/Domain/Aggregates/Subscription.cs`. Statuses actually transitioned in code: `PENDING`, `ACTIVE`, `PAST_DUE`, `SUSPENDED`, `CANCELED`.

| Capability | Status | Evidence | Honesty notes |
|------------|--------|----------|---------------|
| Create via paid checkout | **SHIPPED** | `GatewayPaymentCompletedIntegrationEventHandler.Subscription.cs` | Activates, stores vault tokens when gateway returns them. |
| Manual enroll (offline / bank) | **SHIPPED** | `POST /admin/commerce/subscribers`; `CreateManualSubscriberCommandHandler.cs`; `CreateSubscriberModal` | Can send welcome email; `IsReminderOnly` path for no vault. |
| Billing engine renewals | **PARTIAL** | `BillingEngineJob.cs` hourly; `SKIP LOCKED`; attempt 1 off-session if vaulted; else mark PAST_DUE | **Billplz has no off-session** → every Billplz renewal goes PAST_DUE / reminder. Stripe/CHIP can silent-renew. |
| Recover from payment (advance period + clear dunning) | **SHIPPED** | `Subscription.RecoverFromPayment`; used on arrears success (handler + tests `SubscriptionRecoveryTests.cs`) | Distinct from `Activate` which **does not** advance dates when already PAST_DUE (intentional). |
| Admin cancel | **SHIPPED** | `POST /subscribers/{id}/cancel`; `SubscribersPage.tsx` | Emits `subscription.canceled`. |
| Buyer portal cancel | **SHIPPED** | `POST /public/commerce/{tenantSlug}/portal/cancel`; portal page form | Magic-link bound. |
| Record offline payment | **SHIPPED** | `POST /subscribers/{id}/record-payment` | |
| Pause **subscription** (keep access, stop billing) | **ABSENT** | No `Pause()` on Subscription; only `PauseDunning` | Do not sell “pause plan.” |
| Pause **dunning** until date | **SHIPPED** | `POST .../dunning/pause|resume`; subscribers UI | CS tool, not customer self-serve. |
| Upgrade / downgrade / proration | **ABSENT** | No commands, no UI | |
| Usage-based recurring | **ABSENT** | | |
| Portal list + magic link | **SHIPPED** | `GET /public/commerce/{tenantSlug}/portal?token=`; `MagicLinkTokenService.cs`; admin `POST /subscribers/portal-link` | Buyer landing `app/page.tsx` tells them to use email link. |
| CSV export | **SHIPPED** | `GET /subscribers/export` cap 10_000 | |
| Metadata on subscription.* webhooks | **SHIPPED** | `MetadataJson` + `CommerceWebhookPayload` | Needed for Aura/org correlation. |

**Honesty:** Recurring billing is **real for vaulted Stripe (and CHIP)**. For Billplz (the Malaysian FPX story) recurring means **dunning emails + update-payment link**, not silent debit. That is sellable if phrased honestly. It is **not** Stripe Billing.

---

### 6. Dunning campaigns, retries, off-session, magic update-payment

| Capability | Status | Evidence | Honesty notes |
|------------|--------|----------|---------------|
| Campaign CRUD + builder UI | **SHIPPED** | `DunningCampaignEndpoints.cs`; `DunningCampaignsPage.tsx`; `CampaignBuilderPage.tsx`; `DunningCampaign.cs` | Name, grace days, final CANCEL/SUSPEND/NONE, priority, product + payment-method targeting, steps. |
| Default campaign generate | **SHIPPED** | `POST /dunning-campaigns/defaults` | |
| Engine job | **SHIPPED** | `DunningEngineJob*.cs` hosted; pre-dunning + past-due; claim batches; catch-up `DayOffset <= daysOverdue` | Aug-3 “exact calendar day only” is **stale**. Catch-up exists. |
| Failed charge enters PAST_DUE | **SHIPPED** | `GatewayPaymentFailedIntegrationEventHandler.cs` assigns campaign + emits `subscription.past_due` | Aug-3 P0 is **fixed in code**. |
| AUTO_CHARGE steps | **PARTIAL** | PastDue dispatches `ExecuteOffSessionChargeIntegrationEvent`; max 4 attempts/cycle (`ChargeAttemptLimits.cs`) | Works where adapter supports it (Stripe/CHIP/Razorpay). Billplz throws. |
| EMAIL steps | **SHIPPED** | Dispatch → Communications → Messaging Resend | Requires tenant Resend BYOK. |
| WHATSAPP steps | **PARTIAL / frozen** | Step type exists; `Messaging:WhatsAppEnabled` default **false**; `ConsoleMessagingService` logs only; decision 00.4 **no WA for 6 months** | Engine skips WA or falls back to email (`ResolveEffectiveCommunicationAction`). **Do not sell WhatsApp dunning.** |
| Recovery metrics on campaign | **SHIPPED** (backend) | `RecordRecovery` / `RecordChurn` on campaign | Domain tracks it. |
| Magic update-payment page | **SHIPPED** | Portal `update-payment/[subId]`; `PublicArrearsEndpoints.cs` | Only PAST_DUE/SUSPENDED. Creates new gateway session with subscription metadata. |
| Pre-dunning reminders (before due) | **SHIPPED** (engine) | `DunningEngineJob.PreDunning.cs` | Uses reminder logs + day offset. Default catalog orphaned some old reminder template names (`DefaultMessageTemplates.OrphanNames`). |

**Honesty:** Configuration + email recovery + Stripe retries is a closed loop **if** Resend is configured and a campaign exists. WhatsApp is a **template field and a console logger**. Grace final actions cancel/suspend and emit fulfillment + outbound webhooks.

---

### 7. Communications: email, WhatsApp, templates, suppression

| Capability | Status | Evidence | Honesty notes |
|------------|--------|----------|---------------|
| Tenant Resend BYOK | **SHIPPED** | `EmailSettingsPage.tsx`; `GET/PUT /admin/communications/email-config`; `ResendEmailService.cs` | Dashboard **blocks** checkout activation warning if email missing (`DashboardPage.tsx`). M2M cashier is **not** blocked. |
| Platform Resend fallback | **PARTIAL** | System tenant may use `Resend:ApiKey` in appsettings; tenant mail requires BYOK (no platform fallback) | Correct for multi-tenant isolation. |
| Lifecycle templates CRUD + preview | **SHIPPED** | `TemplateEndpoints.cs`; `TemplatesPage.tsx`; `DefaultMessageTemplates.cs` | Catalog: Payment Failed, Subscription Cancelled, Digital Product Delivery, Quotation Ready, Official Receipt. Variables include `{{plan_name}}`. |
| Template reset / orphan cleanup | **SHIPPED** (ops utility) | `DELETE /templates/{id}` = reset; `DELETE /templates/legacy-cleanup` impl-only | Community* orphans listed for cleanup. |
| Broadcasts | **PARTIAL** | `BroadcastEndpoints.cs` + `BroadcastFanoutJob.cs`; credits reserved at 0 (“v1 free”) | **No ops Broadcasts page** in current sidebar. API exists. |
| Suppression (unsubscribe + bounce/complaint) | **SHIPPED** | `PublicComplianceEndpoints.cs` HMAC unsubscribe HTML; Resend Svix webhook; `SuppressionService` | Fail-closed in Production if webhook secret missing. |
| WhatsApp / SMS transport | **SCAFFOLD / frozen** | `ConsoleMessagingService.cs`; `Messaging:WhatsAppEnabled=false`; credits cost `WhatsAppSend: 2` in appsettings | Decision 00.4. Delivery logs can record SKIPPED. |
| Messaging delivery logs API | **BACKEND-ONLY** | `GET /messaging/delivery-logs` OrgAdmin; honesty allowlist impl-only | Not the Developer Delivery Logs page (that is **outbound webhooks**). |
| Digital delivery email | **PARTIAL** | `OrderCompletedDigitalDeliveryHandler.cs` | Depends on fulfillment URL; Vault module gone so file delivery is not a product. |
| Quote / receipt emails | **BACKEND-ONLY** | Templates + `DocumentPublishedIntegrationEventHandler` | Quote UI hidden; receipts generate in Billing. |

**Honesty:** Email is the production channel. WhatsApp is leftover UX copy (checkout “WhatsApp Number”, privacy policy lists Meta, billing settings copy mentions WhatsApp credits). That copy is **dishonest** relative to `WhatsAppEnabled=false`.

---

### 8. Billing ledger, net profit, tax liability, receipts/PDFs, deferred revenue

| Capability | Status | Evidence | Honesty notes |
|------------|--------|----------|---------------|
| Double-entry ledger on payment | **SHIPPED** | `GatewayPaymentCompletedHandler.cs`: cash + fee + gross + tax; `ValidateBalanced()`; skip utility top-ups | Account types in `AccountTypes.cs`. |
| Refund ledger | **SHIPPED** | `GatewayRefundCompletedHandler` (tests exist) | Contra revenue. |
| Financial summary + net profit APIs | **SHIPPED** | `AdminLedgerEndpoints.cs` `/summary`, `/net-profit`; dashboard uses summary | Phase C: signed sums (gross − contra − fees − tax). |
| Ledger list (admin) | **SHIPPED** (API) | `GET /admin/billing/ledger` | **No live ops page** in sidebar. TaxInvoices/CreditNotes pages consume this but are unrouted. |
| B2C official receipt number + PDF | **SHIPPED** (backend) | `AssignB2cReceipt`; `GenerateAndStoreDocumentCommandHandler` QuestPDF + R2 | Buyer tax-invoice button **hidden**. Email document_link is the intended path. |
| Draft proforma PDF | **BACKEND-ONLY** | `GET /public/billing/{slug}/documents/draft/{sessionId}` HMAC | Used by custom checkout DTO `draft_pdf_url`; quote UI hidden. |
| Signed final PDF download | **BACKEND-ONLY** | HMAC redirect allowlisted (not in TypeSpec) | Email/human only. |
| B2C monthly consolidation | **BACKEND-ONLY** | `B2cConsolidationJob.cs` 28th 02:00 MYT + catch-up; emits consolidated invoice event for Lhdn | No merchant “consolidation ran” UI. Honest as silent backend if LHDN tenant config exists. |
| Deferred revenue recognition | **SCAFFOLD / PARKED** | `DeferredRevenueSchedule`; `RevenueRecognitionJob` **unregistered** (`Billing DI` comment; decision 00.3) | **Do not claim deferred revenue / ASC 606.** |
| Tax liability account | **SHIPPED** (math) | `LIABILITY_TAX_PAYABLE` line when `TaxAmount > 0` | Gateway tax, not a merchant SST settings UI. |
| Affiliate commission / AR | **SCAFFOLD** | Account types + `CommissionAccruedHandler.cs` | No affiliate product. |
| Net Cash in Bank on dashboard | **SHIPPED** | `DashboardPage.tsx` `financials?.net_revenue` | Label matches README “Net Cash in Bank.” Trust depends on fee extraction (Stripe expand works; Billplz often estimated/zero). |

**Honesty:** Ledger is real and posts on the money path. It is **not** a CFO product (no Xero, no recognition, no tax-invoice UI). Selling “double-entry books” to engineers is fair. Selling “replace your accountant” is not.

---

### 9. Credit wallet / utility metering

| Capability | Status | Evidence | Honesty notes |
|------------|--------|----------|---------------|
| Wallet aggregate + holds + idempotent deduct | **SHIPPED** (backend) | `TenantCreditBalance.cs`; `CreditHold`; `DeductTenantCreditCommandHandler`; tests concurrency/idempotency | Costs: `LhdnSubmit=3`, `WhatsAppSend=2` (`appsettings.json`). Starter grant 50. |
| Packages + top-up checkout | **SHIPPED** but **nav-hidden** | `AdminCreditsEndpoints.cs`; `BillingSettingsPage.tsx` (routed `/workspace/billing` **not in sidebar**); `PlatformTopUpEventHandler` | Min RM 50. Uses **platform** payment config (`GenerateSystemCheckoutSessionQuery`). |
| Credit history | **SHIPPED** but **nav-hidden** | `UtilityLedgerPage.tsx` `/workspace/ledger` | |
| Chargeback clawback on utility top-up | **SHIPPED** (backend) | `ChargebackClawbackHandler.cs` — **only** `type=utility_credit_topup` | Does **not** reverse merchant GMV or suspend subs. |
| Credits stay in Billing 6–12 months | Policy | `decisions.md` 00.5 | No separate Credits module. |

**Honesty:** Wallet works. Ops discovery is weak (no sidebar). Copy on `BillingSettingsPage.tsx` still says credits are for “LHDN tax submissions and WhatsApp dunning” — LHDN deduct is real; WhatsApp deduct will not fire while WA is disabled.

---

### 10. LHDN MyInvois

| Capability | Status | Evidence | Honesty notes |
|------------|--------|----------|---------------|
| Submit document (JSON → UBL XML) | **BACKEND-ONLY** (integrator **SHIPPED**) | `POST /lhdn/documents` + Idempotency-Key; `LhdnSubmissionJob`; strategies + Scriban templates + XSD | Merchants have **no ops UI** (ADR 023). Integrators with `lhdn.documents:write` can submit. |
| Status poll | **BACKEND-ONLY** | `LhdnStatusPollingJob`; `GET /lhdn/documents/{internalId}` | |
| Cancel | **BACKEND-ONLY** | `POST /lhdn/documents/{internalId}/cancel`; `CancelWindowMustBeValidRule` | |
| TIN validate | **BACKEND-ONLY** | `POST /lhdn/taxpayer/validate`; `LhdnGatewayAdapter.Tin.cs`; cache entity `TinValidateCache` | Checkout TIN UI hidden — B2B “validate before pay” **not** in portal. |
| Credit / debit / refund notes | **BACKEND-ONLY** | `DocumentStrategyFactory`: 02/03/04 → `CreditNoteStrategy`; 11 self-billed invoice; 12–14 self-billed credit | Debit/refund share CreditNote XML with injected `doc_type_code`. Not separate UIs. |
| Self-billed | **BACKEND-ONLY** | Templates + entity swap in `ViewModelMapper` | Affiliate story; no affiliate product. |
| B2C consolidation XML | **BACKEND-ONLY** | `ConsolidatedInvoiceStrategy` + Billing job event `ConsolidatedInvoiceIssuedIntegrationEventHandler` | |
| Tenant MyInvois config + P12 cert | **BACKEND-ONLY** | `TenantConfigEndpoints.cs` | No ops page (BillingProfile hidden). |
| XAdES / XMLDSig signatures | **ABSENT / unimplemented** | Lhdn README §3: V1.0 unsigned; placeholder in templates | **Do not claim signed V1.1 e-invoices.** |
| LHDN customer webhooks `invoice.valid` / `invoice.invalid` | **SHIPPED** (platform dispatcher) | Lhdn README §5; R43 retired fire-and-forget | Via One endpoints + signing. |
| LHDN-local webhook register API | **PARTIAL** | `AdminWebhookEndpoints.cs` still maps `/lhdn/webhooks` | Registry end-state is One; Lhdn table retained. |
| Reference data seeder | **SHIPPED** (worker) | `LhdnReferenceDataSeederJob`; MSIC/tax/country entities | |
| SDKs | **SHIPPED** | `packages/lhdn-sdk-ts`, `packages/lhdn-sdk-dotnet` Kiota | Documented on developers hub footer. |
| Sandbox scripts | **SHIPPED** (eng) | `scripts/lhdn_sandbox/` | Not a merchant feature. |

**Honesty:** LHDN is a **serious backend + SDK product** and a **hidden merchant feature**. Selling “Malaysian e-invoice compliance included in checkout” to a non-integrator merchant is **false** under ADR 023. Selling “API to submit MyInvois UBL” to an ERP is **true** (unsigned V1.0).

---

### 11. Quotes / tax invoices / credit notes UI (invoicing module)

| Capability | Status | Evidence | Honesty notes |
|------------|--------|----------|---------------|
| Quotes list/create UI | **BACKEND-ONLY** | `QuotesPage.tsx` + `CreateQuoteModal.tsx` exist; **no Route** | Calls `/admin/commerce/custom-checkouts`. |
| Tax invoices UI | **BACKEND-ONLY** | `TaxInvoicesPage.tsx` lists ledger `type_filter=sales` | Unrouted. |
| Credit notes UI | **BACKEND-ONLY** | `CreditNotesPage.tsx` `type_filter=reversals` | Unrouted. |
| Buyer quote pay page | **BACKEND-ONLY** | `pay/[sessionId]/page.tsx` `notFound()` | |
| Mark checkout paid offline | **BACKEND-ONLY** | `POST /admin/commerce/checkouts/{id}/mark-paid` | No live nav. |
| Legal & billing profile UI | **BACKEND-ONLY** | `BillingProfilePage.tsx` unrouted; API `AdminProfileEndpoints.cs` | TIN, SST, logo, address. |

**Honesty:** Do not show screenshots of Quotes/Invoices as shipping product.

---

### 12. Outbound webhooks + delivery logs + signing

| Capability | Status | Evidence | Honesty notes |
|------------|--------|----------|---------------|
| Multi-endpoint CRUD | **SHIPPED** | `WebhookEndpoints.cs`; `DeveloperSettingsPage.tsx` | Create returns `whsec_` once; rotate; disable; event allowlist (empty = all). |
| Event catalog (ops UI) | **SHIPPED** | `WEBHOOK_EVENT_OPTIONS` in DeveloperSettingsPage | `subscription.activated|resumed|suspended|canceled|past_due`, `order.completed`, `payment_link.paid`, `payment.completed`, `payment.failed`. LHDN `invoice.*` not in this ops checklist (still delivered if subscribed via One). |
| HMAC signing Standard Webhooks-style | **SHIPPED** | `OutboundWebhookSignature.cs` `t=,v1=` over `{ts}.{body}`; header `X-Lazuar-Signature` | Also `X-Lazuar-Event`, `X-Lazuar-Delivery-Id`, `X-Lazuar-Webhook-Id`. |
| Dispatcher + retry + lease | **SHIPPED** | `OutboundWebhookDispatcherJob.cs` 10s interval; claim lease; permanent HTTP fail vs retry | |
| Delivery logs UI | **SHIPPED** | `DeliveryLogsPage.tsx` → `GET /one/workspaces/{id}/webhooks/logs` | Status, attempts, last error. Redrive-from-UI not seen (retry is automatic). |
| Silent exact-URL product match | **Fixed / gone as product rule** | Aug-3 `18-outbound-customer-webhooks.md`; current path is workspace endpoints + event types | Do not revive “set product fulfillment URL = webhook URL.” |
| SSRF URL validation | **SHIPPED** (backend) | `WebhookUrlValidator.cs` | |

**Honesty:** Outbound webhooks are now a real Developer product. Payloads are still relatively thin (id + status + metadata) — integrators may need a follow-up GET. That is acceptable to sell as v1.

---

### 13. Inbound gateway webhooks + idempotency

| Capability | Status | Evidence | Honesty notes |
|------------|--------|----------|---------------|
| Multi-gateway ingress | **SHIPPED** | `POST /webhooks/payments/{gatewayType}/{tenantId}` | Allow-list; empty body throws (TODO curl expects 400). |
| Signature verify per adapter | **SHIPPED** | Stripe / Billplz / CHIP / Razorpay parsers | Unverified → exception (not silent OK). |
| EventId + business-key idempotency | **SHIPPED** | `ProcessGatewayWebhookCommandHandler` + `HasBusinessKeyBeenProcessedAsync`; migration `AddPaymentWebhookBusinessKey` | Dedupes payment identity, not only provider event id. |
| Metadata rehydrate from integration session | **SHIPPED** | `MergeSessionMetadataAsync` for Billplz-stripped fields | Billplz also stamps `checkout_id` on callback query. |
| Emit completed / failed / dispute | **SHIPPED** | Events published to Payments outbox | Failed was a gap; now first-class. |
| Refunds inbound | **PARTIAL** | Stripe refunds via Commerce `RecordRefundCommand` **outbound** to gateway; inbound refund webhooks not the primary path | Merchant refunds from Transaction Logs UI. Billplz `IssueRefundAsync` returns false → **cannot** refund Billplz from product. |
| Raw intake / replay UI | **ABSENT** | Logs in DB + structured log line (Phase C notes) | Support = SQL + logs, no timeline UI. |

---

### 14. Integrator / Connect / provision (Aura hop)

| Capability | Status | Evidence | Honesty notes |
|------------|--------|----------|---------------|
| Provision workspace for external org | **SHIPPED** | `POST /one/integrations/workspaces/provision`; `ProvisionAuraWorkspaceCommand`; tests `ProvisionAuraWorkspaceTests.cs` | Products: legacy `aura_org_id` or `external_product` + `external_org_id`. |
| Mint scoped payments key | **SHIPPED** | Default Aura scopes: checkouts write/read + webhooks manage (`PlatformApiScopes.DefaultAuraIntegratorScopes`) | |
| Register companion webhook | **SHIPPED** | Provision body `webhook_url` + events | Aura expects `payment.completed` / `payment.failed` (`TODO.md` hop B). |
| M2M cashier | **SHIPPED** | `IntegrationEndpoints.cs`; sample `examples/hub-cashier-next` | Docs: `apps/lazuar-docs/docs/integrations/*`, developers `/payments-cashier`. |
| `GET /integrations/payments/me` | **SHIPPED** | Key introspection; humans 403 | |
| Connect UI in ops (“Connect Aura”) | **ABSENT** | Server-to-server only | Aura pastes key / calls provision. |
| Commerce M2M admin (create products via key) | **ABSENT** | Developers page explicitly: “Admin product CRUD is console-only” | |

**Honesty:** Hub-as-Aura-cashier is the **most honest new sell** in this repo. It is documented, sampled, scoped, and webhooked.

---

### 15. CRM / client profiles

| Capability | Status | Evidence | Honesty notes |
|------------|--------|----------|---------------|
| Tenant-scoped PII registry | **SHIPPED** (internal) | `CRM/Domain/ClientProfileEntity.cs`; create/resolve/anonymize handlers | **No HTTP endpoints** (`CRM/README.md`). Other modules use `ICrmQueryService`. |
| Consent default false | **SHIPPED** | Migration `ConsentDefaultFalse` | |
| PDPA anonymize + fan-out | **BACKEND-ONLY** | `AnonymizeClientProfileCommand`; Commerce cancels subs; Communications suppresses | No merchant “delete customer” button. Privacy policy tells buyers to email the creator or `privacy@lazuar.com`. |
| CRM UI / pipeline / tickets | **ABSENT** | README: not leads/deals | |

---

### 16. Analytics / dashboard / stats

| Capability | Status | Evidence | Honesty notes |
|------------|--------|----------|---------------|
| Commerce stats API | **SHIPPED** | `StatsEndpoints.cs`; `CommerceQueryService.Stats.cs` | MRR (yr/12), active/past-due, 30-day churn, ARPU, confirmed tx revenue, 6-month cash trend, payment method mix. |
| Ops dashboard | **SHIPPED** | `DashboardPage.tsx` | Net cash (billing summary), active, past due, cancellation rate, gateway/email setup warnings, product list, bar chart. |
| Deep analytics / cohorts / tax pack | **ABSENT** | | |
| Platform-wide admin analytics | **ABSENT** | Admin has only gateway settings | |

**Honesty:** Dashboard is a real ops home, not a BI product. MRR includes PAST_DUE in `activeSubs` (same filter as “Active Subscribers” count) — slightly optimistic.

---

### 17. Developer DX: docs, SDKs, sample app

| Capability | Status | Evidence | Honesty notes |
|------------|--------|----------|---------------|
| Scalar OpenAPI hub | **SHIPPED** | `apps/lazuar-developers` | Split product vs internal (Ops dashed). |
| Integrator guides (VitePress) | **SHIPPED** | `apps/lazuar-docs/docs/` — cashier, webhooks, provision, Aura reference, hub vs DIY | Front-matter says **drafts for refinement**. |
| TypeSpec → TS/C# clients | **SHIPPED** | `task gen`; honesty CI `scripts/check-openapi-minimal-honesty.mjs` | |
| LHDN SDKs | **SHIPPED** | `packages/lhdn-sdk-ts`, `packages/lhdn-sdk-dotnet` | |
| Hub cashier sample | **SHIPPED** (example) | `examples/hub-cashier-next` README: S40–S46; webhook-only fulfill | Not production software; port 3020. |
| Postman | **PARTIAL** | `docs/postman/` | May drift; TypeSpec is SSoT. |
| Payments SDK (non-LHDN) | **ABSENT** | Sample uses raw `fetch` | |

---

### 18. Admin platform

| Capability | Status | Evidence | Honesty notes |
|------------|--------|----------|---------------|
| Superadmin login | **SHIPPED** | `lazuar-admin` | |
| Platform payment gateways (system tenant) | **SHIPPED** | `PlatformPaymentSettingsPage.tsx` → `/platform/payment-config` | Needed for credit top-ups. |
| Tenant directory / impersonation UI | **ABSENT** | Superadmin uses **ops** entitlements instead | |
| Grant credits / support tools | **ABSENT** as UI | Commands exist (`ClawbackCredits`, deduct) | |
| Feature flags / kill switches | **ABSENT** | | |

**Honesty:** `lazuar-admin` is a **thin control plane**, not AWS-style superapp (README still says “AWS-style superapp” for **ops**, which is closer but still CaaS-lobotomized).

---

### 19. Refunds, disputes, chargebacks

| Capability | Status | Evidence | Honesty notes |
|------------|--------|----------|---------------|
| Merchant-initiated refund (Stripe) | **SHIPPED** | `POST /admin/commerce/transactions/{id}/refund` → `RecordRefundCommand` → `GatewayRefundRequestedIntegrationEventHandler` | Ops Transactions page. |
| Billplz refund | **ABSENT** in adapter | `BillplzGatewayAdapter.IssueRefundAsync` → `false` | UI may offer refund; gateway will fail. **Dishonest if shown for Billplz txs.** |
| Dispute webhook (Stripe) | **PARTIAL** | Stripe parser emits `DISPUTE_CREATED`; `ChargebackClawbackHandler` only claws **utility credits** | Commerce subs are **not** auto-suspended on chargeback. |
| Buyer self-serve refund | **ABSENT** | Legal refund page: Lazuar is not MoR | Correct legally. |

---

### 20. Legal pages, PDPA, tax settings

| Capability | Status | Evidence | Honesty notes |
|------------|--------|----------|---------------|
| Privacy / terms / refund pages | **SHIPPED** (static) | `apps/lazuar-portal/src/app/legal/*` | Dated June 2026. |
| PDPA processor framing | **SHIPPED** (copy) | Privacy: creator = controller, Lazuar = processor | |
| Privacy copy still claims WhatsApp + community | **PARTIAL / dishonest** | Privacy lists Meta WhatsApp + “community resources”; terms mention courses/communities | Vault/Community removed. |
| Tax / SST / TIN merchant settings | **BACKEND-ONLY** | Billing profile API + hidden page | |
| Unsubscribe (marketing) | **SHIPPED** | HMAC link | |

---

### 21. Background workers, outbox/inbox

Every module registers `*OutboxPublisherJob` + `*InboxConsumerJob` via `ModuleOutboxInboxServiceCollectionExtensions` (retry + dead letter migrations Aug 3). Additional hosted services:

| Worker | Module | Interval / schedule | Status |
|--------|--------|---------------------|--------|
| `BillingEngineJob` | Commerce | `Workers:BillingEngineInterval` default 1h | **SHIPPED** |
| `DunningEngineJob` | Commerce | 1h | **SHIPPED** |
| `CheckoutSessionExpiryJob` | Commerce | hosted | **SHIPPED** |
| `OutboundWebhookDispatcherJob` | One | 10s | **SHIPPED** |
| `SystemGenesisBootstrapperJob` | One | boot | **SHIPPED** (seed) |
| `BroadcastFanoutJob` | Communications | 10s | **PARTIAL** (API without UI) |
| `LhdnSubmissionJob` | Lhdn | 5s | **SHIPPED** |
| `LhdnStatusPollingJob` | Lhdn | 10s | **SHIPPED** |
| `LhdnReferenceDataSeederJob` | Lhdn | boot | **SHIPPED** |
| `B2cConsolidationJob` | Billing | 28th 02:00 MYT + catch-up | **SHIPPED** (backend) |
| `RevenueRecognitionJob` | Billing | **not registered** | **SCAFFOLD** |
| `PlatformMetricsRefreshJob` | Host | 30s | **SHIPPED** (ops observability) |
| Legacy API key / webhook migrators | Host | optional env | **SCAFFOLD** tools |

**Honesty:** Outbox drain for Lhdn/CRM (Aug-3 P0) has publisher jobs registered now. Multi-instance: claim + `SKIP LOCKED` on dunning/billing/webhooks/LHDN; deploy still says **replica=1** (`TODO.md`, `deploy/prod/README.md`).

---

### 22. README / ADR claims the code does not support

| Claim | Source | Reality |
|-------|--------|---------|
| Local gateways: Billplz, **Fiuu**, CHIP, **Xendit**, Razorpay | README Phase 1 | Only Stripe, Billplz, CHIP, Razorpay adapters |
| Gov tax: LHDN, **GSTN**, **Coretax** | README Phase 1 | LHDN only |
| **Xero / QuickBooks** | README Phase 1; ADR 021 keep | **ABSENT** |
| **Native WhatsApp dunning** (Meta Cloud) | README architecture diagram; ADR 019/021; privacy policy | Console logger + flag off |
| Vault / SaaS secure R2 PDF fulfillment as product | README diagram | R2 used for **receipt PDFs** + presign; Vault module **deleted** |
| Community fulfillment | README / legal | Module deleted |
| Escrow + e-sign at B2B checkout | ADR 021 pillar 2 | **ABSENT**; quote checkout hidden |
| USDC/Web3 + zero-rated export | ADR 021 pillar 3 | **ABSENT** |
| XAdES signed invoices | Implied by “mathematically perfect signed XML” in ADR 021 | README/module: **unsigned V1.0**; signatures unimplemented |
| 15 apps / marketplace | ADR 014/018 | Killed |
| Ops is “AWS-style superapp” | README | Thin CaaS console; invoicing/chat hidden |
| Automated WhatsApp in credit wallet marketing | `BillingSettingsPage.tsx` | WA disabled |
| Require WhatsApp number at checkout | product form + `CheckoutForm` label | Collects phone; does not send WA |

---

## Closed loops vs broken loops

### Closed loops you can demo today (honest)

1. **Ops signup → workspace → BYOK Stripe or Billplz → Resend key → create checkout link → buyer pays → success page → ledger row → receipt PDF generated → optional email.**  
   Evidence: ops pages + `InitiateCheckoutCommand` + webhook handler + `GatewayPaymentCompletedHandler` + document command.

2. **Stripe subscription with vault → billing engine off-session → success recovers period; failure → PAST_DUE → email dunning → update-payment magic link → recover.**  
   Evidence: `BillingEngineJob`, `ChargeOffSession` Stripe, `GatewayPaymentFailedIntegrationEventHandler`, portal update-payment, `RecoverFromPayment`.

3. **Integrator provision → `sk_test_` → `POST /integrations/payments/checkouts` → pay → `payment.completed` signed webhook → sample app marks order paid.**  
   Evidence: provision endpoint, integration endpoints, `hub-cashier-next`, docs product-lines.

4. **Ops Developer: mint scoped key, register webhook, see delivery logs.**  
   Evidence: ApiKeys + DeveloperSettings + DeliveryLogs pages.

5. **LHDN SDK submit → job → poll → `invoice.valid` via One dispatcher** (if tenant MyInvois config + credits).  
   Evidence: document endpoints, jobs, Lhdn README §5. **No merchant UI.**

6. **Admin cancel / portal cancel / CSV export / record offline payment / coupon at checkout.**

### Broken or incomplete loops (do not demo as done)

1. **Billplz recurring silent debit** — adapter throws; loop is email + new bill.  
2. **WhatsApp dunning** — flag false, console transport.  
3. **Quote → pay → tax invoice download** — UI lobotomy.  
4. **B2B TIN at checkout → instant MyInvois QR** — TIN fields hidden; signatures unimplemented.  
5. **Merchant refund on Billplz** — adapter returns false.  
6. **Chargeback → cancel commerce sub** — only utility clawback.  
7. **Upgrade/proration/trials/usage.**  
8. **Xero export.**  
9. **Credits top-up** works but **not in sidebar** (discoverability break).  
10. **Ops AI chat** — backend `/ops` chat+stream live; UI unrouted.  
11. **Member invites / forgot password** — API only.  
12. **PWYW** — product flag without checkout amount field.  
13. **Digital file delivery** — Vault gone; template still mentions `{{fulfillment_url}}`.  
14. **Broadcasts** — API + worker, no nav.  
15. **Deferred revenue** — table + parked job.

---

## README/ADR claims vs code

### Aligned (safe to repeat)

- Headless CaaS, not CMS (ADR 015/019).  
- BYOK, not MoR (legal refund page + adapters).  
- Double-entry ledger posts on gateway completion.  
- Pure CaaS UI lobotomy (ADR 023) is implemented with `[MVP-HIDE]` exactly where claimed.  
- Community/Vault removed from API composition (ADR 022 Phase 2 largely done on backend).  
- TypeSpec SSoT + honesty allowlist.  
- Hub path for Aura (`TODO.md`, provision, M2M).  
- Prepaid credits for LHDN submit.

### Misaligned (must not repeat without asterisks)

- Phase 1 “Un-Fireable Core” list (Fiuu, Xendit, GSTN, Coretax, Xero, native WhatsApp).  
- Architecture ASCII art: Vault + WhatsApp as live fulfillment.  
- ADR 021 pillar 2 “Escrow and E-Signatures at checkout” + “returns official LHDN QR to corporate buyer” as current UX.  
- ADR 021 keep Xero — zero code.  
- “Automated WhatsApp dunning” as differentiator (ADR 023 mitigation text still says this).  
- Privacy/terms community + Meta WhatsApp sub-processor as current behavior.

### Intentionally dark matter (aligned with ADR 023)

- LHDN pipeline, quotes, billing profile, tax invoice buttons — **code yes, product UI no.** Marketing must say “coming” or “API-only.”

---

## What we can honestly sell today

### To a Malaysian / SEA creator (ops + portal)

- Hosted **checkout links** (one-time or monthly/yearly) with **promo codes**.  
- **Billplz FPX** and **Stripe** (and CHIP/Razorpay if they have those accounts) **BYOK** — money in *their* merchant account.  
- **Subscriber list**, manual enroll, offline payment, cancel, CSV.  
- **Email** receipts and **failed-payment emails** if they paste a **Resend** key.  
- **Dunning campaign builder** (email + auto-charge **where the gateway allows** + cancel/suspend).  
- **Update-payment page** for past-due.  
- Buyer **magic-link portal** to see plans and cancel.  
- **Transaction log** and a **simple dashboard** (net cash, MRR-ish, past due).  
- **Stripe refunds** from the transaction log.  
- Legal pages stating Lazuar is not MoR.

Caveats that must be in the sales sentence: no WhatsApp; Billplz will not auto-debit; no tax invoice UI; no trials/proration.

### To an integrator / Aura / any SaaS backend

- **Provision** a workspace + **scoped `sk_`** + **webhook secret**.  
- **M2M checkout** for ad-hoc amounts + metadata + idempotency.  
- **Signed** `payment.completed` / `payment.failed`.  
- **Introspect** key via `/integrations/payments/me`.  
- **Sample app** + VitePress + Scalar.  
- **LHDN document API + SDKs** (unsigned V1.0) with credit metering.

### To a platform operator

- Superadmin cookie console to set **system** gateway keys for credit top-ups.  
- Single-replica API with workers in-process.  
- Metrics/health endpoints.

---

## What we must not claim

1. Native WhatsApp / Meta Cloud dunning.  
2. Fiuu, Xendit, Midtrans, SenangPay, Cashfree, BTCPay.  
3. GSTN, Coretax, InvoiceNow.  
4. Xero or QuickBooks sync.  
5. Merchant of Record, tax remittance, or “we issue your refunds.”  
6. LHDN-validated QR on the hosted checkout thank-you page (UI hidden; V1.1 signatures off).  
7. Escrow, e-sign, DRM, community bouncer, link-in-bio.  
8. Usage-based billing, trials, upgrades, proration, pause-subscription.  
9. Billplz off-session renewals or Billplz API refunds.  
10. Deferred revenue / “CFO-grade recognition.”  
11. Chargeback automation for commerce subscriptions.  
12. Multi-region HA / multi-replica workers.  
13. That Community or Vault still fulfill purchases.  
14. That Quotes / Tax Invoices / Credit Notes are in the merchant app.  
15. That the developers hub is only internal Swagger — it is better now, but Commerce admin is still JWT/console, not M2M.

---

## Suggested feature IDs for the tracker (LP-001 …)

Use these IDs as the competitor-feature program rows. Status is **today**.

| ID | Feature | Status today | Primary evidence |
|----|---------|--------------|------------------|
| LP-001 | Human JWT session auth (ops) | SHIPPED | `AuthEndpoints.cs`, `LoginPage.tsx` |
| LP-002 | Platform superadmin JWT | SHIPPED | `PlatformAuthEndpoints.cs`, `lazuar-admin` |
| LP-003 | Public register + first workspace | SHIPPED | `POST /one/public/register` |
| LP-004 | Multi-tenant workspaces + switcher | SHIPPED | `WorkspaceEndpoints.cs`, `App.tsx` |
| LP-005 | Tenant isolation fail-closed | SHIPPED | `TenantSecurityMiddleware.cs` |
| LP-006 | Platform API keys (scoped, ops UI) | SHIPPED | `ApiCredentialEndpoints.cs`, `ApiKeysPage.tsx` |
| LP-007 | Forgot/reset password UX | BACKEND-ONLY | Auth endpoints; no ops UI |
| LP-008 | Workspace invites / members UX | BACKEND-ONLY | Invite APIs; no ops UI |
| LP-009 | Stripe BYOK checkout + webhooks | SHIPPED | `StripeGatewayAdapter.cs` |
| LP-010 | Billplz BYOK checkout + webhooks | SHIPPED | `BillplzGatewayAdapter.cs` |
| LP-011 | CHIP BYOK checkout + webhooks | PARTIAL | `ChipCollectGatewayAdapter.cs` |
| LP-012 | Razorpay BYOK checkout + webhooks | PARTIAL | `RazorpayGatewayAdapter.cs` |
| LP-013 | Fiuu / Xendit / Midtrans / SenangPay | ABSENT | README-only |
| LP-014 | Hosted product checkout | SHIPPED | portal checkout + `PublicCheckoutEndpoints.cs` |
| LP-015 | Promo codes | SHIPPED | `CouponEndpoints.cs`, `PromoCodeInput.tsx` |
| LP-016 | PWYW pricing | PARTIAL | Product model; weak checkout UI |
| LP-017 | Custom quotes / pay-by-link | BACKEND-ONLY | custom-checkouts API; portal `notFound()` |
| LP-018 | M2M ad-hoc cashier | SHIPPED | `IntegrationEndpoints.cs` |
| LP-019 | Checkout links CRUD | SHIPPED | `ProductEndpoints.cs`, `ProductsPage.tsx` |
| LP-020 | One-time + monthly + yearly | SHIPPED | `CreateProductForm.tsx` intervals |
| LP-021 | Trials | ABSENT | — |
| LP-022 | Subscription activate/renew/cancel | SHIPPED | `Subscription.cs`, engines, portal cancel |
| LP-023 | Pause subscription | ABSENT | dunning pause only |
| LP-024 | Upgrade / proration | ABSENT | — |
| LP-025 | Usage-based subscriptions | ABSENT | — |
| LP-026 | Dunning campaign builder | SHIPPED | `DunningCampaignEndpoints.cs` + builder pages |
| LP-027 | Dunning engine (email + catch-up) | SHIPPED | `DunningEngineJob*.cs` |
| LP-028 | Off-session auto-charge | PARTIAL | Stripe/CHIP/Razorpay yes; Billplz no |
| LP-029 | Magic update-payment links | SHIPPED | `PublicArrearsEndpoints.cs`, portal page |
| LP-030 | Failed payment → PAST_DUE | SHIPPED | `GatewayPaymentFailedIntegrationEventHandler.cs` |
| LP-031 | Resend BYOK email | SHIPPED | `EmailSettingsPage.tsx`, `ResendEmailService.cs` |
| LP-032 | Notification templates | SHIPPED | `TemplatesPage.tsx`, `DefaultMessageTemplates.cs` |
| LP-033 | WhatsApp dunning | SCAFFOLD | `ConsoleMessagingService.cs`, flag false |
| LP-034 | Suppression / unsubscribe / bounce | SHIPPED | `PublicComplianceEndpoints.cs` |
| LP-035 | Broadcasts | PARTIAL | API + job; no nav |
| LP-036 | Double-entry ledger | SHIPPED | `GatewayPaymentCompletedHandler.cs` |
| LP-037 | Dashboard net cash / stats | SHIPPED | `DashboardPage.tsx`, `GetStatsAsync` |
| LP-038 | Receipt PDF generation | BACKEND-ONLY | QuestPDF + R2; buyer button hidden |
| LP-039 | B2C consolidation job | BACKEND-ONLY | `B2cConsolidationJob.cs` |
| LP-040 | Deferred revenue recognition | SCAFFOLD | unregistered job |
| LP-041 | Utility credit wallet + top-up | PARTIAL | APIs + hidden routes `/workspace/billing|ledger` |
| LP-042 | LHDN submit/status/cancel API | BACKEND-ONLY | `DocumentEndpoints.cs` (integrator-ready) |
| LP-043 | LHDN TIN validate | BACKEND-ONLY | `POST /lhdn/taxpayer/validate` |
| LP-044 | LHDN credit/debit/refund notes | BACKEND-ONLY | `DocumentStrategyFactory.cs` |
| LP-045 | LHDN XAdES signing | ABSENT | module README |
| LP-046 | LHDN merchant UI | ABSENT (hidden) | ADR 023 |
| LP-047 | Quotes / tax invoices / credit notes UI | BACKEND-ONLY | unrouted `modules/invoicing/*` |
| LP-048 | Outbound webhooks + signing | SHIPPED | `WebhookEndpoints.cs`, dispatcher |
| LP-049 | Webhook delivery logs | SHIPPED | `DeliveryLogsPage.tsx` |
| LP-050 | Inbound webhook idempotency | SHIPPED | business key + event id |
| LP-051 | Integrator provision (Aura) | SHIPPED | `IntegrationProvisionEndpoints.cs` |
| LP-052 | CRM directory | BACKEND-ONLY | no HTTP; internal PII |
| LP-053 | PDPA anonymize | BACKEND-ONLY | command + events |
| LP-054 | Developer hub + guides + sample | SHIPPED | developers + docs + example |
| LP-055 | LHDN TS/.NET SDKs | SHIPPED | `packages/lhdn-sdk-*` |
| LP-056 | Platform admin console | PARTIAL | gateways only |
| LP-057 | Stripe refunds | SHIPPED | transactions refund endpoint |
| LP-058 | Billplz refunds | ABSENT | adapter returns false |
| LP-059 | Dispute → commerce cancel | ABSENT | utility clawback only |
| LP-060 | Legal pages (PDPA/MoR) | SHIPPED | portal `/legal/*` |
| LP-061 | Merchant tax profile UI | BACKEND-ONLY | hidden BillingProfile |
| LP-062 | Outbox/inbox + workers | SHIPPED | per-module jobs |
| LP-063 | Ops AI chat | BACKEND-ONLY | `/ops` endpoints; UI hidden |
| LP-064 | Community / Vault | ABSENT | deleted from API |
| LP-065 | Xero / QuickBooks | ABSENT | ADR keep, no code |
| LP-066 | Escrow / e-sign / Keygen / Wise / Web3 | ABSENT | ADR 020 wishlist |
| LP-067 | GSTN / Coretax | ABSENT | — |
| LP-068 | Forgot-password + verify-email UX | BACKEND-ONLY | APIs only |
| LP-069 | Workspace billing nav (credits) | PARTIAL | routed, not in sidebar |

---

## Appendix: file index

### Apps

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/README.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/TODO.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/App.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/components/Sidebar.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/components/LoginPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/lib/utils.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/commerce/pages/DashboardPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/commerce/pages/ProductsPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/commerce/pages/SubscribersPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/commerce/pages/TransactionsPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/commerce/pages/CouponsPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/commerce/pages/DunningCampaignsPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/commerce/pages/CampaignBuilderPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/commerce/pages/TemplatesPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/commerce/components/CreateProductForm.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/ApiKeysPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/DeveloperSettingsPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/DeliveryLogsPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/GeneralSettingsPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/PaymentSettingsPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/EmailSettingsPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/BillingSettingsPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/UtilityLedgerPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/BillingProfilePage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/invoicing/pages/QuotesPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/invoicing/pages/TaxInvoicesPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/invoicing/pages/CreditNotesPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/page.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/page.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/[tenantSlug]/pay/[sessionId]/page.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/[tenantSlug]/update-payment/[subId]/page.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/legal/privacy/page.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/legal/terms/page.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/legal/refund/page.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-admin/src/App.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-admin/src/components/Sidebar.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-admin/src/modules/platform/pages/PlatformPaymentSettingsPage.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-developers/app/page.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/index.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/guide/product-lines.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/examples/hub-cashier-next/README.md`

### API host

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Program.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/appsettings.json`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Composition/ModuleRegistrationExtensions.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Middleware/TenantSecurityMiddleware.cs`

### One

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/PlatformApiScopes.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/ApiCredentialEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WebhookEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/IntegrationProvisionEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/PlatformAuthEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/ProfileEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/StorageEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookDispatcherJob.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookSignature.cs`

### Payments

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Endpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/IntegrationEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/PlatformEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/PaymentGatewayFactory.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/StripeGatewayAdapter.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/ChipCollectGatewayAdapter.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/RazorpayGatewayAdapter.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/ExecuteOffSessionChargeIntegrationEventHandler.cs`

### Commerce

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/ProductEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/SubscriberEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicCheckoutEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicPortalEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicArrearsEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicCustomCheckoutEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/CouponEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/DunningCampaignEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/TransactionEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/StatsEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Product.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Subscription.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/Aggregates/DunningCampaign.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/ChargeAttemptLimits.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/DunningEngineJob.PastDue.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentFailedIntegrationEventHandler.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Stats.cs`

### Billing

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/README.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Domain/AccountTypes.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints/AdminLedgerEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints/AdminCreditsEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints/AdminProfileEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints/PublicBillingEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ChargebackClawbackHandler.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/Workers/RevenueRecognitionJob.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/Services/CreditCostService.cs`

### LHDN

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/README.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Endpoints/DocumentEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Endpoints/TenantConfigEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Endpoints/AdminApiKeyEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Endpoints/AdminWebhookEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/DocumentStrategyFactory.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Lhdn/Infrastructure/Workers/LhdnSubmissionJob.cs`

### Communications / Messaging / CRM / Ops

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints/TemplateEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints/BroadcastEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints/PublicComplianceEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Communications/Domain/DefaultMessageTemplates.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Messaging/Infrastructure/Endpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Messaging/Infrastructure/Messaging/ConsoleMessagingService.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Messaging/Infrastructure/Email/ResendEmailService.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/CRM/README.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Ops/Infrastructure/Endpoints.cs`

### Packages / contracts / docs / plans

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/main.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/honesty-allowlist.yaml`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/lhdn-sdk-ts/src/`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/lhdn-sdk-dotnet/src/`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/019-checkout-as-a-service-pivot.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/021-compliance-caas-pivot.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/022-remove-community-vault-modules.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/023-pure-caas-mvp-ui-lobotomy.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/001-gaps/README.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/001-gaps/00-what-we-need-to-do-next.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/001-gaps/21-phase-c-acceptance-notes.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/contracts/openapi-vs-minimal-api.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/004-maintenance/decisions.md`

### Tests (evidence that money/auth loops are not untested)

Under `apps/lazuar-api/tests/Lazuar.ModuleTests/`: Billing ledger/refund/top-up/chargeback/consolidation; Commerce coupons, dunning domain, payment-failed, recovery, billing engine; Communications templates/suppression/broadcasts; Lhdn credits/auth/sandbox; Messaging Resend/WA skip; One keys/webhooks/provision; Payments webhook/off-session/integration checkout; Tenant isolation. Plus `Lazuar.IntegrationTests`, `Lazuar.ArchitectureTests`, `Modules.Billing.Tests`, `Modules.Ops.Tests`.

---

*End of ground-truth inventory. Parent program should treat this file as the uncondensed source; do not collapse statuses without re-opening the cited paths.*
