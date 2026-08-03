<!-- Source subagent: 019fc650-3515-7c20-8652-51523d72773d -->
<!-- Full uncondensed subagent analysis — do not summarize -->

# Frontend–Backend Integration Gap Analysis

Cross-cutting read of `apps/ops-page/src/`, `apps/portal-page/src/`, `apps/superadmin-page/src/`, `packages/api-types-ts/`, and matching backend routes under `apps/lazuar-api/Modules/`. Focus: what exists in UI vs what the API/contracts expose, especially credentials, webhooks, dunning, and auth.

---

## Ops Page Capabilities vs Backend

### Integration model (solid base)

| Concern | Implementation |
|---|---|
| Typed client | `openapi-fetch` + `@repo/api-types-ts` in `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/ops-page/src/lib/api-client.ts` |
| Auth | Cookie session (`credentials: "include"`); JWT is **not** in `localStorage` |
| Tenant context | `localStorage.ops_active_workspace_id` → `X-Tenant-Id` on non-`/one/` requests |
| Auth APIs | `GET /one/auth/me`, `POST /one/auth/login`, `POST /one/auth/logout`, `GET /one/me/entitlements` |

### Present and wired (good coverage)

| Area | UI | Backend / OpenAPI |
|---|---|---|
| Commerce dashboard | `DashboardPage` | `/admin/commerce/stats`, `/admin/billing/summary` |
| Products / coupons / transactions | Full CRUD-ish UI | `/admin/commerce/products*`, `coupons*`, `transactions` |
| Payment BYOK (tenant) | `PaymentSettingsPage` + modals | `GET/PUT /admin/commerce/payment-config` |
| Email BYOK (Resend) | `EmailSettingsPage` | `GET/PUT /admin/communications/email-config` |
| Outbound webhooks | `DeveloperSettingsPage` | `GET/PUT /one/workspaces/{id}/webhooks` |
| Webhook delivery logs | `DeliveryLogsPage` | `GET /one/workspaces/{id}/webhooks/logs` |
| Dunning campaigns | list / builder / defaults | full `/admin/commerce/dunning-campaigns*` |
| Per-subscriber dunning pause/resume | `SubscribersPage` | `POST .../dunning/pause\|resume` |
| Templates | `TemplatesPage` | `/admin/communications/templates*` |
| Workspace general | `GeneralSettingsPage` | `PUT /one/workspaces/{id}` |
| Credits top-up | `BillingSettingsPage` (routed) | `/admin/billing/credits*` |
| Utility ledger | `UtilityLedgerPage` (routed) | `/admin/billing/credits` |

### Routed vs discoverable (integration model friction)

`App.tsx` routes more than the sidebar exposes:

- **Routed but not in sidebar:** `/workspace/billing`, `/workspace/ledger`
- **Implemented modules with no routes:** entire `modules/invoicing/*` (quotes, tax invoices, credit notes), `BillingProfilePage`, Ops chat (`OpsChatWorkspace`, `ConversationsDirectory`)
- **Login redirect bug:** after login/signup, hard-navigates to `/community/dashboard`, which is **not** a route → falls through to `*` → `/commerce/dashboard` only if user is already inside the app shell; full page load to a missing path is brittle

Sidebar only lists Commerce, Developer (webhooks + logs), Workspace (general, payment gateways, email). Billing/ledger/profile/invoicing/team/LHDN/API keys are invisible.

### Ops UI vs real backend (broken or phantom APIs)

These UIs call endpoints that **do not exist** on Commerce (or anywhere in OpenAPI):

| UI action | Frontend call | Backend reality |
|---|---|---|
| Cancel subscription | `POST /admin/commerce/subscribers/{id}/cancel` via dynamic `as any` | **Missing** — only pause/resume dunning + list/create |
| Ban user | `.../ban` | **Missing** |
| Log offline payment | `.../record-payment` | **Missing** |
| Refund (subscribers) | `.../refund` | **Missing** |
| Refund (transactions) | `POST /admin/community/subscribers/{id}/refund` with dummy UUID | **Missing**; code comments admit this |
| Product associations | `GET /admin/community/spaces`, `/admin/vault/assets` | **Not in OpenAPI / Commerce** |

