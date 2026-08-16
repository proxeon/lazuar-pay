# W1-LP-006 — Public self-serve signup + pricing page

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 1 row `LP-006` (“Public signup + pricing page”). Tracker label in [00-checklist-tracker.md](../00-checklist-tracker.md) is “Public self-serve signup + pricing page” (Wave **1**, Lazuar **N**). Wave-1 cluster in the same tracker: `LP-006, LP-183, LP-184` = “Pricing + self-serve time-to-first-link”.  
**Evidence (do not reopen as product strategy):** [18-pricing-onboarding-trust.md](../18-pricing-onboarding-trust.md) Path S + commercial packaging; [00-evaluation.md](../00-evaluation.md) §4 “No public pricing page in this repo”; [01-lazuar-feature-inventory.md](../01-lazuar-feature-inventory.md) identity table; [docs/001-gaps/10-one-identity-module.md](../../../docs/001-gaps/10-one-identity-module.md).

**Not this ID.** Three other files reuse `LP-006` for unrelated rows. Ignore those meanings when implementing this ticket:

| File | That file’s `LP-006` | What to do |
|------|----------------------|------------|
| [01-lazuar-feature-inventory.md](../01-lazuar-feature-inventory.md) | Platform API keys (SHIPPED) | That is tracker **LP-131**. Keys already have ops UI. |
| [18-pricing-onboarding-trust.md](../18-pricing-onboarding-trust.md) | 0% platform take on GMV (shipped / lock) | That lock is tracker **LP-001** (BYOK). Protect it; do not add `applicationFee`. |
| Same file’s `LP-001` | Public pricing page (0% GMV + credit table + SST footnote) | **Same work as this ticket.** Report-18 minted a parallel `LP-*` family; the living tracker absorbed the *job* under Wave-1 `LP-006`. |

**This ticket is not** LP-004 (real SaaS / Hub Pro SKU), LP-005 (prepaid credits consume on LHDN/WA), LP-007 (KYC — refuse), LP-183 (time-to-first-checkout wizard / BYOK paste), LP-184 (second workspace after you already have one), LP-182 (sandbox honesty), or report-18 LP-024 (merchant TOS + DPA). Adjacent holes are listed only so implementers do not “fix” them here.

**Invariant:** A stranger on `hub.lazuar.com` can read an honest price card and create a workspace **without a superadmin queue**. Guest GMV is never taxed. Do not invent a 5% number “to look normal.”

---

## 0. Scope lock

In scope:

- Public, unauthenticated **pricing** page (0% GMV + credit packs + SST footnote + what is *not* sold)
- Public, unauthenticated **signup** that lands in ops with a cookie
- Reuse of `POST /api/v1/one/public/register` so the first workspace exists immediately
- Tests that lock: no approval queue, duplicate email / taken slug, reserved slug, entitlements, pricing copy honesty

Out of scope (do not expand this ticket):

- Building `AppAccessRequest` / `one.AppAccessRequests` / any superadmin approval inbox
- Hub Pro monthly SKU (LP-004)
- Un-hiding LHDN / WhatsApp / Billing Profile / quotes
- In-app “gateway → test pay” empty state (report-18 LP-015 / tracker LP-183)
- Email-verify gate, forgot-password UI, 2FA, CAPTCHA vendor
- Merchant DPA / AUP PDF (report-18 LP-024)
- National KYC / Singpass (refuse)
- Integrator provision (`POST /one/integrations/workspaces/provision`)
- A new marketing site / Astro / `www` app
- Portal buyer landing rewrite (lock-icon page stays for buyers)
- Per-checkout credit tax (report-18 LP-005 — refuse)

**Dependency (do not implement here):** cookie JWT + CORS already work for ops login. Pricing/signup must stay on the **ops origin** (`hub.lazuar.com` `/`) so `lazuar_auth` + `credentials: "include"` keep working. Do not send first-session signup through `portal` or `docs`.

---

## 1. Verdict

**Signup-the-API is shipped. Signup-the-product is not. Pricing is absent. There is no superadmin queue to remove.**