Evidence: `SubscriberEndpoints.cs` only maps list, create, portal-link, dunning pause/resume. No cancel/ban/record-payment/refund.

### Contract skew on “working” endpoints

**Manual enroll** (`CreateSubscriberModal.tsx`):

- Sends: `plan_id`, `source`, `is_reminder_only`, … with `as any`
- OpenAPI `Commerce.CreateManualSubscriberDto` expects: `product_id`, `name`, `email`, `phone`, `payment_method`, `amount_paid`, …
- Backend `CreateManualSubscriberRequest` expects `Product_id`, not `plan_id`

This is a **high-severity** solidification blocker: UI payload ≠ contract ≠ handler binding.

**LHDN cancel** (`TaxInvoiceDetailPanel.tsx`):

- Calls `POST /api/v1/lhdn/documents/{internalId}/cancel` with `as any`
- Client `baseUrl` is already `.../api/v1`
- Backend group is `/lhdn` under `/api/v1` → correct path should be `/lhdn/documents/{id}/cancel`
- OpenAPI paths are wrongly prefixed `/api/v1/lhdn/...` (double-prefix trap when used with openapi-fetch baseUrl)

**Portal-link admin API:** backend `POST /admin/commerce/subscribers/portal-link` exists but is **absent from OpenAPI** and has **no Ops UI**.

### Ops chat (orphaned integration)

Components + `use-chat-stream` exist and hit:

- Typed: `/ops/chat/conversations*`, `/ops/execute-action`, resolve
- Untyped raw `fetch`: `/ops/chat/stream`, `/ops/chat/conversations/{id}/system-message`

Backend implements stream + system-message; OpenAPI omits them. **No route** mounts chat UI in `App.tsx`.

---

## Portal Page Capabilities

### Architecture

| Layer | File | Pattern |
|---|---|---|
| SSR client | `modules/core/lib/server-client.ts` | Forwards `lazuar_auth` cookie from Next `cookies()` |
| Browser client | `modules/checkout/lib/api.ts`, `community/lib/api.ts` | `credentials: "include"` + openapi-fetch |
| Auth for portal | Query `?token=` (magic/checkout token), optional session cookie |

**No JWT in localStorage** on portal either. Access token after checkout is passed in the URL query string (`CheckoutSuccessView` → `portal?token=...`).

### Wired public flows

| Flow | Routes | APIs |
|---|---|---|
| Product checkout | `/[tenantSlug]/checkout/[productSlug]` | `GET products/{slug}`, `POST /public/commerce/checkout`, coupon validate, status poll |
| Checkout success | `.../success` | `GET /public/commerce/checkout/{subId}/status` (+ optional token) |
| Custom quote pay | `/[tenantSlug]/pay/[sessionId]` | custom-checkouts + public billing profile |
| Update payment (dunning recovery) | `/[tenantSlug]/update-payment/[subId]` | arrears + `update-payment` |
| Customer portal | `/[tenantSlug]/portal` | portal GET + cancel |
| Legal | privacy / terms / refund | static |

### Gaps (portal vs backend)

| Capability | Backend OpenAPI | Portal UI |
|---|---|---|
| Magic link request | `POST .../portal/magic-link` | **No form** — page only tells user to use email link |
| Billing portal link | `POST .../portal/billing-link` | **Unused** |
| Tax invoice download | commented `[MVP-HIDE]` + draft URL only for quotes | Incomplete |
| Update payment when not past due | Links to portal without token | May 404 / empty if no cookie |
| Community portal view | Component exists | Not clearly mounted as primary portal page (main portal is server page list + cancel) |
| Forgot/reset password / profile | One auth APIs | **No UI** |
| Guest vs logged-in consistency | `GET /one/auth/me` on checkout | Portal cancel with empty token if cookie-only path is inconsistent |

Portal is a **thin public surface**: checkout + recovery + minimal cancel. It does not participate in API-key, webhook, or BYOK management (correct for tenant customers), but customer self-serve auth (magic-link request UI) is incomplete relative to the API.

---

## Superadmin Page

### Scope today

Extremely narrow:

| Route | API |
|---|---|
| `/login` | `POST /platform/auth/login` |
| Session gate | `GET /platform/auth/me` |
| Logout | `POST /platform/auth/logout` |
| `/platform/gateways` | `GET/PUT /platform/payment-config` |

Auth cookie: backend uses **`lazuar_admin_auth`** (distinct from ops `lazuar_auth`). Client correctly uses `credentials: "include"` only; no token storage.

### Gaps vs platform/backend surface

- **Only** platform payment BYOK UI
- No platform user management, tenant admin, credit grants, feature flags, observability, LHDN global settings, or audit
- Payment settings page is a near-clone of ops `PaymentSettingsPage` (duplicated forms, `any[]` configs) rather than a shared package
- No use of admin billing/ledger paths (those are tenant-scoped under `/admin/billing/*` with `X-Tenant-Id`, not true superadmin multi-tenant tooling)

Superadmin does **not** block solidifying the tenant integration model, but it does **not** provide a control plane for platform-level credentials beyond gateways.

---

## Auth Token Handling Patterns

| App | Auth mechanism | Storage | Notes |
|---|---|---|---|
| **ops-page** | HttpOnly cookie `lazuar_auth` | Cookie only | `localStorage` holds **workspace id** + UI prefs, **not** JWT |
| **superadmin-page** | HttpOnly cookie `lazuar_admin_auth` | Cookie only | Same pattern, different cookie name |
| **portal-page (SSR)** | Forwards `lazuar_auth` if present | Cookie on API domain | Server-side only |
| **portal-page (customer)** | Portal **token query param** | URL / memory | Post-checkout token not persisted as JWT in browser storage |
| **Raw chat stream** | Cookie + `X-Tenant-Id` header | — | Bypasses openapi-fetch middleware for stream |

### Strengths

- Correct browser model for SPA + cross-origin API: cookie + `credentials: "include"` + CORS
- JWT never written to `localStorage` / `sessionStorage` (good for XSS surface)
- Platform vs org cookie separation is intentional and implemented in JWT bearer `OnMessageReceived`

### Gaps / risks for a solid model

1. **Tenant id in localStorage** is authoritative for `X-Tenant-Id`; backend must re-validate membership (middleware exists for admin paths — still a client trust boundary to document)
2. **No shared auth package** — three reimplemented clients with slight differences
3. **Portal token in query string** can leak via Referer / logs; no refresh / expiry UX
4. **Missing UX for** forgot-password, reset-password, verify-email, resend-verification (APIs exist under `/one/auth/*`)
5. **No password change / profile UI** (`/one/me/profile`, `/one/me/security/password`)
6. **Workspace members/invites** APIs exist; zero Ops UI → team onboarding is API-only
7. Login success path uses `window.location.href` full reload (ok for cookie set) but wrong default path on ops

---

## Missing Integration Management UIs (API keys, webhooks, credentials)

### Present

| Integration | Where | Status |
|---|---|---|
| **Payment gateway BYOK** (CHIP, Billplz, Stripe, Razorpay) | Ops `PaymentSettingsPage`; Superadmin platform twin | **Present** — multi-gateway vault |
| **Resend email API key** | Ops `EmailSettingsPage` | **Present** |
| **Outbound commerce webhooks** (single endpoint + secret display) | Ops Developer settings | **Present** (single URL, not multi-endpoint) |
| **Webhook delivery logs** | Ops Delivery logs | **Present** (list only; no retry/replay/detail payload viewer) |

### Missing / incomplete (blocks “solid” integration model)