Tracker **N** is correct. The backend already does the hard part (user + workspace + ADMIN membership + core entitlements + cookie, no approval). What a stranger cannot do today is: land on a public URL, see what Lazuar costs, and start. Ops `/login` defaults to **Sign in**; “Sign up” is a toggle with no pricing, no TOS, no deep link. Portal `/` is a buyer lock icon. Docs are integrator guides. Credit packs live behind authenticated `GET /admin/billing/credits/packages`.

`AppAccessRequest` is **README fiction**. Domain, `OneDbContext`, and every One migration omit it. Do **not** implement the queue “to match the README.” Instant workspace is already the product.

| Layer | Status |
|-------|--------|
| `POST /one/public/register` → user + org + ADMIN + 5 entitlements + cookie | **Y** — no queue |
| Ops login page signup mode | **Y** as a hidden toggle — not a public funnel |
| Additional workspace (`POST /one/workspaces` + modal) | **Y** — this is tracker **LP-184** (`P`, not `N`) |
| Superadmin approval / `AppAccessRequest` | **Does not exist** — do not add |
| Public price card (0% GMV + packs + SST) | **N** |
| Clickwrap TOS/Privacy on register | **N** |
| Rate limit / CAPTCHA on register | **N** |
| Handler / slug tests for register | **N** (One has other tests; this path has none) |

**LP-006 is a public surface on top of an existing command.** Do not rebuild identity. Do not invent a sales-led queue.

---

## 2. Product contract for this ID

Sellable sentence after this ticket:

> A stranger opens `hub.lazuar.com/pricing`, sees **RM 0 on your sales**, the credit packs, and an SST footnote, clicks **Create workspace**, and is in ops on a live workspace **without waiting for a human**. Checkout itself is free software today. Credits are for LHDN / WhatsApp when those products are on; they are not a GMV tax.

| Input | Result |
|-------|--------|
| `GET /pricing` (unauthenticated) | Honest card. CTA → `/signup`. No login required. |
| `GET /signup` or `/login?mode=signup` | Same form as today’s signup mode. Workspace name + slug + email + password. |
| Valid `POST /one/public/register` | HTTP 200 + `lazuar_auth` cookie + workspace exists + role in body `ADMIN` + browser → `/commerce/dashboard` |
| Duplicate email | HTTP 400 `invalid_operation` — “already exists.” No user/org written. |
| Taken / reserved / malformed slug | HTTP 400 (`invalid_operation` or `business_rule_violation`). No user written. |
| Missing email / password / workspace / slug | HTTP 400. |
| Existing account hits signup | Error, not a second workspace. Extra workspaces are LP-184 (`CreateWorkspaceModal`). |
| Superadmin never sees the request | There is no inbox. That is the feature. |
| Card / KYC / phone / company | **Not collected.** BYOK pushes KYC to Billplz/Stripe/CHIP. |
| Email unverified | Still logged in. Do **not** gate this ticket on verify. |

Industry cousins (do not copy extras): Polar / Lemon Squeezy / HitPay / Billplz all have a public price URL and a no-card start. We steal **that**. We do not steal MoR take-rate, HitPay MDR tables as *our* fee, or Stripe Connect onboarding.

Honest second sentence that **must** appear on the page (report 18 §6). Until Hub Pro (LP-004) and remounted LHDN exist, publish this, not the ADR-019 brochure:

> **RM 0 on your sales.** You pay Billplz / Stripe / CHIP their rate. Checkout software is free today. Credits are sold for LHDN e-invoice and (when enabled) WhatsApp recovery; those products are mostly dark in the UI right now. Optional Hub Pro later for SLA / extra workspaces — never a GMV tax.

---

## 3. ID collisions (read once)

Living tracker + implement-ids win.

| Authority ID | Job | Today | This ticket |
|--------------|-----|-------|-------------|
| Tracker **LP-006** | Public self-serve signup + pricing page | **N** | **This file** |
| Tracker **LP-184** | Self-serve workspace create | **P** | Already: register + `POST /workspaces`. Do not rebuild. |
| Tracker **LP-183** | Time-to-first-checkout &lt; 15 min | **P** | Signup is 60s; the clock is BYOK paste + Billplz KYC. Not this ticket. |
| Tracker **LP-004** | SaaS fee (not GMV take) | **P** | Named in ADRs, **no SKU**. Do not print a monthly RM number. |
| Tracker **LP-005** | Prepaid utility credits | **P** | Packs exist; LHDN/WA mostly dark. Show packs; do not sell dark SKUs as live. |
| Tracker **LP-001** | BYOK / 0% GMV | **Y** | Lock. No `applicationFee`. |
| Tracker **LP-007** | KYC for *our* acquiring | **R** | Refuse. |
| Inventory `LP-003` | Public register + first workspace | SHIPPED | Evidence for reuse, not a second register API. |
| Inventory `LP-006` | Platform API keys | SHIPPED | Ignore. |
| Report-18 `LP-001` | Public pricing page | none | Same job as tracker LP-006. |
| Report-18 `LP-006` | 0% GMV take | shipped | Lock, not a page. |
| Report-18 `LP-012` | Self-serve signup, no card | shipped | True for the API; false as a public funnel. |
| Report-18 `LP-014` | TOS/Privacy clickwrap | none | Cheap include on the signup form. Not a DPA. |

---

## 4. What exists (read, not redesigned)

### 4.1 Public register — already creates a workspace

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/one/routes.tsp` | `POST /one/public/register` → `LoginResponse \| ProblemDetails` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/one/models/auth.tsp` | `PublicRegisterRequestDto`: `email`, `password?`, `name?`, `workspace_name`, `tenant_slug` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs` | Maps the route. **No** `RequireAuthorization`. Issues `lazuar_auth`. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Application/Commands/RegisterPublicUserCommand.cs` | Atomic bootstrap. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/Organization.cs` | Slug rule + `OrganizationCreatedDomainEvent` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/Rules/OrganizationSlugMustBeValidRule.cs` | 3–63, `[a-z0-9-]+`, no `--` / edge hyphens, reserved set |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Domain/GlobalUser.cs` | Raises `UserRegisteredDomainEvent` (**no handlers**) |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Application/EventHandlers/OrganizationCreatedDomainEventHandler.cs` | → `TenantProvisionedIntegrationEvent` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/StarterCreditSeederHandler.cs` | On `BILLING` entitlement: 50 starter credits, idempotent |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/BuildingBlocks/Infrastructure/GlobalExceptionHandler.cs` | `InvalidOperationException` / `BusinessRuleValidationException` → HTTP **400** ProblemDetails |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/src/Lazuar.Api/Middleware/TenantSecurityMiddleware.cs` | `/api/v1/one/public` is tenant-exempt |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ArchitectureTests/TenantIsolationArchitectureTests.cs` | Asserts `/api/v1/one/public/register` is exempt |

Handler sequence (`RegisterPublicUserCommandHandler`):

1. Normalize email + slug.
2. Reject if email exists → `InvalidOperationException`.
3. Reject if slug taken → `InvalidOperationException`.
4. `GlobalUser(email, name, hash, isSystemAdmin: false)` — unverified.
5. `Organization(workspaceName, slug)` — slug rule can throw `BusinessRuleValidationException`.
6. `TenantMembership(user, org, "ADMIN")`.
7. Entitlements **`OPS`, `BILLING`, `PAYMENTS`, `CRM`, `LHDN`**. Each publishes `AppEntitlementGrantedIntegrationEvent` onto **One outbox** (`OutboxEventBus<OneDbContext>`), then `SaveChanges` — same context, so publish-before-save is safe.
8. `BILLING` grant → `StarterCreditSeederHandler` (50 credits, skip if wallet exists).
9. Endpoint reloads user, `IssueCookie`, returns `{ user: { email, name, role: "ADMIN", is_email_verified } }`.

JWT role claim is still **`CLIENT`** (not system admin). Body role `ADMIN` is the workspace convention. Pre-existing inconsistency; do not “fix” it here.