| Integration | Backend / OpenAPI | Frontend |
|---|---|---|
| **LHDN API keys** (`list/generate/revoke`) | `/lhdn/api-keys` (+ OpenAPI under wrong `/api/v1/lhdn/...` prefix) | **No UI** |
| **LHDN inbound webhooks** (register/list/delete) | `/lhdn/webhooks` | **No UI** |
| **LHDN certificate upload** | `PUT .../lhdn-certificate` | **No UI** |
| **Workspace LHDN / MyInvois config** | `one/workspaces/{id}/lhdn-config` (secret + env + TIN) | **No UI** |
| **Billing profile** (tax identity for invoices) | `GET/PUT /admin/billing/profile` | Page exists, **not routed / not in nav** |
| **Multi webhook endpoints / event filters** | One workspace webhook only | Single URL; no event-type picker |
| **Webhook secret rotation** | Secret returned on GET | Display + copy only; no rotate endpoint/UI |
| **API keys for external Commerce/Ops SDK** (non-LHDN) | LHDN-only API_CLIENT style keys | No general “Developer API keys” product surface |
| **Admin portal-link generation** | Backend exists | No “Send customer portal link” button |
| **Presigned storage** | Used only inside unrouted billing profile | Partial |
| **Credential masking UX** | APIs may return redacted secrets | Forms re-save password fields; Billplz allows `••••` length skip inconsistently |
| **Delete / disconnect gateway** | PUT-only config | No clear “remove credentials” |
| **Test connection / dry-run webhook** | Not in OpenAPI | No ping button |
| **Team credentials / members** | invites, members | No UI |

### Duplicated / dead payment UI

- `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/ops-page/src/components/PaymentSettingsModal.tsx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/ops-page/src/modules/workspace/components/PaymentSettingsModal.tsx`
- Page version at `PaymentSettingsPage.tsx`

Three near-copies → integration model drift risk (field mapping `collection_id` vs `merchant_id` already mixed in local state updates).

---

## Dunning UI Completeness vs Backend Flexibility

### Backend flexibility (domain + API)

Supported model:

- Campaign: name, active, final action (`CANCEL` / `SUSPEND` / `NONE`), grace days, priority, product targets, payment-method targets, steps
- Step: `day_offset`, `action_type` ∈ `EMAIL | WHATSAPP | AUTO_CHARGE`, subject/email/whatsapp bodies
- Defaults seed: `POST /dunning-campaigns/defaults`
- Per-subscription: pause until datetime, resume
- Engine job drives recovery metrics (`recovered_revenue`, `saved_subscriptions`, `churned_subscriptions`)

### UI completeness (strong for campaign design)

Ops covers:

- List + metrics columns
- Deploy recommended strategy empty-state
- Full builder: identity, priority, product multi-select, payment method toggles (`ONLINE_GATEWAY`, `MANUAL`), terminal action, grace period
- Timeline with EMAIL / WHATSAPP / AUTO_CHARGE and template live preview via communications preview API
- Subscriber panel shows campaign name, current step, pause/resume

### Dunning gaps

| Gap | Detail |
|---|---|
| **No GET by id** | Builder loads **all** campaigns then `find` by id — fine for small N, not solid for scale |
| **Create payload extra field** | UI sends `is_active` on create; `CreateDunningCampaignRequestDto` has no `is_active` (always active server-side) |
| **Local types not from api-types** | `LocalStepState` hand-rolled strings instead of `Commerce.DunningStepDto` |
| **Products typed as `any[]`** in settings panel | Weakens targeting correctness |
| **No assignment override UI** | Cannot pin a specific campaign to a subscription beyond product/method targeting |
| **No simulation / dry-run** | Cannot preview schedule for a given sub |
| **No step-level analytics** | Only campaign-level recovered/saved/churned |
| **WhatsApp depends on credits + messaging** | Billing page mentions WhatsApp dunning credits; no UI link between dunning steps and credit balance / provider health |
| **Portal recovery path** | `update-payment` exists; dunning emails must deep-link correctly (backend builds cancel/update URLs) — no ops preview of customer-facing link |
| **Subscriber ops incomplete** | Pause works; cancel/ban/record-payment UI implies recovery workflow but backend missing → ops cannot fully close the loop after dunning |
| **AUTO_CHARGE** | UI exposes it; depends on vaulted tokens (`vaulted_token_id` shown on subscribers) — no management of vault / payment method from ops |

**Verdict:** Campaign configuration UI is close to backend flexibility. **Operational dunning** (subscriber lifecycle actions, recovery payment logging, refunds) is **not** backed by Commerce APIs → largest gap for “solidifying” the model.

---

## Type Safety from `api-types-ts`

### What works

- Single generated `paths` + `components` package consumed by all three apps
- openapi-fetch gives compile-time path/method checking **when** callers avoid `as any`
- Named schema types used well in places (`AuthUser`, `EntitlementDto`, `WebhookEndpointDto`, portal DTOs)