**Not granted:** `COMMERCE`. Communications template seed only runs on `COMMERCE` / leftover `COMMUNITY` / `VAULT`. New signups therefore get **no** default Official Receipt / Payment Failed templates until something grants `COMMERCE`. Commerce **routes** in ops do not check that entitlement — they only need a membership (`GET /one/me/entitlements` is the membership list). Leave the `COMMERCE` hole to LP-151 / LP-183 unless a one-line add is needed for template seed; do not expand LP-006 into communications.

**Email:** register does **not** call `SetEmailVerificationToken`. `UserRegisteredDomainEvent` has zero handlers. Verify/resend/forgot endpoints exist; ops UI does not. Do not block signup on verify.

**Password:** TypeSpec marks `password` optional; the endpoint still requires it. No complexity rule. Ops UI only checks confirm-match.

**Rate limit:** none on register/login. Integrator provision has its own limiter. Opening this as a public CTA makes abuse (free 50 credits + LHDN entitlement) a real control, not theatre. A cheap token-bucket on `POST /public/register` (copy `IntegratorProvisionRateLimiter`) is in-scope hygiene. CAPTCHA vendor is not.

Development demo tenant (`SystemGenesisBootstrapperJob`) already calls the **same** `RegisterPublicUserCommand` when `DemoTenant:*` is set. Do not add a second bootstrap.

### 4.2 Ops signup UI — exists, not a funnel

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/components/LoginPage.tsx`

- Modes `signin` \| `signup`. Default **signin**. No `?mode=` query (only `returnUrl`).
- Signup fields: workspace name, slug (client slugify + reserved set mirrored from the domain rule), email, password, confirm. **No** TOS. **No** card. **No** KYC.
- `POST /one/public/register` via `openapi-fetch` with `credentials: "include"`.
- Success: `window.location.href = returnUrl || "/commerce/dashboard"`.
- After cookie, `App.tsx` `OpsLayout` calls `/one/auth/me` then `/one/me/entitlements`. Membership from register populates the switcher. Zero entitlements → “Access Denied” (only if register failed to write membership).

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/App.tsx`

- Public route today: **`/login` only**.
- `/` is inside `OpsLayout` → unauthenticated visitor is bounced to `/login?returnUrl=…`.
- That is why tracker is **N**: the public host is a login wall, not a price + start page.

Ops has **no** test runner (`package.json`: `dev` / `build` / `lint` only). Do not invent Vitest on this ticket.

### 4.3 Additional workspace (LP-184, already P)

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Application/Commands/CreateWorkspaceCommand.cs` | Any authenticated user; ADMIN membership; entitlements only from `provision_apps` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs` | `POST /one/workspaces` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/components/CreateWorkspaceModal.tsx` | Posts `provision_apps: ["OPS","BILLING","PAYMENTS","CRM","LHDN"]` |

This is “create another workspace after you exist.” Not the public funnel. Leave it.

### 4.4 `AppAccessRequest` — documented, not in the database

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/README.md` still claims:

- “Onboarding Queue: … (`AppAccessRequest`) for new Superadmin-led workspace provisioning”
- Aggregate `AppAccessRequest`
- Table `one.AppAccessRequests`

Grep of `*.cs` / `*.tsp` / `*.sql` / migrations / `OneDbContext`: **zero** types, DbSets, or tables. Initial schema (`20260627124757_InitialOneSchema.cs`) creates `GlobalUsers`, `Organizations`, memberships, entitlements, invitations, outbox/inbox — not access requests.

[docs/001-gaps/10-one-identity-module.md](../../../docs/001-gaps/10-one-identity-module.md) already recorded this as README drift. Public signup is **“Always grants fixed core apps; no approval queue.”**

`lazuar-admin` `LoginPage.tsx` is **sign-in only** (`POST /platform/auth/login`). No request-access UI. Superadmin can list every workspace via `/one/me/entitlements` when `IsSystemAdmin`; they do not approve signups.