### Structural issues blocking a solid integration model

1. **LHDN path prefix bug in OpenAPI**  
   Paths like `/api/v1/lhdn/api-keys` while client base is `/api/v1` and runtime routes are `/api/v1/lhdn/...` only if the generated path is `/lhdn/...`. Current generation forces clients to either double-prefix or cast away types (`as any` on cancel).

2. **OpenAPI lag behind backend**  
   Missing from types / present in code:
   - `POST /ops/chat/stream`
   - `POST /ops/chat/conversations/{id}/system-message`
   - `POST /admin/commerce/subscribers/portal-link`
   - Resolve may be PUT in backend vs generated operation shape

3. **Phantom frontend paths not in types** (correctly rejected by TS — bypassed with `as any`):
   - `/admin/commerce/subscribers/{id}/cancel|ban|record-payment|refund`
   - `/admin/community/*`, `/admin/vault/*`

4. **`components` re-export**  
   Pages import `type components` from `api-client`, but `api-client` only re-exports named types — **does not re-export `components`**. That is a packaging smell (may work via transitive/path quirks or be broken).

5. **Widespread `any`**  
   Payment configs `any[]`, gateway type `any`, subscriber action payloads, CreateSubscriber body, association hooks.

6. **Create subscriber contract mismatch**  
   Types define `CreateManualSubscriberDto` with `product_id`; UI still uses legacy `plan_id` / `source` / `is_reminder_only` under `as any` — types not enforcing the real contract.

7. **No frontend package boundaries for domains**  
   Everything goes through one mega `paths` client; no thin modules (`commerceApi`, `oneApi`) that encode tenant header rules per surface.

8. **Generate pipeline**  
   `packages/api-types-ts` generates from `../api-spec/dist/openapi.yaml` — solidifying requires TypeSpec + backend endpoint parity review, especially LHDN servers/base paths and Ops streaming.

---

## Recommendations

### P0 — Contract truth (must fix to solidify integration model)

1. **Align manual subscriber create** with `CreateManualSubscriberDto` / backend (`product_id`, drop dead fields). Remove `as any`.
2. **Either implement or remove** subscriber cancel / ban / record-payment / refund APIs; stop shipping UI against imaginary routes.
3. **Add real refund endpoint** (e.g. `POST /admin/commerce/transactions/{id}/refund`) and wire both Subscribers + Transactions panels; delete `/admin/community/...` stubs.
4. **Fix LHDN OpenAPI paths** to `/lhdn/...` (relative to `/api/v1`) and regenerate `api-types-ts`; fix cancel call accordingly.
5. **Regenerate OpenAPI** for portal-link, ops stream, system-message; ban untyped `fetch` except SSE if stream cannot be represented cleanly.

### P1 — Integration management surface (ops)

6. Ship **Developer → API Keys (LHDN)** UI: list / generate (show plaintext once) / revoke.
7. Ship **LHDN webhooks + certificate + MyInvois config + billing profile** under Workspace or Compliance nav.
8. Route and nav-link: Billing profile, Utility ledger, Credits, Invoicing module if product-ready.
9. Webhook UX: payload preview, retry, last delivery detail, event catalog docs link.
10. Payment vault: shared component package; mask secrets; explicit disconnect; optional “test credentials”.

### P1 — Dunning operational loop

11. Keep campaign builder; add GET-by-id if list grows.
12. Add ops actions that match engine outcomes: cancel/suspend manual, generate update-payment link, portal-link.
13. Surface credit/WhatsApp readiness when campaign contains WHATSAPP steps.

### P2 — Auth & multi-app consistency

14. Shared `@repo/api-client` (or similar): base URL, cookie credentials, tenant middleware, error mapping.
15. Ops login default → `/commerce/dashboard`; add forgot/reset password pages if product needs them.
16. Portal magic-link request form calling `portal/magic-link`.
17. Members/invites UI before scaling multi-user tenants.

### P2 — Type safety hygiene