**Decision lock:** do not implement `AppAccessRequest`. Instant workspace is possible today and is the Wave-1 shape. A queue would make tracker LP-006 *worse*.

### 4.5 Other create paths (do not use for this ticket)

| Path | Auth | Job |
|------|------|-----|
| `POST /one/integrations/workspaces/provision` | Provision secret or SUPER_ADMIN | Aura / sample cashier. Idempotent on `(external_product, external_org_id)`. Rate-limited. **Does not** create a human password user unless `owner_email` matches an existing user. |
| `SystemGenesisBootstrapperJob` | Startup | System org `slug=system` + env superadmins. Dev-only demo tenant via `RegisterPublicUserCommand`. |

Provision is LP-184-adjacent / Aura Connect. A public stranger must not need `INTEGRATOR_PROVISION_SECRET`.

### 4.6 Pricing surfaces — none public

There is **no** `/pricing` route in ops, portal, docs, developers, or admin.

| Surface | What a stranger sees |
|---------|----------------------|
| Prod `hub.lazuar.com` `/` ([deploy/prod/Caddyfile](../../../deploy/prod/Caddyfile) default handle → ops) | Login wall |
| `lazuar-portal` `/` (`apps/lazuar-portal/src/app/page.tsx`) | “Lazuar Secure Portal” + magic-link copy (buyer) |
| `lazuar-docs` home | Integrator guides. Hub vs DIY says commercial terms are “outside this tech guide.” |
| `lazuar-developers` | Scalar / cashier docs |
| `lazuar-admin` | Platform gateways only |
| Ops `BillingSettingsPage` | Pack picker **after** login; **not in sidebar** (ADR 023). Hits `GET /admin/billing/credits/packages` (`OrgAdmin`) |
| `appsettings.json` `Credits:Packages` | RM 50 / 500, RM 100 / 1100, RM 200 / 2500. `StarterGrant`: 50. Costs: WA 2, LHDN 3. |

`GET /admin/billing/credits/packages` is on `/admin/billing` + `RequireAuthorization("OrgAdmin")`. A public page cannot call it. Either **static copy** of those three packs on `/pricing`, or a tiny `GET /one/public/pricing` that reads `ICreditCostService.GetPackages()` + starter grant. Prefer the public GET so the card cannot drift from config. Do **not** expose top-up or wallet.

SST: packs and `"Lazuar Utility Credits"` checkout have **no** tax line. Page must footnote: show **SST 0% or 8% with a reason**, even if the reason is “not SST-registered / below threshold — confirm with accountant.” Do not silently add 8% at checkout on this ticket (that is report-18 LP-010).

### 4.7 Legal pages (buyer, not merchant)

Portal already has buyer-facing:

- `/legal/terms` — Lazuar is not a party; 99.9% sentence; community leftover
- `/legal/privacy`
- `/legal/refund` — we are not MoR

These are **buyer** pages (tracker LP-180 = Y). They are the only clickwrap targets we have. Signup should link them. Do **not** write a merchant DPA on this ticket. One line on the form is enough: platform use is also covered by these pages until a merchant MSA exists.

### 4.8 Production routing (why ops is the right host)

ADR 016 three-tier: `api` / `ops` / `portal`. Prod collapsed to **one host** (`hub.lazuar.com`): `/api/*` → API, `/portal*` → portal, `/docs*` → developers, `/admin/*` → superadmin, **everything else → ops**.

So the public marketing URL of the company **is** the ops SPA. A `/pricing` + `/signup` route on ops is the page. A new `www` app is out of scope.

Cookie: `lazuar_auth`, HttpOnly, Lax, Domain `.lazuar.com` outside Development, TTL `Jwt:ExpiryHours` (24). Ops client always `credentials: "include"`. Keep signup on this origin.

---

## 5. Queue vs instant workspace

| Option | Fits LP-006? | Notes |
|--------|--------------|-------|
| **A. Instant workspace (current command)** | **Yes — required** | Already coded. Matches Polar/HitPay “no card, start now.” |
| **B. Implement `AppAccessRequest` + superadmin approve** | **No** | README-only. Sales-led. Makes TTFC worse. Tracker LP-007-adjacent theatre. |
| **C. Instant workspace + later optional “request Enterprise”** | Later, not this ticket | Report-18 LP-019. Only if a whale asks. |

**Pick A.** If someone cites the One README, update the README in the same PR (delete the queue claims) so the next review does not rebuild it.

Abuse control is **rate limit + starter-grant ceiling + LHDN UI still hidden**, not a human inbox.

---

## 6. Recommended minimal build

Do the smallest surface that flips tracker LP-006 from **N** → **Y** without touching LP-004 / LP-183 / LP-184.

### 6.1 Backend (small)

1. **Keep** `RegisterPublicUserCommand` as the only human signup writer. No new aggregate.
2. Optional but recommended: `GET /api/v1/one/public/pricing` (anonymous, tenant-exempt — `/one/public` already is).
   - Body (illustrative): `{ gmv_take_percent: 0, starter_credits, packages: [{ amount_myr, credits }], sst_note, checkout_is_free: true, lhdn_credits_live: false, whatsapp_credits_live: false }`
   - Read packs + starter from `ICreditCostService`. Hard-code `gmv_take_percent: 0`. Flags from `Messaging:WhatsAppEnabled` and “LHDN UI is hidden” (static `false` until ADR 023 remount — do not auto-detect sidebar).
   - TypeSpec on `OneOperations` next to `registerPublicUser`.
3. Optional hygiene: token-bucket on `POST /one/public/register` (IP or email+IP). 5–10 / 10 min is enough. Mirror provision limiter. Do not block legitimate retries of “slug taken.”
4. Optional one-line: accept `accepted_terms: true` on `PublicRegisterRequestDto` and 400 if false. Enforcement can be client-only if we do not want a schema bump; prefer the boolean so a raw API caller cannot skip clickwrap.
5. Do **not** add `COMMERCE` to `CoreModules` unless template seed is explicitly pulled in. Default: leave it.

### 6.2 Ops frontend (the actual product)

1. Public routes **outside** `OpsLayout`:
   - `/pricing` — new page
   - `/signup` — `LoginPage` forced `signup` (or extract `SignupForm`)
   - `/login` — unchanged default signin; honor `?mode=signup`
2. Unauthenticated visit to `/` → **`/pricing`** (not `/login?returnUrl=/`). Authenticated `/` still → `/commerce/dashboard`.
3. Pricing page content (lock):
   - Headline: RM 0 on your sales / BYOK
   - Rail sentence: you pay Billplz/Stripe/CHIP their MDR, not us
   - Pack table from `GET /one/public/pricing` (or static fallback matching `appsettings.json`)
   - Starter 50 credits, not advertised as “LHDN is live”
   - SST footnote
   - What we are **not**: MoR, acquirer, KYC bureau
   - CTA: Create workspace → `/signup`
   - Secondary: Sign in → `/login`
4. Signup form: existing fields + required unchecked TOS/Privacy checkboxes linking to portal `/portal/legal/terms` and `/portal/legal/privacy` (prod path prefix).
5. Sign-in page: keep “Sign up” link, point at `/signup` or `/pricing`.
6. Do not add billing to the sidebar on this ticket (report-18 LP-003).

### 6.3 Docs (one link, not a second price card)

Add a single sentence + link on VitePress home or `hub-vs-diy.md`: commercial terms live at `/pricing` on the Hub host. Do not duplicate numbers in markdown (they will rot).

### 6.4 README honesty (One module)

Delete `AppAccessRequest` / `one.AppAccessRequests` / “Superadmin-led onboarding queue” from `Modules/One/README.md`. Replace with: public register creates the workspace immediately.

---

## 7. Tests

There is **no** `RegisterPublicUser*` / `OrganizationSlug*` test today. [docs/001-gaps/16-testing-coverage.md](../../../docs/001-gaps/16-testing-coverage.md) still says One auth is uncovered; that doc is stale for webhooks/provision/keys, **still true for register**.