18. Zero `as any` on HTTP paths; prefer openapi-fetch path unions.
19. Re-export `components` / domain types from api-client deliberately.
20. Delete or route dead modules (chat, invoicing, duplicate modals) so “frontend = product contract” is true.

### Superadmin

21. Keep platform payment config; defer broader admin until tenant model is solid.
22. Share gateway form with ops via package to avoid third fork.

---

## File Evidence Notes

### Clients & auth

| Path | Role |
|---|---|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/ops-page/src/lib/api-client.ts` | openapi-fetch + `X-Tenant-Id` from localStorage |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/superadmin-page/src/lib/api-client.ts` | cookie-only platform client |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/portal-page/src/modules/core/lib/server-client.ts` | SSR cookie forward `lazuar_auth` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/portal-page/src/modules/checkout/lib/api.ts` | browser public commerce client |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/One/Infrastructure/Endpoints.cs` | issues `lazuar_auth` HttpOnly |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Payments/Infrastructure/PlatformEndpoints.cs` | issues `lazuar_admin_auth` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/src/Lazuar.Api/Program.cs` | cookie→JWT bearer by route prefix |

### Integration UIs (present)

| Path | Role |
|---|---|
| `.../ops-page/src/modules/workspace/pages/PaymentSettingsPage.tsx` | tenant payment BYOK |
| `.../ops-page/src/modules/workspace/pages/EmailSettingsPage.tsx` | Resend BYOK |
| `.../ops-page/src/modules/workspace/pages/DeveloperSettingsPage.tsx` | outbound webhooks |
| `.../ops-page/src/modules/workspace/pages/DeliveryLogsPage.tsx` | webhook logs |
| `.../superadmin-page/src/modules/platform/pages/PlatformPaymentSettingsPage.tsx` | platform gateways |
| `.../ops-page/src/modules/commerce/pages/DunningCampaignsPage.tsx` | campaign list/defaults |
| `.../ops-page/src/modules/commerce/pages/CampaignBuilderPage.tsx` | campaign CRUD |
| `.../ops-page/src/modules/commerce/components/dunning/*` | timeline / steps / settings |
| `.../ops-page/src/modules/commerce/pages/SubscribersPage.tsx` | dunning pause + phantom actions |

### Gaps / phantoms

| Path | Issue |
|---|---|
| `CreateSubscriberModal.tsx` | wrong body fields + `as any` |
| `TransactionDetailPanel.tsx` | dummy community refund route |
| `use-product-associations.ts` | non-existent community/vault APIs |
| `TaxInvoiceDetailPanel.tsx` | LHDN path double-prefix + `as any` |
| `use-chat-stream.ts` | untyped stream path not in OpenAPI |
| `App.tsx` (ops) | missing routes for invoicing, billing profile, chat; login targets wrong path |
| `Sidebar.tsx` (ops) | incomplete nav for billing/integration surfaces |
| `modules/invoicing/*`, `BillingProfilePage.tsx` | implemented, unrouted |
| `portal/.../portal/page.tsx` | no magic-link request UI |
| Backend `SubscriberEndpoints.cs` | no cancel/ban/refund/record-payment |
| `packages/api-types-ts/src/index.ts` | LHDN paths under `/api/v1/lhdn/...`; missing stream/portal-link |

### Types package

| Path | Role |
|---|---|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/packages/api-types-ts/src/index.ts` | generated `paths` / schemas |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/packages/api-types-ts/package.json` | `openapi-typescript` from `api-spec/dist/openapi.yaml` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/packages/api-spec/` | TypeSpec source of truth |

---

### Executive summary

- **Auth model is already sound** for solidification: HttpOnly JWT cookies, no localStorage tokens, tenant via header + cookie session.
- **BYOK for payments + email and outbound webhooks exist in Ops**; **LHDN API keys, LHDN webhooks, certificates, MyInvois config, and general developer API keys do not**.
- **Dunning campaign design is largely complete** vs backend; **subscriber recovery operations in the UI are largely fictional**.
- **Largest blockers to a solid frontend–backend integration model:** phantom subscriber/refund/community routes, create-subscriber contract drift, LHDN OpenAPI base-path errors, unrouted modules, OpenAPI lag (stream/portal-link), and overuse of `as any` that hides all of the above.