Follow the NSubstitute + in-memory style in `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/tests/Lazuar.ModuleTests/One/ProvisionAuraWorkspaceTests.cs`.

### 7.1 Must add — `RegisterPublicUserCommandHandler`

New file under `apps/lazuar-api/tests/Lazuar.ModuleTests/One/` (name like `RegisterPublicUserCommandHandlerTests.cs`).

| Case | Assert |
|------|--------|
| Happy path | User + org + ADMIN membership + entitlements `OPS,BILLING,PAYMENTS,CRM,LHDN` + `SaveChanges` + `AppEntitlementGranted` published once per app. Returns user id. `IsSystemAdmin == false`. |
| Duplicate email (any case) | Throws `InvalidOperationException` containing “already exists.” No org added. |
| Taken slug | Throws `InvalidOperationException` containing “already taken.” No user added. |
| Reserved slug (`admin`, `portal`, `system`, `billplz`, …) | `BusinessRuleValidationException` from `Organization` ctor. |
| Slug too short / `--` / leading hyphen | Same. |
| Empty name | Display name falls back to local-part of email. |
| No superadmin / no `AppAccessRequest` | Handler type does not reference approval. After success, org `IsActive == true` with no extra table. |

### 7.2 Must add — `OrganizationSlugMustBeValidRule`

Pure unit (no I/O). Valid: `acme`, `acme-corp`, 3 and 63 chars. Broken: empty, `ab`, 64 chars, `Acme`, `acme_corp`, `-acme`, `acme-`, `acme--corp`, each reserved slug.

### 7.3 Must add — public pricing (if the GET is built)

| Case | Assert |
|------|--------|
| `gmv_take_percent == 0` | Forever. Anti-metric: a test that would pass a 5. |
| Packages match `ICreditCostService` | RM 50/500, 100/1100, 200/2500 when configured that way |
| Anonymous / tenant-exempt | Existing `IsTenantExemptPath("/api/v1/one/public/pricing")` if the path is under `/one/public` (already covered by prefix). Add an explicit assert next to the register line. |

### 7.4 Must add — no queue

A cheap architecture or reflection test is optional. Stronger: document in the handler test that `OneDbContext` has no `AppAccessRequest` DbSet (compile-time: the type does not exist). Do **not** add the type to make a test compile.

### 7.5 Already exists — do not redo

| Test | What it already locks |
|------|------------------------|
| `TenantIsolationArchitectureTests` | Register is tenant-exempt |
| `ProvisionAuraWorkspaceTests` | Integrator path (not human signup) |
| `PlatformAdminAuthQueryTests` | Superadmin login DTO |
| Billing wallet domain tests | TopUp/Deduct math — not starter seed |
| Communications `AppEntitlementGrantedIntegrationEventHandlerTests` | Template seed on **COMMERCE**, which register does not grant |

### 7.6 Nice — starter grant

If you touch billing on the way past: `StarterCreditSeederHandler` — BILLING grant creates wallet 50; second grant no-ops; non-BILLING ignored. Not required to close LP-006.

### 7.7 Do not add

- Playwright / Vitest for the SPA (no runner).
- HTTP-level cookie crypto tests (existing login also lacks them).
- KYC / captcha / Hub Pro SKU tests.
- Provision-secret tests (wrong path).

---

## 8. Out of scope / anti-goals

- **Do not** implement `AppAccessRequest`.
- **Do not** print 5% or any GMV take “to look like Paddle.”
- **Do not** invent Hub Pro RM/month (LP-004).
- **Do not** charge 1 credit per checkout (report-18 LP-005).
- **Do not** require MyDigital ID / card / phone to register.
- **Do not** claim LHDN or WhatsApp as live products on the price card.
- **Do not** publish 99.9% on the pricing page (TOS already over-claims).
- **Do not** put pricing on portal `/` (buyers).
- **Do not** build a marketing CMS (ADR 015 / refuse LP-200).
- **Do not** turn this into the BYOK wizard (LP-183).
- **Do not** add members/invite UX (inventory leftover).
- **Do not** gate login on `IsEmailVerified`.

---

## 9. Adjacent IDs (mention, do not implement)

| ID | Why it sits next to this | Boundary |
|----|--------------------------|----------|
| LP-184 | Second workspace | Already P. Modal stays. |
| LP-183 | TTFC &lt; 15 min | Clock after cookie: product + paste 128-char Billplz key. |
| LP-004 | SaaS fee | No SKU. Pricing page says checkout is free today. |
| LP-005 | Credits consume | Packs listed; consumption honesty is a different ticket. |
| LP-182 | Sandbox / test keys | Env badge is not a price card. |
| Report-18 LP-014 | Clickwrap | Include the checkbox; not a DPA. |
| Report-18 LP-015 | Gateway → test pay empty state | Highest TTFC lever; Wave 1 later. |
| Report-18 LP-003 | Credits in sidebar | Do not remount nav here. |
| LP-007 | KYC | Refuse. |

---

## 10. Files to touch when implementing (preview)

Backend:

- `packages/api-spec/modules/one/routes.tsp` + `models/auth.tsp` (pricing DTO; optional `accepted_terms`)
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs` (GET pricing and/or rate limit)
- New small query or endpoint class next to auth — **not** a new module
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/RegisterPublicUserCommandHandlerTests.cs` (new)
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/OrganizationSlugMustBeValidRuleTests.cs` (new)
- `apps/lazuar-api/tests/Lazuar.ArchitectureTests/TenantIsolationArchitectureTests.cs` (pricing path if added)
- `apps/lazuar-api/Modules/One/README.md` (delete queue fiction)

Frontend:

- `apps/lazuar-ops/src/App.tsx` (public routes; `/` → `/pricing` when logged out)
- `apps/lazuar-ops/src/components/LoginPage.tsx` (`mode` query, TOS checkbox, `/signup`)
- New `apps/lazuar-ops/src/pages/PricingPage.tsx` (or `components/PricingPage.tsx`)

Docs (optional one link):

- `apps/lazuar-docs/docs/integrations/hub-vs-diy.md` or `docs/index.md`

Do **not** touch `CreateWorkspaceCommand`, provision, admin login, portal buyer `/`, or Billing top-up.

---

## 11. Commercial copy lock

Source numbers (2026-08-16 config — re-read `appsettings.json` at implement time):

| Pack (MYR) | Credits | RM / credit |
|-----------:|--------:|------------:|
| 50 | 500 | 0.100 |
| 100 | 1100 | 0.091 |
| 200 | 2500 | 0.080 |

Starter grant: **50** credits on first `BILLING` entitlement.

Must say:

- 0% of guest GMV. Money settles to the merchant’s Billplz / Stripe / CHIP account.
- Checkout software: **free today** (no Hub Pro SKU).
- Credits: LHDN submit (3 cr) and WhatsApp (2 cr) **when those products are on**. WhatsApp flag is false. LHDN UI is unrouted.
- SST: state 0% or 8% **with reason**. Do not imply Paddle covers Hub credits (Paddle is Aura Pro only).

Must not say:

- “15 apps”, live WhatsApp dunning, LHDN at checkout, 99.9% SLA, MoR, “we KYC you”, PCI/BNM badge, Fiuu/Xendit as live adapters.

---

## 12. Why tracker is N while inventory says register is SHIPPED

Inventory row “Public self-serve register + first workspace” is about the **command**. Tracker LP-006 is about the **GTM surface** every competitor column has (Billplz, CHIP, HitPay, Xendit, Stripe, Paddle, Chargebee, Polar all **Y**). We have the former and not the latter. Flipping the cell requires a URL a stranger can open, not another identity rewrite.

Success metric (report 18, adapted): page live; 0% GMV + packs + SST footnote; URL is the Hub home for logged-out users; register still creates a workspace with no superadmin.

Anti-metric: a 5% number; a waitlist; a new `AppAccessRequest` table.

---

*Analysis only. Do not implement from this file.*
