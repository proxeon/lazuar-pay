# 09 — Frontends: lazuar-ops, lazuar-portal, lazuar-admin

**Date:** 17 August 2026  
**HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)  
**Slice:** routes, API client paths, chrome that lies, Viewer role, payment settings labels, accept-invite, checkout UX, portal magic link, mobile layout  
**Primary trees:** `apps/lazuar-ops/src/`, `apps/lazuar-portal/src/`, `apps/lazuar-admin/src/`  
**Out of scope:** API handler internals except where the UI calls the wrong contract; `lazuar-docs` / `lazuar-developers` except broken links actually opened (none opened in this pass).  
**This file does not implement anything.** It is a bug audit of the tree as it is now. Code wins. Quotes are from this HEAD.

008 (`plans/008-evals/07-ops-portal-admin-frontend.md`, 16 August 2026) is the baseline. This file re-reads after `911d358` … `297ba98`. A bug 008 filed is closed only if this tree no longer contains it. A bug 008 missed is still written up.

Honesty rule used here: a surface is live only if a human can reach a mounted route and click a control. Backend that exists behind a 403, a swallowed empty table, or an unrouted file is not shipped UI. Role chrome means the UI *knows* ADMIN / MEMBER / VIEWER and changes what it offers. After Waves 1–4 the APIs know roles. The consoles still mostly do not.

---

## 1. Files table (what this slice actually read)

| Path | Lines | Role in this audit |
|------|------:|--------------------|
| `apps/lazuar-ops/src/App.tsx` | 252 | Public vs authed route map; returnUrl; empty-workspace gate; no role on context |
| `apps/lazuar-ops/src/components/Sidebar.tsx` | 307 | Four accordions; no role chip; mobile translate-x |
| `apps/lazuar-ops/src/components/LoginPage.tsx` | 330 | Relative-only returnUrl; signup preserves invite; register always creates a workspace |
| `apps/lazuar-ops/src/components/EmptyWorkspaceState.tsx` | 45 | First-run create-or-logout; no invite path |
| `apps/lazuar-ops/src/components/PricingPage.tsx` | 172 | Public Hub pricing; LHDN UI “not live” sentence |
| `apps/lazuar-ops/src/lib/api-client.ts` | 33 | `VITE_API_URL` + cookie + `X-Tenant-Id` |
| `apps/lazuar-ops/src/lib/utils.ts` | — | `gatewaySupportsOffSession` Stripe/CHIP only |
| `apps/lazuar-ops/src/modules/core/components/PageLayout.tsx` | 155 | Breadcrumbs + workspace switcher; **no hamburger** |
| `apps/lazuar-ops/src/modules/workspace/pages/AcceptInvitePage.tsx` | 185 | Public accept; 401 → login with token in returnUrl |
| `apps/lazuar-ops/src/modules/workspace/pages/PaymentSettingsPage.tsx` | 447 | Xendit fields present; Razorpay not e-mandate |
| `apps/lazuar-ops/src/modules/workspace/pages/TeamPage.tsx` | 133 | Invite/remove always painted |
| `apps/lazuar-ops/src/modules/workspace/pages/GeneralSettingsPage.tsx` | 205 | `hasChanges \|\| true`; branding vs legal logo |
| `apps/lazuar-ops/src/modules/workspace/pages/AuditLogPage.tsx` | 99 | 403 → “No audit events yet.” |
| `apps/lazuar-ops/src/modules/workspace/pages/BillingSettingsPage.tsx` | 183 | WhatsApp not connected (honest); SaaS + credits |
| `apps/lazuar-ops/src/modules/workspace/pages/BillingProfilePage.tsx` | — | Stationery + MyInvois |
| `apps/lazuar-ops/src/modules/workspace/pages/UtilityLedgerPage.tsx` | — | Routed, not in sidebar |
| `apps/lazuar-ops/src/modules/workspace/pages/DeveloperSettingsPage.tsx` | — | Webhook create/rotate |
| `apps/lazuar-ops/src/modules/workspace/pages/EmailSettingsPage.tsx` | — | Resend vault, OrgAdmin |
| `apps/lazuar-ops/src/modules/workspace/pages/ApiKeysPage.tsx` | — | OrgAdmin |
| `apps/lazuar-ops/src/modules/workspace/pages/DeliveryLogsPage.tsx` | — | Redeliver |
| `apps/lazuar-ops/src/modules/workspace/components/CreateWorkspaceModal.tsx` | 90 | `POST /one/workspaces` |
| `apps/lazuar-ops/src/modules/commerce/pages/DashboardPage.tsx` | 278 | Five queries; 403 paints zeros |
| `apps/lazuar-ops/src/modules/commerce/pages/SubscribersPage.tsx` | 863 | Viewer-writable plan/seats/collection; trial cancel chrome |
| `apps/lazuar-ops/src/modules/commerce/pages/ProductsPage.tsx` | 239 | OrgAdmin banners on OrgMember/Viewer |
| `apps/lazuar-ops/src/modules/commerce/pages/DisputesPage.tsx` | 83 | Read-only museum |
| `apps/lazuar-ops/src/modules/commerce/pages/TransactionsPage.tsx` | — | Pagination exists (unlike subscribers) |
| `apps/lazuar-ops/src/modules/commerce/pages/TemplatesPage.tsx` | 265 | WhatsApp Body * required |
| `apps/lazuar-ops/src/modules/commerce/pages/CouponsPage.tsx` | — | Writes OrgMember |
| `apps/lazuar-ops/src/modules/commerce/pages/DunningCampaignsPage.tsx` | — | Deploy defaults |
| `apps/lazuar-ops/src/modules/commerce/pages/CampaignBuilderPage.tsx` | — | WhatsApp step copy |
| `apps/lazuar-ops/src/modules/commerce/components/ProductForm.tsx` | 299 | “We do not validate the TIN” |
| `apps/lazuar-ops/src/modules/commerce/components/dunning/DunningStepEditor.tsx` | — | “Send WhatsApp (not connected)” |
| `apps/lazuar-ops/src/modules/invoicing/pages/TaxInvoicesPage.tsx` | 251 | Receipts as “tax invoices” |
| `apps/lazuar-ops/src/modules/invoicing/pages/CreditNotesPage.tsx` | — | Reuses tax-invoice panel |
| `apps/lazuar-ops/src/modules/invoicing/pages/QuotesPage.tsx` | — | Create + mark paid |
| `apps/lazuar-ops/src/modules/invoicing/components/TaxInvoiceDetailPanel.tsx` | 337 | Always “Tax Document Details” |
| `apps/lazuar-ops/src/modules/invoicing/components/QuoteDetailPanel.tsx` | — | `/pay/{id}` copy |
| `apps/lazuar-ops/src/hooks/use-mobile.ts` | 19 | Exists, unused |
| `apps/lazuar-portal/src/app/page.tsx` | 19 | Dead-end landing; “courses and downloads” |
| `apps/lazuar-portal/src/app/accept-invite/page.tsx` | 17 | 302 to `NEXT_PUBLIC_OPS_URL` or `:3003` |
| `apps/lazuar-portal/src/app/layout.tsx` | — | Footer EN/BM + safe-area |
| `apps/lazuar-portal/src/app/not-found.tsx` | — | Localized 404 |
| `apps/lazuar-portal/src/app/legal/terms/page.tsx` | — | Community leftover; June 2026 |
| `apps/lazuar-portal/src/app/legal/privacy/page.tsx` | — | Meta/WhatsApp as live sub-processor |
| `apps/lazuar-portal/src/app/[tenantSlug]/layout.tsx` | — | `--brand` |
| `apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx` | 243 | Token vs cookie; cancel chrome; documents table |
| `apps/lazuar-portal/src/app/[tenantSlug]/portal/layout.tsx` | 57 | “Buyer Dashboard” → `/{slug}` 404; “Member” |
| `apps/lazuar-portal/src/app/[tenantSlug]/update-payment/[subId]/page.tsx` | 152 | Token required; `err=1` unread; `token=undefined` |
| `apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/page.tsx` | — | Product checkout |
| `apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/success/page.tsx` | — | Polls status; wants token |
| `apps/lazuar-portal/src/app/[tenantSlug]/checkout/custom/success/page.tsx` | — | `returnHref` to portal, no token |
| `apps/lazuar-portal/src/app/[tenantSlug]/pay/[sessionId]/page.tsx` | — | QuoteView |
| `apps/lazuar-portal/src/modules/checkout/lib/api.ts` | — | Browser client; TIN POST |
| `apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx` | 377 | Validates TIN; ID labels untranslated |
| `apps/lazuar-portal/src/modules/checkout/components/CheckoutView.tsx` | — | Quantity/trial; Yearly/Monthly hard-coded |
| `apps/lazuar-portal/src/modules/checkout/components/CheckoutSuccessView.tsx` | 199 | `response.token` never arrives |
| `apps/lazuar-portal/src/modules/checkout/components/QuoteView.tsx` | 224 | No TIN validate; portal CTA tokenless |
| `apps/lazuar-portal/src/modules/checkout/components/CheckoutLayout.tsx` | 20 | `flex-col-reverse` on phone |
| `apps/lazuar-portal/src/modules/checkout/i18n/messages.ts` | — | `form.phone` = “WhatsApp Number” |
| `apps/lazuar-portal/src/modules/checkout/i18n/errors.ts` | — | Email-missing → “gatewayDown” |
| `apps/lazuar-portal/src/modules/checkout/i18n/i18n.test.mjs` | 161 | **Only** portal test; cements the gatewayDown map |
| `apps/lazuar-portal/src/modules/core/lib/server-client.ts` | 21 | Forwards `lazuar_auth` only |
| `apps/lazuar-portal/src/modules/portal/components/PortalPlanChange.tsx` | 120 | Token-only |
| `apps/lazuar-portal/src/modules/portal/components/RequestMagicLinkForm.tsx` | 54 | Always-200 copy |
| `apps/lazuar-portal/src/modules/community/components/CommunityPortalView.tsx` | — | Unimported island |
| `apps/lazuar-admin/src/App.tsx` | 97 | One authed route; returnUrl = pathname only |
| `apps/lazuar-admin/src/components/LoginPage.tsx` | 73 | **No** relative-only guard |
| `apps/lazuar-admin/src/components/Sidebar.tsx` | 221 | “Super Admin” hard-coded |
| `apps/lazuar-admin/src/modules/platform/pages/PlatformPaymentSettingsPage.tsx` | 428 | Xendit fields; no environment select |
| `apps/lazuar-admin/src/modules/core/components/PageLayout.tsx` | — | No hamburger |
| `apps/lazuar-admin/src/lib/api-client.ts` | — | `/platform/*` |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/SubscriberEndpoints.cs` | — | change-plan / quantity / collection inherit OrgRead |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicPortalEndpoints.cs` | — | Portal GET **token only** |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicCheckoutEndpoints.cs` | — | Status `Token = null` always |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicArrearsEndpoints.cs` | — | Token required; success URL tokenless |
| `apps/lazuar-api/Modules/Commerce/Application/ArrearsAccess.cs` | — | Empty token → false |
| `apps/lazuar-api/Modules/Commerce/Application/SubscriptionCancelDecision.cs` | — | TRIALING cancel allowed (616b37d) |
| `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/PortalDocumentQueryService.cs` | — | Classify receipt vs tax invoice |
| `apps/lazuar-api/src/Lazuar.Api/Configuration/AppOptions.cs` | — | Default `ClientUrl` **3020** |
| `apps/lazuar-api/Modules/One/Infrastructure/Services/OneLinkService.cs` | — | OpsUrl vs ClientUrl |
| `apps/lazuar-api/Modules/One/Application/Commands/UpdateWorkspaceCommand.cs` | — | Role must be exactly `ADMIN` |
| `apps/lazuar-api/Modules/One/Infrastructure/Queries/GetPublicPricingQueryHandler.cs` | — | `Lhdn_credits_live = false` hard-coded |
| `packages/api-spec/modules/commerce/public-routes.tsp` | — | Status “does not mint portal tokens” |
| `packages/api-types-ts/src/index.ts` | — | `One.EntitlementDto.role` exists |
| `docker-bake.hcl` | — | Portal args: API URL + basePath; **no** `NEXT_PUBLIC_OPS_URL` |
| `apps/lazuar-portal/Dockerfile` | — | Same missing ARG |
| `docker-compose.yml` | — | Portal service no `NEXT_PUBLIC_OPS_URL` |
| `deploy/prod/env.example` | — | `App__OpsUrl=https://hub.lazuar.com`; no portal OPS URL |
| `deploy/dev/Caddyfile` | — | `/portal*` → :3004; `/` → ops :3003 |

API files above are cited only as the contract the UI actually calls. This report does not re-audit those handlers.

---

## 2. How the three apps sit (re-checked at 297ba98)

| App | Stack | Who | Auth cookie | Entry |
|-----|--------|-----|-------------|-------|
| `lazuar-ops` | Vite + React Router, port **3003** | Merchant console | `lazuar_auth` via `GET /one/auth/me` | `apps/lazuar-ops/src/App.tsx` |
| `lazuar-portal` | Next.js App Router, port **3004**, prod `basePath=/portal` | Buyer checkout + portal | optional cookie *or* `?token=` | `apps/lazuar-portal/src/app/` |
| `lazuar-admin` | Vite + React Router, port **3005**, prod `base=/admin/` | Platform superadmin | `lazuar_admin_auth` via `GET /platform/auth/me` | `apps/lazuar-admin/src/App.tsx` |

Cookie split is host-owned (`AuthAndCorsExtensions.cs`): `/api/v1/platform` reads `lazuar_admin_auth`; everything else reads `lazuar_auth`. Ops talks to `/one/*` and `/admin/*` with `credentials: "include"`. Admin talks to `/platform/*`. Portal uses a server `openapi-fetch` client that forwards **only** `lazuar_auth`, plus a few raw `fetch` calls to `NEXT_PUBLIC_API_URL`.

Neither ops nor admin README is a product surface. Both are still the two-word stub.

`One.EntitlementDto` includes `role`:

```3305:3310:packages/api-types-ts/src/index.ts
        "One.EntitlementDto": {
            workspace_id: string;
            workspace_name: string;
            workspace_slug: string;
            role: string;
        };
```

Ops fetches entitlements and **drops the role on the floor**. `OpsOutletContext` is still only `{ activeWorkspaceId, entitlements, onWorkspaceSelect }`. No page reads `e.role`.

---

## 3. Route maps

### 3.1 `lazuar-ops` — `App.tsx` is the source of truth

Public (no `OpsLayout`):

| Path | Component | Notes |
|------|-----------|--------|
| `/` | `HomeRedirect` | Cookie check → `/commerce/dashboard` or `/pricing` |
| `/pricing` | `PricingPage` | No session |
| `/signup` | `LoginPage` | Forced signup mode; preserves `returnUrl` |
| `/login` | `LoginPage` | `POST /one/auth/login`; relative-only `returnUrl` |
| `/accept-invite` | `AcceptInvitePage` | **Public as of 297ba98.** Outside `OpsLayout`. |

Authenticated (`OpsLayout` + session + ≥1 entitlement):

| Path | Page | Sidebar |
|------|------|---------|
| `/commerce/dashboard` | `DashboardPage` | Dashboard |
| `/commerce/products` | `ProductsPage` | Checkout Links |
| `/commerce/subscribers` | `SubscribersPage` | Subscribers |
| `/commerce/transactions` | `TransactionsPage` | Transaction Logs |
| `/commerce/disputes` | `DisputesPage` | Disputes |
| `/commerce/coupons` | `CouponsPage` | Promotions |
| `/commerce/dunning-campaigns` | `DunningCampaignsPage` | Dunning Campaigns |
| `/commerce/dunning-campaigns/new` | `CampaignBuilderPage` | (no extra nav) |
| `/commerce/dunning-campaigns/:id` | `CampaignBuilderPage` | (no extra nav) |
| `/commerce/templates` | `TemplatesPage` | Notification Templates |
| `/developer/api-keys` | `ApiKeysPage` | API Keys |
| `/developer/webhooks` | `DeveloperSettingsPage` | Outbound Webhooks |
| `/developer/logs` | `DeliveryLogsPage` | Delivery Logs |
| `/workspace/general` | `GeneralSettingsPage` | General Settings |
| `/workspace/team` | `TeamPage` | Team |
| `/workspace/audit` | `AuditLogPage` | Audit log |
| `/workspace/billing-profile` | `BillingProfilePage` | Legal & Billing |
| `/workspace/payment-gateways` | `PaymentSettingsPage` | Payment Gateways |
| `/workspace/email` | `EmailSettingsPage` | Email Provider |
| `/workspace/billing` | `BillingSettingsPage` | Plan & billing |
| `/workspace/ledger` | `UtilityLedgerPage` | **not in sidebar** |
| `/invoicing/quotes` | `QuotesPage` | Quotes |
| `/invoicing/tax-invoices` | `TaxInvoicesPage` | Sales documents |
| `/invoicing/credit-notes` | `CreditNotesPage` | Credit Notes |

Catch-all:

```249:249:apps/lazuar-ops/src/App.tsx
      <Route path="*" element={<Navigate to="/commerce/dashboard" replace />} />
```

There is still **no merchant 404**. Unknown paths, including the commented `/ops/chat`, become Sales Insights. The only `[MVP-HIDE]` left is ops chat (`244:246:apps/lazuar-ops/src/App.tsx`).

`OpsLayout` (`42:165`):

1. `GET /one/auth/me`. Failure → `/login?returnUrl=` + **`location.pathname + location.search`** (search is included; this is the 297ba98 returnUrl fix).
2. Throw → `/login` with **no** returnUrl.
3. `GET /one/me/entitlements`. Empty array → `EmptyWorkspaceState`.
4. Active workspace is `localStorage.ops_active_workspace_id`, repaired to the first entitlement if stale.
5. Workspace switch navigates to `/commerce/dashboard`.
6. Logout `POST /one/auth/logout` and clears the workspace key.

If the entitlements query **errors** (network, 500), `entitlements` is `undefined`, `isEntitlementsLoading` is false. The empty-state branch is `entitlements?.length === 0` (false). The initializing branch requires `entitlements && entitlements.length > 0`. The layout then renders with whatever is in localStorage. That is a lockout-shaped hole: no “create workspace”, no error chrome, just a console that 403s.

### 3.2 `lazuar-admin`

| Path | What happens |
|------|----------------|
| `/login` | `POST /platform/auth/login` |
| `/` | redirect → `/platform/gateways` |
| `/platform/gateways` | `PlatformPaymentSettingsPage` |
| `*` | redirect → `/platform/gateways` |

Auth: `GET /platform/auth/me`. Failure → `/login?returnUrl=` + **`location.pathname` only** (search dropped). Logout `POST /platform/auth/logout`. No entitlements. Sidebar brand: “Platform Control.” Subtitle: hard-coded “Super Admin”. One nav item.

### 3.3 `lazuar-portal`

| URL | File | Live? |
|-----|------|-------|
| `/` | `src/app/page.tsx` | Yes — lock icon + “magic links” + **courses and downloads** |
| `/legal/terms`, `/privacy`, `/refund` | `src/app/legal/**` | Yes |
| `/accept-invite` | `src/app/accept-invite/page.tsx` | Yes — **302 to ops** |
| `/{tenantSlug}/checkout/{productSlug}` | checkout page | Yes |
| `/{tenantSlug}/checkout/{productSlug}/success` | success | Yes |
| `/{tenantSlug}/checkout/custom/success` | custom success | Yes |
| `/{tenantSlug}/pay/{sessionId}` | QuoteView | Yes |
| `/{tenantSlug}/portal` | aggregated portal | Yes — **token required by API** |
| `/{tenantSlug}/update-payment/{subId}` | arrears / update card | Yes — **token required** |
| `/{tenantSlug}` (no child) | *(no `page.tsx`)* | **404** |
| unknown | `not-found.tsx` | Localized 404 |

Production `basePath` is `/portal`. Caddy `handle /portal*` → :3004. A path like `https://hub.lazuar.com/accept-invite` is **ops**, not portal. A path like `https://hub.lazuar.com/portal/accept-invite` is the 302.

---

## 4. Quoted walk (what a human actually clicks)

This is a sequential walk of the three apps as painted, not as intended.

### 4.1 Ops: first paint, login, invite, empty workspace

Unauthed `/` waits on `GET /one/auth/me` then `Navigate`s to `/pricing`. Pricing header is “Lazuar Hub”. The credits card prints this when `lhdn_credits_live` is false (and the API **hard-codes** it false):

```120:124:apps/lazuar-ops/src/components/PricingPage.tsx
          {!pricing.lhdn_credits_live && (
            <p className="text-[13px] text-[#71717a] leading-relaxed">
              LHDN merchant UI is not live in Hub Ops yet. Do not buy credits expecting e-invoice at
              checkout today.
            </p>
          )}
```

That sentence is false at this HEAD. `/workspace/billing-profile` and `/invoicing/tax-invoices` are mounted. The same card honestly says WhatsApp is not connected.

Sign in is `/one/auth/login`. After 297ba98, `returnUrl` is accepted only if it starts with `/` and not `//`:

```12:14:apps/lazuar-ops/src/components/LoginPage.tsx
function isSafeReturnUrl(value: string): boolean {
  return value.startsWith("/") && !value.startsWith("//");
}
```

```33:37:apps/lazuar-ops/src/components/LoginPage.tsx
  const rawReturnUrl = searchParams.get("returnUrl");
  const returnUrl = rawReturnUrl && isSafeReturnUrl(rawReturnUrl) ? rawReturnUrl : null;
  const signupHref = returnUrl ? `/signup?returnUrl=${encodeURIComponent(returnUrl)}` : "/signup";
  const loginHref = returnUrl ? `/login?returnUrl=${encodeURIComponent(returnUrl)}` : "/login";
  const inviteReturn = returnUrl?.startsWith("/accept-invite") ?? false;
```

Signup **preserves** the invite returnUrl. That 008 hole is closed. What remains: signup still `POST /one/public/register` with `workspace_name` + `tenant_slug` required. An invited human who clicks “Sign up” is forced to create a **second** workspace, then bounce to `/accept-invite?token=`. The signup heading when `inviteReturn` is true is:

```208:210:apps/lazuar-ops/src/components/LoginPage.tsx
                  {inviteReturn
                    ? "Sign in with the invited email."
                    : "Register a global identity and workspace."}
```

That is sign-**up** chrome saying “Sign in”.

`/accept-invite` is public. Unauthed → `/login?returnUrl=%2Faccept-invite%3Ftoken%3D…`. Authed with the wrong email → “Sign out”. 5xx is rewritten as “This invite may already have been accepted. Try signing in.” — a real 500 looks like a used token.

Zero entitlements:

```15:18:apps/lazuar-ops/src/components/EmptyWorkspaceState.tsx
        <h1 className="text-xl font-semibold tracking-tight text-[#09090b]">Create your workspace</h1>
        <p className="text-[13px] text-[#71717a] leading-relaxed">
          You are signed in but have no workspace yet. Pick a name and slug — no Superadmin approval.
        </p>
```

There is no “I have an invite” field. If returnUrl was lost (throw path on `/one/auth/me` goes to bare `/login`), an invited user with no memberships is locked into create-workspace.

Legal links on login are Caddy-relative:

```9:10:apps/lazuar-ops/src/components/LoginPage.tsx
const LEGAL_TERMS_HREF = "/portal/legal/terms";
const LEGAL_PRIVACY_HREF = "/portal/legal/privacy";
```

On `http://localhost:3003` without the :9080 gateway those 404. On `http://localhost:9080` they work.

### 4.2 Ops: sidebar and mobile

Brand string is “Lazuar Console”. Footer is `user.name` + `user.email`. **No role chip.** Admin, Member, Viewer, Superadmin get the identical rail.

```249:275:apps/lazuar-ops/src/components/Sidebar.tsx
                mod.id === "commerce" ? [
                  { label: "Dashboard", href: "/commerce/dashboard" },
                  { label: "Checkout Links", href: "/commerce/products" },
                  { label: "Subscribers", href: "/commerce/subscribers" },
                  { label: "Transaction Logs", href: "/commerce/transactions" },
                  { label: "Disputes", href: "/commerce/disputes" },
                  { label: "Promotions", href: "/commerce/coupons" },
                  { label: "Dunning Campaigns", href: "/commerce/dunning-campaigns" },
                  { label: "Notification Templates", href: "/commerce/templates" }
                ] : mod.id === "invoicing" ? [
                  { label: "Quotes", href: "/invoicing/quotes" },
                  { label: "Sales documents", href: "/invoicing/tax-invoices" },
                  { label: "Credit Notes", href: "/invoicing/credit-notes" }
                ] : ...
```

`/workspace/ledger` is mounted and unlinked.

Mobile (`App.tsx` 52:61): on every `innerWidth < 768` the rail is forced closed. The aside is `absolute` and `x: -240` when closed. Collapse toggle is hidden when `isMobile`. `PageLayout` has breadcrumbs and a workspace switcher. **It has no menu button.** `use-mobile.ts` exists in both ops and admin and is unused; both `App.tsx` files inline the same check.

A merchant on an iPhone who loads `/commerce/dashboard` sees Sales Insights and cannot reach Subscribers without editing the URL. That is not polish. It is a navigation outage.

Sidebar collapse persistence is still inverted: `localStorage.setItem("lazuar-ops-sidebar-collapsed", String(prev))` stores the pre-toggle value.

### 4.3 Ops: dashboard as an Admin page wearing a shared layout

Five queries. The page blocks until all five settle, then **never reads `isError`**.

| Query | Endpoint | Policy | Viewer/Member |
|-------|----------|--------|----------------|
| `commerce-stats` | `GET /admin/commerce/stats` | OrgRead | 200 |
| `financial-summary` | `GET /admin/billing/summary` | OrgAdmin (whole billing group) | **403** |
| `commerce-products` | `GET /admin/commerce/products` | OrgRead | 200 |
| `payment-config-status` | `GET /admin/commerce/payment-config` | OrgAdmin | **403** (only 404 swallowed) |
| `email-config-status` | `GET /admin/communications/email-config` | OrgAdmin | **403** |

After a 403 React Query sets `data=undefined`. KPI 1 is `financials?.net_revenue || 0` → **RM 0.00**. Checklist `gatewayReady` / `emailReady` stay false. The Getting started card is immortal unless dismissed for 30 days.

ARR tooltip is the MRR sentence pasted again (`77:78:DashboardPage.tsx`). Product catalog on the dashboard is not clickable. Inactive products are labeled “Archived” here and “Draft” on Checkout Links.

Pay-link copy uses `VITE_PORTAL_URL` or `http://localhost:3004`. Production bake sets `https://hub.lazuar.com/portal`. That default is honest.

### 4.4 Ops: Checkout Links

`showGatewayWarning` is true if the payment-config query **errors** or the array is empty. Viewer/Member always see the rose “Payment Gateway Not Configured” bar, even when an Admin already saved CHIP. Same for Resend. Create Link is always enabled. `hasValidEmailConfig` false also blocks Active on `ProductForm`.

TIN help on the live form:

```221:222:apps/lazuar-ops/src/modules/commerce/components/ProductForm.tsx
                <span className="text-[12px] font-medium text-[#09090b] block">Require Company Name &amp; Tax ID (LHDN B2B)</span>
                <span className="text-[11px] text-[#71717a] block mt-0.5">Collects buyer company + TIN. We do not validate the TIN at checkout.</span>
```

Portal `CheckoutForm` **does** validate (`96:110`). The checkbox “Require WhatsApp Number” does not send WhatsApp. It collects a phone.

### 4.5 Ops: Subscribers — Viewer can click money-adjacent writes

Header: Export CSV + Add Member. Always painted. Export is OrgRead. Viewer downloads PII.

Status filter is client-side on the current page of 50. There is `page` state and **no Prev/Next**. After 50 subscribers the merchant cannot go to page 2. PAST_DUE on page 2 is invisible under the PAST DUE filter on page 1.

Member Console mutations the UI offers:

| Button | Call | API policy | Viewer |
|--------|------|------------|--------|
| Schedule / Revert plan | `POST .../change-plan` | **inherits OrgRead** (`157:188:SubscriberEndpoints.cs` — no `.RequireAuthorization("OrgMember")`) | **200** |
| Set seats | `POST .../quantity` | inherits OrgRead (`190:210`) | **200** |
| Pause / resume collection | `POST .../collection/pause\|resume` | inherits OrgRead (`212:243`) | **200** |
| Pause / resume dunning | `POST .../dunning/pause\|resume` | OrgMember | 403 toast |
| Log Payment | `POST .../record-payment` | OrgMember | 403 |
| Cancel now / at period end | `POST .../cancel` | OrgMember | 403 |
| Keep plan | `POST .../keep` | OrgMember | 403 |
| Anonymize | `POST .../anonymize` | OrgAdmin | 403 |
| Copy Portal Link | `POST .../subscribers/portal-link` | OrgMember | 403 |
| Refund | `POST .../transactions/{id}/refund` | OrgMember | 403 |
| Add Member | `POST /subscribers` | OrgMember | 403 |
| Export CSV | `GET .../export` | OrgRead | **200 + PII** |

The four OrgRead POSTs are not “missing routes.” They exist. The UI invites the click. Team copy says Viewers can only read (`62:TeamPage.tsx`). That is the worst hole in this slice: product copy and API group policy disagree, and the console takes the loose side.

Plan & seats is shown for `ACTIVE` **or** `TRIALING`. Copy: “No charge today.” Collection pause is ACTIVE-only.

Trial cancel chrome (616b37d) is live:

```666:669:apps/lazuar-ops/src/modules/commerce/pages/SubscribersPage.tsx
                {(selectedSub.status === "ACTIVE" || selectedSub.status === "TRIALING") && selectedSub.next_billing_date && new Date(selectedSub.next_billing_date).getTime() > Date.now() && !selectedSub.cancel_at_period_end && (
                  <button onClick={() => { if (window.confirm("Cancel at period end? Access continues until the paid-through date.")) actionMutation.mutate({ action: "cancel", payload: { at_period_end: true } }); }} ...>
                    ... Cancel at period end
```

Backend `SubscriptionCancelDecision` now accepts `TRIALING` (`22:22` of that file). Re-verified closed.

Chrome that still lies on this panel:

```502:504:apps/lazuar-ops/src/modules/commerce/pages/SubscribersPage.tsx
                  {selectedSub.current_period_end && (
                    <span className="block text-[10px] text-[#a1a1aa] mt-0.5">Period started {new Date(selectedSub.current_period_end).toLocaleDateString()}</span>
                  )}
```

It says “Period started” and formats **`current_period_end`**.

Phone row has a `wa.me` link labeled “WhatsApp”. That is a deep-link, not Communications. Plan & billing (`149`) and Pricing (`128`) say WhatsApp is not connected.

Offline payment copy: “This grants one period from today.”

### 4.6 Ops: refunds, disputes, quotes

Refunds are a modal from the subscriber ledger and `TransactionDetailPanel`. Viewer sees Refund and gets a toast. Billplz / offline copy is honest. “Refund does not cancel” is honest.

Disputes: `GET /admin/commerce/disputes?page=1&limit=50`. Read-only table. Status always amber. Empty: “No open disputes.” A 403 throws; React Query error is unhandled. A merchant can click Disputes. They cannot *do* a dispute.

Quotes: list OrgRead, create/mark-paid OrgMember. Pay URL `{VITE_PORTAL_URL}/{slug}/pay/{sessionId}` is a real portal route. Viewer can copy it. There is no “email this quote” button.

### 4.7 Ops: sales documents as e-invoices

Route is `/invoicing/tax-invoices`. Sidebar label is “Sales documents”. Description is almost honest: “Official receipts and tax invoices… B2C receipts stay receipts until monthly consolidation.”

Then the empty state:

```168:168:apps/lazuar-ops/src/modules/invoicing/pages/TaxInvoicesPage.tsx
                <tr><td colSpan={6} className="py-12 text-center text-[12px] text-[#71717a]">No tax invoices found.</td></tr>
```

Type column is `entry.customer_type || "B2C"` — not “Official Receipt” vs “Tax Invoice”. LHDN Status column is on every row. Opening any row mounts `TaxInvoiceDetailPanel` titled **“Tax Document Details”**. Download is “Download PDF Document”. Cancel is “Cancel e-Invoice (LHDN)” when status is VALID.

The portal classifier (`PortalDocumentQueryService.Classify`) already knows Official Receipt vs Tax Invoice vs Credit Note. Ops sales documents do not use it. A B2C receipt is painted as a tax invoice with an LHDN badge.

GET `/admin/billing/ledger?type_filter=sales` is OrgAdmin. Viewer/Member: throwing query, no `isError` UI, empty “No tax invoices found” or a stuck spinner.

Credit notes reuse the same panel. A credit-note row can grow a “Cancel e-Invoice” button if `lhdn_validation_status === "VALID"`.

MyInvois QR is `https://api.qrserver.com/v1/create-qr-code/...` — third-party, not self-hosted.

### 4.8 Ops: team, audit, general, vault

Team always shows email + Admin/Member/Viewer + Invite, and a trash icon on every row including yourself. Description is honest. There is no pending-invites list (`GET .../invites` exists). Viewer clicks Invite → toast.

Audit swallows 403 as empty:

```29:31:apps/lazuar-ops/src/modules/workspace/pages/AuditLogPage.tsx
      if (res.status === 403) return { data: [] as AuditEvent[], total_count: 0, total_pages: 1 };
      if (!res.ok) throw new Error("Failed to load audit log");
```

`metadata_json` is fetched and never shown.

General Settings Save is always enabled:

```110:113:apps/lazuar-ops/src/modules/workspace/pages/GeneralSettingsPage.tsx
  const hasChanges =
    name !== entitlements?.find(e => e.workspace_id === activeWorkspaceId)?.workspace_name
    || slug !== originalSlug
    || true;
```

`UpdateWorkspaceCommand` requires `membership.Role == "ADMIN"` exactly. Superadmin entitlements inject `Role = "SUPER_ADMIN"` without a `TenantMembership`. Save throws `InvalidOperationException("Unauthorized to update workspace.")` — a 500-shaped problem, not a 403. Member/Viewer get the same. PageLayout still offers “Create New Workspace” to every role.

Payment vault (cf0f07d) now has a Xendit block:

```398:428:apps/lazuar-ops/src/modules/workspace/pages/PaymentSettingsPage.tsx
                {gatewayType === "XENDIT" && (
                  <>
                    <div className="p-3 bg-amber-50 ...">
                      <strong>Hosted invoice only.</strong> Xendit is reminder-only. We create a hosted invoice and email the link. No silent auto-charge, no FPX e-mandate.
                    </div>
                    ...
                        Secret API Key
                    ...
                        Callback token (x-callback-token)
```

Razorpay option text is “Razorpay / Curlec (cards; reminder-only until token soak)” — **not** e-mandate. Re-verified closed.

First-time Xendit validation requires API key only, not the callback token. Save with a blank callback token is allowed. Admin vault is the same clone minus the environment `<select>`.

Templates create modal: **WhatsApp Body \*** is required. Dunning step editor is more honest (“Send WhatsApp (not connected)”). Those two screens disagree.

### 4.9 Admin walk

Login has **no** `isSafeReturnUrl`:

```26:31:apps/lazuar-admin/src/components/LoginPage.tsx
      const returnUrl = searchParams.get("returnUrl");
      if (returnUrl) {
        window.location.href = returnUrl;
      } else {
        window.location.href = "/platform/gateways";
      }
```

`/login?returnUrl=https://evil.example` is an open redirect. `//evil.example` is too. Ops closed this. Admin did not.

`SuperadminLayout` returnUrl is pathname only (`33:33:App.tsx`). Search is dropped.

PageLayout has no hamburger. Same mobile trap. Footer subtitle is the string `"Super Admin"`, not `user.email`. A non-superadmin with a product cookie hitting `/platform/auth/me` fails and is sent to login with no “wrong console” message.

### 4.10 Portal walk: checkout

Checkout is the only surface that looks designed for a hand (`flex-col-reverse`, address/TIN 1-col on mobile, safe-area footer). EN/BM lives on the checkout header only.

`CheckoutForm` blocks submit on `validateTin`. Hint “We will validate this number in a later step.” (`form.taxIdHint`) is the opposite of the code. ID type / ID value labels are English only. Interval buttons are the literals `"Yearly"` / `"Monthly"`.

`classifyCheckoutError` maps a missing Resend key to `error.gatewayDown`:

```23:28:apps/lazuar-portal/src/modules/checkout/i18n/errors.ts
  if (
    lower.includes("payment gateway") ||
    lower.includes("not configured an active email provider")
  ) {
    return "error.gatewayDown";
  }
```

Buyer sees “This creator is currently updating their payment settings.” The workspace is missing email. The i18n test **asserts** this map (`135:137:i18n.test.mjs`). That is a lying test.

Country default is `MY`. Ops legal profile uses `MYS`.

Phone label is “WhatsApp Number” in both EN and BM. The field is a phone. WhatsApp is not connected.

### 4.11 Portal walk: success without a token

```114:118:apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicCheckoutEndpoints.cs
            var response = new CheckoutStatusResponse
            {
                Status = result.Status,
                Token = null
            };
```

TypeSpec: “does not mint portal tokens.” Query service comment: “Token is never minted.”

Success UI still does this:

```50:52:apps/lazuar-portal/src/modules/checkout/components/CheckoutSuccessView.tsx
        if (response.status === "COMPLETED") {
          if (response.token) setAccessToken(response.token);
          setStatus("SUCCESS");
```

```191:191:apps/lazuar-portal/src/modules/checkout/components/CheckoutSuccessView.tsx
        <Link href={accessToken ? `/${tenantSlug}/portal?token=${encodeURIComponent(accessToken)}` : `/${tenantSlug}/portal`} ...>
```

`accessToken` stays `null`. “Go to dashboard” is `/{slug}/portal` with no token. Timeout and expired paths also link tokenless. Custom success sets `returnHref={`/${tenantSlug}/portal`}`.

### 4.12 Portal walk: magic link vs cookie vs 404

```24:46:apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx
  if (!token) {
    const { data: authCheck } = await serverClient.GET("/one/auth/me");
    if (!authCheck) {
      return ( ... <RequestMagicLinkForm ... /> );
    }
  }

  const { data: commerceData, error: commerceError } = await serverClient.GET("/public/commerce/{tenantSlug}/portal", {
    params: { path: { tenantSlug }, query: { token: token ?? "" } },
```

```36:37:apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicPortalEndpoints.cs
            var subId = tokenService.ValidateToken(token);
            if (!subId.HasValue) return TypedResults.Unauthorized();
```

Walk A — emailed `?token=`: GET 200. Banner “Identity Verified.” Cancel / keep / plan change work (plan change only if `ACTIVE && token`).

Walk B — no token, no cookie: magic-link form. Honest. Always-200, no enumeration.

Walk C — no token, **has** `lazuar_auth` (merchant previewing the buyer portal, or a buyer who somehow has a product cookie): `/one/auth/me` succeeds, so the form is skipped. GET portal with `token: ""` → 401 → `notFound()` → **localized 404**. The chrome thought a cookie session was a thing. The API does not. This is the empty-state lockout on the portal side.

Cancel / keep server actions ignore the result:

```132:138:apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx
                        <form action={async () => {
                          "use server";
                          await serverClient.POST("/public/commerce/{tenantSlug}/portal/cancel", {
                            params: { path: { tenantSlug }, query: { token: token ?? "" } },
                            body: { subscription_id: sub.id, at_period_end: true }
                          });
                          revalidatePath(`/${tenantSlug}/portal`);
```

Empty token → 401. `revalidatePath` still runs. Button looks like it worked.

Trial cancel chrome (616b37d) is live: `isHealthyForCancel = (ACTIVE || TRIALING) && !cancel_at_period_end`. Re-verified closed. Plan change is still `isHealthyActive && token` — **TRIALING buyers cannot change plan** in the portal even with a token.

Documents table: `doc.type` is honest (`Official Receipt` / `Tax Invoice` / `Credit Note` / `Proforma`). The **LHDN Status** column is on every row, including receipts and proformas. Empty: “No receipts or invoices yet.” Tokenless cookie sessions never reach this table (they 404 first).

Update payment link from the portal:

```174:174:apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx
                        href={`/${tenantSlug}/update-payment/${sub.id}?token=${token}`}
```

When `token` is `undefined`, JavaScript interpolates the string `"undefined"`. The update-payment page then:

```16:19:apps/lazuar-portal/src/app/[tenantSlug]/update-payment/[subId]/page.tsx
  const token = resolvedSearchParams.token as string | undefined;
  if (!token) {
    notFound();
  }
```

The string `"undefined"` is truthy. GET arrears with token `undefined` fails `ArrearsAccess` → 401 → `notFound()`. Cookie-session “Update payment method” is a 404.

`handleUpdatePayment` redirects to `?token=…&err=1` on API error. The page **never reads `err`**. The buyer sees the same card with no error.

Reminder-only ACTIVE and “Account in Good Standing” both `Link href={`/${tenantSlug}/portal`}` with no token.

Arrears POST success URL built by the API is also tokenless (`PublicArrearsEndpoints.cs` 138:139): `${clientUrl}/{slug}/portal`. After paying, the gateway sends the buyer to a portal URL that is either the magic-link form or the cookie 404.

### 4.13 Portal chrome around the dashboard

```21:26:apps/lazuar-portal/src/app/[tenantSlug]/portal/layout.tsx
          <Link
            href={`/${tenantSlug}`}
            className="text-sm font-bold uppercase tracking-widest text-foreground ..."
          >
            Buyer Dashboard
          </Link>
```

There is no `app/[tenantSlug]/page.tsx`. That link is a **404**. Guest name is the literal `"Member"`. Logout posts `/one/auth/logout` and does not redirect or revalidate; the header still says you are logged in until a refresh.

Landing `/`:

```13:15:apps/lazuar-portal/src/app/page.tsx
          Please use the secure, personal magic links sent to your email to access your subscriptions, courses, and downloads.
```

There are no courses. There are no downloads. `CommunityPortalView` is unimported.

Privacy still lists Meta (WhatsApp) as a live sub-processor and says phone is “used for WhatsApp delivery.” Terms (June 2026) still talk about “private communities” and “Creator's community.” BillingSettings (ops) says WhatsApp is not connected. Those three surfaces disagree.

### 4.14 Accept-invite 302 and ClientUrl ports

API invite email (297ba98) is `${GetOpsBaseUrl()}/accept-invite?token=`. `App:OpsUrl` default 3003; prod example `https://hub.lazuar.com`. New mail lands on ops. That fix holds.

Portal still ships a compatibility 302:

```11:16:apps/lazuar-portal/src/app/accept-invite/page.tsx
  const opsBase = (process.env.NEXT_PUBLIC_OPS_URL || "http://localhost:3003").replace(/\/$/, "");
  const dest =
    token && token.length > 0
      ? `${opsBase}/accept-invite?token=${encodeURIComponent(token)}`
      : `${opsBase}/accept-invite`;
  redirect(dest);
```

`docker-bake.hcl` portal args are `NEXT_PUBLIC_API_URL` and `NEXT_BASE_PATH` only. `apps/lazuar-portal/Dockerfile` has the same two ARGs. `docker-compose.yml` portal service does not set `NEXT_PUBLIC_OPS_URL`. A baked production image 302s leftover `/portal/accept-invite` links to **`http://localhost:3003`**.

`AppOptions.ClientUrl` default is `http://localhost:3020` with a comment “typically port 3020”. 3020 is the sample app. Portal is 3004. `appsettings.json` overrides to 3004. Anyone binding `AppOptions` without the JSON, or reading the comment as FE gospel, mints checkout/magic URLs at the wrong port. That is the “wrong ClientUrl ports in FE env” residue: the FE bake is missing `NEXT_PUBLIC_OPS_URL`, and the shared options type still advertises 3020.

---

## 5. Bug catalog

IDs are `B09-U##`. Severity is the impact of the **UI as painted**, not a wishlist.

### P0 — buyer or Viewer can lose money, access, or PII through a painted control

#### B09-U01 — Checkout success never receives a portal token (P0)

**Where:** `CheckoutSuccessView.tsx` 50–52, 162–166, 191; `PublicCheckoutEndpoints.cs` 114–118; `public-routes.tsp` 122–126.  
**What:** Status poller always returns `Token = null`. Success CTA, timeout CTA, and custom-success `returnHref` go to `/{slug}/portal` without `?token=`.  
**Walk:** Pay → COMPLETED → “Go to dashboard” → magic-link form (or cookie 404, B09-U02). The buyer just paid and cannot open the portal from the page the product sent them to.  
**Not a missing route.** The UI calls a contract that documents “does not mint portal tokens” and then branches on `response.token`.

#### B09-U02 — Cookie session on `/{slug}/portal` is a 404 (P0)

**Where:** `portal/page.tsx` 24–45; `PublicPortalEndpoints.cs` 36–37.  
**What:** FE treats `/one/auth/me` success as enough to skip the magic-link form, then calls GET portal with `token: ""`. API is token-only. Unauthorized → `notFound()`.  
**Walk:** Merchant opens their own portal to preview. Buyer who has a product cookie from checkout. Both get a localized 404 instead of the form or the dashboard.  
**008** described cookie sessions as a live path. They are not, at this HEAD, after `9b531d2` required tokens on arrears and the portal GET stayed token-only.

#### B09-U03 — “Update payment method” from a cookie/tokenless portal interpolates `token=undefined` (P0)

**Where:** `portal/page.tsx` 174; `update-payment/[subId]/page.tsx` 16–29; `ArrearsAccess.cs` 20–23.  
**What:** `` `?token=${token}` `` with `token === undefined` is the string `"undefined"`, which passes the `if (!token)` guard and fails HMAC.  
**Walk:** Reach the portal somehow without a real token (you cannot, because of U02 — unless a stale HTML or a future cookie-auth API lands). More importantly, **any** render where `searchParams.token` is missing produces this href. Combined with U01 (success lands tokenless), the next click is 404.  
Reminder-only and “good standing” CTAs also omit the token (`74`, `110`).

#### B09-U04 — Viewer can change plan, seats, and collection pause (P0)

**Where:** `SubscribersPage.tsx` 574–634, 140–204; `SubscriberEndpoints.cs` 157–243 (no OrgMember); `TeamPage.tsx` 62.  
**What:** Team copy: “Viewers can only read.” Member Console: Schedule / Revert / Set seats / Pause collection are enabled. Those four POSTs sit on the OrgRead group. Viewer write is 200.  
**Walk:** Invite a contractor as Viewer. They open a subscriber. They schedule a plan change. Next billing date the customer is on a different product.  
This is the UI exposing an authorization hole. It is also an API hole. This slice owns the painted buttons.

CSV export is the same OrgRead group (`82:104`). Viewer walks out with the subscriber file. Filed with U04 as the PII half of “Viewer can click forbidden actions.”

### P1 — chrome that lies, lockouts, open redirects, cancel that does not cancel

#### B09-U05 — Ops/admin mobile nav cannot be reopened (P1)

**Where:** `lazuar-ops/src/App.tsx` 52–61, 204–206; `PageLayout.tsx` (no hamburger); `lazuar-admin/src/App.tsx` 17–26; both `use-mobile.ts` unused.  
**Walk:** iPhone, `/commerce/dashboard`. Rail is off-screen. There is no button that calls `setIsSidebarOpen(true)`.  
008 filed this. Still open.

#### B09-U06 — Production portal `/accept-invite` 302s to `localhost:3003` (P1)

**Where:** `lazuar-portal/src/app/accept-invite/page.tsx` 11–16; `docker-bake.hcl` 76–87; `lazuar-portal/Dockerfile` 21–24; `docker-compose.yml` 66–83.  
**What:** `NEXT_PUBLIC_OPS_URL` is not baked. Default is `http://localhost:3003`.  
**Walk:** Old email or bookmark `https://hub.lazuar.com/portal/accept-invite?token=…` (ClientUrl era). 302 to a host that is not the Hub. New mail uses OpsUrl (297ba98) and is fine. The compatibility page is the landmine.

#### B09-U07 — Admin login open redirect (P1)

**Where:** `lazuar-admin/src/components/LoginPage.tsx` 26–31.  
**What:** `window.location.href = returnUrl` with no relative-only check. Ops has `isSafeReturnUrl`. Admin does not.  
**Walk:** `https://hub.lazuar.com/admin/login?returnUrl=https://evil.example`.

#### B09-U08 — Portal cancel / keep ignore API errors (P1)

**Where:** `portal/page.tsx` 132–166, 181–188.  
**What:** Server actions `await` the POST and always `revalidatePath`. 401/400 look like success.  
**Walk:** Token expired. Buyer clicks Cancel Plan. Page reloads. Subscription is still ACTIVE. No error.

#### B09-U09 — Quote settled CTA and custom-success return are tokenless (P1)

**Where:** `QuoteView.tsx` 96–98; `checkout/custom/success/page.tsx` 22.  
**Walk:** Pay a quote → “Open buyer portal” → U01/U02.

#### B09-U10 — Update-payment `err=1` is never shown (P1)

**Where:** `update-payment/[subId]/page.tsx` 48–49 vs the render (no `err` read).  
**Walk:** POST fails. Redirect back. Same card. Buyer retries forever.

#### B09-U11 — “Buyer Dashboard” header 404s (P1)

**Where:** `portal/layout.tsx` 21–26; no `app/[tenantSlug]/page.tsx`.  
**Walk:** Click the only brand link on the portal. Localized 404.

#### B09-U12 — Sales documents paint receipts as e-invoices (P1)

**Where:** `TaxInvoicesPage.tsx` 116–117, 168, 193–209; `TaxInvoiceDetailPanel.tsx` 151, 279–288; contrast `PortalDocumentQueryService.Classify` 189–200.  
**What:** Empty “No tax invoices found.” Type = B2C/B2B. Panel title “Tax Document Details.” Cancel e-Invoice on VALID. Portal already knows “Official Receipt.” Ops does not.  
**Walk:** First B2C sale. Merchant opens Sales documents. They think they issued an e-invoice. They did not.

#### B09-U13 — Portal documents table puts LHDN Status on receipts and proformas (P1)

**Where:** `portal/page.tsx` 215, 226.  
**Walk:** Official Receipt row, Status column shows `B2C_RECEIPT` or `—` next to a tax-looking header. Buyer asks why their receipt is not VALID.

#### B09-U14 — No role chrome anywhere in ops (P1)

**Where:** `App.tsx` 36–40; `Sidebar.tsx` 287–291; `PageLayout.tsx` 75–76; every mutation button.  
**Walk:** Three humans, three roles, one UI. Failure is a Sonner toast — if the query layer surfaces `detail`.

#### B09-U15 — Dashboard + Checkout Links lie to Member/Viewer (P1)

**Where:** `DashboardPage.tsx` 27–34, 75–76, 85–86, 111–155; `ProductsPage.tsx` 64–65, 105–133.  
**Walk:** Member operates commerce. Net Cash is RM 0.00. Getting started never completes. Rose “gateway not configured” bar even when CHIP is live.

#### B09-U16 — Product form and checkout disagree about TIN (P1)

**Where:** `ProductForm.tsx` 222 vs `CheckoutForm.tsx` 96–110 vs `messages.ts` `form.taxIdHint` vs `QuoteView.tsx` 37–40 (no validate).  
Three bars: ops says no validate; product checkout validates immediately; quotes collect TIN and do not call MyInvois. Checkout hint says “later step.”

#### B09-U17 — Invite signup still creates a dummy workspace (P1)

**Where:** `LoginPage.tsx` 112–126, 208–210; `EmptyWorkspaceState.tsx` 15–18.  
ReturnUrl is preserved (closed). The register contract still requires a workspace. Invitee becomes ADMIN of a junk tenant plus MEMBER/VIEWER of the real one. Empty state has no invite-token field if they land without returnUrl (auth throw path).

#### B09-U18 — Entitlements query failure skips empty state (P1)

**Where:** `App.tsx` 81–89, 127–140.  
**Walk:** `/one/me/entitlements` 500. Full chrome, stale `ops_active_workspace_id`, every page 403s. No create. No error.

#### B09-U19 — Pricing page says LHDN merchant UI is not live (P1)

**Where:** `PricingPage.tsx` 120–124; `GetPublicPricingQueryHandler.cs` 58; `GetPublicPricingQueryHandlerTests.cs` 97.  
API flag is hard-coded false. FE prints a sentence that Wave 2 made false. The test cements the flag.

#### B09-U20 — Legal/privacy/landing still sell WhatsApp, communities, courses (P1)

**Where:** `legal/privacy/page.tsx` 30, 41; `legal/terms/page.tsx` 20, 33; `app/page.tsx` 14; contrast `BillingSettingsPage.tsx` 149 and `Messaging__WhatsAppEnabled=false`.  
**Walk:** Buyer reads privacy, thinks WhatsApp will fire. It will not.

#### B09-U21 — Superadmin cannot Save General Settings (P1)

**Where:** `UpdateWorkspaceCommand.cs` 32–35; `WorkspaceEndpoints.cs` 147–157; `GeneralSettingsPage.tsx` 79–90.  
Role must be the string `ADMIN`. Superadmin entitlement is `SUPER_ADMIN`. Save → “Unauthorized to update workspace.”

#### B09-U22 — Email-missing checkout error is labeled a gateway outage (P1)

**Where:** `errors.ts` 23–28; `i18n.test.mjs` 135–137; `messages.ts` `error.gatewayDown`.  
**Walk:** Merchant forgot Resend. Buyer thinks Billplz is down.

#### B09-U23 — `Period started {current_period_end}` (P1)

**Where:** `SubscribersPage.tsx` 502–504.  
The date shown is the **end**.

#### B09-U24 — Admin returnUrl drops search (P1)

**Where:** `lazuar-admin/src/App.tsx` 33. Ops includes search (`68`). Admin does not. Low traffic (admin has no query-string pages today) but the pattern is the one 297ba98 just fixed on ops.

#### B09-U25 — Anonymize / Invite / Save vault painted for roles that 403 (P1)

**Where:** `SubscribersPage.tsx` 676–688; `TeamPage.tsx` 66–97; `PaymentSettingsPage.tsx` 433–440.  
Member sees Anonymize. Viewer sees Invite and Save Credentials. Toasts only.

#### B09-U26 — Subscribers have no page 2; status filter is fake (P1)

**Where:** `SubscribersPage.tsx` 22, 53–60, 299, 337–348. Transactions and quotes have Prev/Next. Subscribers do not.  
**Walk:** 51 ACTIVE + 1 PAST_DUE on page 2. Filter PAST DUE on page 1 → “No subscribers found.”

#### B09-U27 — Catch-all erases 404 (P1)

**Where:** ops `App.tsx` 249; admin `App.tsx` 94.  
Bad bookmarks become the dashboard / gateways. `/ops/chat` does too.

#### B09-U28 — Portal plan change is ACTIVE+token only (P1)

**Where:** `portal/page.tsx` 108–116. Cookie buyers (already 404) and TRIALING token buyers do not see the control. Ops can still change a trial’s plan (U04). Two products, two rules.

#### B09-U29 — QuoteView can submit `customer@example.com` (P1)

**Where:** `QuoteView.tsx` 50–51.  
If `client_email` is empty, checkout goes out as that mailbox.

#### B09-U30 — Accept-invite maps every 5xx to “already accepted” (P1)

**Where:** `AcceptInvitePage.tsx` 40–45.  
A down database looks like a used invite.

### P2 — lying labels, dead routes, i18n holes, museums

#### B09-U31 — `hasChanges || true` (P2)

`GeneralSettingsPage.tsx` 110–113. Save is never disabled.

#### B09-U32 — Utility Ledger is a secret route (P2)

Mounted at `/workspace/ledger`. Not in the sidebar. Credits history is hidden next to a top-up form that does not link to it.

#### B09-U33 — Portal header shows “Member” for guests (P2)

`portal/layout.tsx` 15. Magic-link page says you are a Member.

#### B09-U34 — Portal logout does not redirect (P2)

`portal/layout.tsx` 35–45. Cookie dies; chrome stays.

#### B09-U35 — WhatsApp Body * required on template create; dunning editor says not connected (P2)

`TemplatesPage.tsx` 249; `DunningStepEditor.tsx` 152–168; `MessageTemplateEditor.tsx` WhatsApp tab; `ProductForm.tsx` 227; `SubscribersPage.tsx` 470–472.

#### B09-U36 — Checkout i18n holes (P2)

`CheckoutForm.tsx` 228–251 (“ID type”, “ID value”); `CheckoutView.tsx` 160 (“Yearly” / “Monthly”); portal, update-payment, QuoteView, legal: English only. The i18n test only checks dictionary key parity.

#### B09-U37 — Disputes are a museum (P2)

`DisputesPage.tsx` entire file. Clickable nav, no action, no 403 chrome.

#### B09-U38 — Audit 403 → empty (P2)

Latent. Today Viewer can read. If policy tightens, Admins will think nothing happened.

#### B09-U39 — ARR tooltip is the MRR sentence (P2)

`DashboardPage.tsx` 77–78.

#### B09-U40 — Draft vs Archived (P2)

`ProductsPage.tsx` 211 vs `DashboardPage.tsx` 265.

#### B09-U41 — Ops legal hrefs require Caddy (P2)

`LoginPage.tsx` 9–10.

#### B09-U42 — Country `MY` vs stationery `MYS` (P2)

`CheckoutForm.tsx` 53 vs billing profile.

#### B09-U43 — Xendit/Razorpay/Stripe first-save does not require webhook secret (P2)

Vaults accept a key without a callback/whsec. First Billplz *does* require 128-char X-Signature. Inconsistent; Xendit webhooks will 401 until they come back.

#### B09-U44 — Admin vault has no environment select (P2)

Ops does (`230:242:PaymentSettingsPage.tsx`). Admin does not. Hub SaaS top-ups cannot mark test vs live in the UI.

#### B09-U45 — Identity Verified on any successful GET (P2)

Including a query-string token. Looks like a session. It is a 24h HMAC.

#### B09-U46 — Sidebar collapse localStorage inverted (P2)

`App.tsx` 104–108 (ops), 47–51 (admin).

#### B09-U47 — Credit-note rows open a tax-invoice cancel panel (P2)

`CreditNotesPage.tsx` mounts `TaxInvoiceDetailPanel`.

#### B09-U48 — QR via qrserver.com (P2)

`TaxInvoiceDetailPanel.tsx` 258–261. Third-party sees the MyInvois URL.

#### B09-U49 — Create workspace in the switcher for every role (P2)

`PageLayout.tsx` 101–107. Viewer can try. Outcome depends on `POST /one/workspaces`.

#### B09-U50 — No pending invites UI (P2)

Team page invalidates members after invite, not invites.

#### B09-U51 — Community leftover fulfillment still filtered, labels still exist (P2)

`utils.ts` hides `internal:community`. Chat `CreateProductForm` still has WhatsApp. Not on a live route.

#### B09-U52 — `CommunityPortalView` dead (P2)

Unimported. Cancel-at-period-end lives on the aggregated page.

#### B09-U53 — Ops chat still `[MVP-HIDE]` (P2)

Not a bug. Listed so the next person does not remount it by accident and call `/ops/execute-action` from `ActionApprovalCard`.

#### B09-U54 — Admin “wrong console” is silent (P2)

Product cookie on admin → login, no explanation.

#### B09-U55 — Portal i18n Accept-Language prefers any `ms` tag even at low q (P2)

`i18n.test.mjs` 72–78 asserts this. A `en-US,en;q=0.9,ms-MY;q=0.8` browser gets BM. Product decision encoded as a test; easy to call a bug later.

#### B09-U56 — AppOptions default ClientUrl 3020 (P2, FE-adjacent)

`AppOptions.cs` 8–10. Comment says portal is “typically port 3020.” Portal is 3004. Sample app is 3020.

#### B09-U57 — Zero tests in ops and admin (P2)

The only frontend test in this slice is `i18n.test.mjs`, and it cements U22.

#### B09-U58 — Buttons that POST routes that exist but 403 (P2 inventory)

Not missing routes. Catalog of painted writes that are not Viewer-legal: refund, cancel, keep, record-payment, anonymize, invite, remove, save vault, save email, save legal, Check TIN, create quote, mark paid, create coupon, deploy dunning, create template (WhatsApp required), create API key, create webhook, rotate secret, redeliver, SaaS pay, credit top-up, create product. Failure = toast. This is U14’s inventory.

No live button in the three apps POSTs a path that 404s at the API, except the unrouted chat island (`/ops/execute-action`, `/ops/chat/conversations/...`) which is not mounted.

---

## 6. 008 re-verify (filed then, status now)

008 is `plans/008-evals/07-ops-portal-admin-frontend.md`. This table is the law for “did the branch actually close it.”

| 008 claim | Status at 297ba98 | Evidence |
|-----------|-------------------|----------|
| Trial cancel chrome missing on TRIALING (ops + portal) | **CLOSED** (616b37d) | Ops button includes TRIALING (`SubscribersPage.tsx` 666). Portal `isHealthyForCancel` includes TRIALING (`portal/page.tsx` 73–76, 131). `SubscriptionCancelDecision` accepts TRIALING. |
| Xendit option, no credential fields | **CLOSED** (cf0f07d) | Ops + admin + both unused modals have Secret API Key + x-callback-token + reminder-only banner. |
| Razorpay labeled e-mandate | **CLOSED** (cf0f07d) | Option text is “cards; reminder-only until token soak.” Banner says no FPX e-mandate on **Xendit**, not Razorpay. |
| `/accept-invite` missing on ops; portal 302 / ClientUrl | **CLOSED** for new mail (297ba98) | Public ops route (`App.tsx` 214). `OneLinkService.GetOpsBaseUrl()`. Tests `OneLinkServiceTests`. Portal 302 remains as a compatibility page (open as U06). |
| returnUrl drops search; open redirect | **CLOSED on ops** | `pathname + location.search` (`App.tsx` 68). `isSafeReturnUrl` (`LoginPage.tsx` 12–14). **OPEN on admin** (U07, U24). |
| Viewer has no role chrome | **OPEN** | U14. Context still has no role. |
| Viewer change-plan / seats / collection | **OPEN** | U04. Endpoints still OrgRead. Buttons still painted. |
| Dashboard 403 → zeros + immortal checklist | **OPEN** | U15. |
| Checkout Links scare banners are Admin GETs | **OPEN** | U15. |
| Anonymize shown to Member | **OPEN** | U25. |
| Audit 403 → empty | **OPEN** (latent) | U38. |
| Team no pending list, invite always on | **OPEN** | U25, U50. |
| Superadmin ≠ ADMIN on General Settings | **OPEN** | U21. |
| Product form lies about TIN | **OPEN** | U16. |
| Subscriber status filter is fake pagination; no page 2 | **OPEN** | U26. |
| Mobile nav trap | **OPEN** | U05. |
| Utility Ledger secret route | **OPEN** | U32. |
| Portal plan change requires `?token=` | **OPEN** | U28. Cookie path is now worse (U02). |
| Portal cancel ignores errors | **OPEN** | U08. |
| `/pay/{id}` completed CTA goes to `/portal` without token | **OPEN** | U09. Broader now: **all** success CTAs (U01). |
| ID type + interval skipped i18n | **OPEN** | U36. |
| Disputes museum | **OPEN** | U37. |
| Catch-all erase 404 | **OPEN** | U27. |
| WhatsApp affordances disagree | **OPEN** | U20, U35. |
| Export CSV OrgRead | **OPEN** | U04. |
| `hasChanges \|\| true` | **OPEN** | U31. |
| ARR tooltip = MRR | **OPEN** | U39. |
| Period-started uses period-end | **not in 008; OPEN** | U23. |
| Portal cookie session works | **008 assumed yes; now FALSE** | U02. Token requirement on arrears (`9b531d2`) + portal GET token-only. |
| Checkout success attaches token | **008 implied the branch; now FALSE** | U01. Status explicitly `Token = null`. |

Recently-fixed items the prompt named, restated:

1. **Trial cancel chrome** — closed on both painted surfaces. Confirm copy still says “paid-through date” on a trial; acceptable.
2. **Xendit fields + Razorpay not e-mandate** — closed. Residual: callback token not required on first save (U43); admin has no environment select (U44).
3. **`/accept-invite` public ops + portal 302** — ops page and OpsUrl mint closed. Portal 302 default host is still localhost (U06).
4. **returnUrl includes search; relative-only** — closed on ops. Admin is the leftover (U07, U24).

---

## 7. Lying tests and lying copy

### Tests

| Test | What it asserts | Why it lies or cements a lie |
|------|-----------------|------------------------------|
| `apps/lazuar-portal/src/modules/checkout/i18n/i18n.test.mjs` `classifyCheckoutError` | `"Workspace has not configured an active email provider."` → `error.gatewayDown` | The buyer-facing string is about **payment settings**. The workspace is missing Resend. The test will fail if someone fixes the map. |
| Same file `messages` | `en` and `ms` have the same keys | Does not fail when the UI hard-codes `"ID type"`, `"Yearly"`, QuoteView English, portal English. Green CI, partial BM. |
| `GetPublicPricingQueryHandlerTests` `Lhdn_credits_live` is False | API flag is false | Combined with `PricingPage.tsx` 120–124, the public pricing page will keep saying LHDN UI is not in Hub Ops. The UI is in Hub Ops. |
| Ops | *(none)* | No test that Viewer buttons are hidden. No test that returnUrl is relative-only. No test that Xendit fields render. |
| Admin | *(none)* | No test that returnUrl is relative-only. The open redirect cannot regress-fail. |

There is no Playwright / component test in these three apps that clicks Cancel on a TRIALING sub, fills Xendit, or follows `/accept-invite?token=`.

### Copy that contradicts the tree

| Copy | File | Tree |
|------|------|------|
| “Viewers can only read.” | `TeamPage.tsx` 62 | U04 writes |
| “We do not validate the TIN at checkout.” | `ProductForm.tsx` 222 | `CheckoutForm` validates |
| “We will validate this number in a later step.” | `messages.ts` `form.taxIdHint` | Validate is the next line |
| “LHDN merchant UI is not live in Hub Ops yet.” | `PricingPage.tsx` 122–123 | Legal & Billing + Sales documents are mounted |
| “WhatsApp Number” / “used for WhatsApp delivery” / Meta sub-processor | checkout + privacy | `Messaging__WhatsAppEnabled=false`; Plan & billing “not connected” |
| “courses, and downloads” | `app/page.tsx` 14 | No courses module; `CommunityPortalView` dead |
| “private communities” | `legal/terms/page.tsx` 20 | ADR-023 lobotomy |
| “Period started {current_period_end}” | `SubscribersPage.tsx` 503 | End, not start |
| “No tax invoices found.” | `TaxInvoicesPage.tsx` 168 | Page is receipts + invoices |
| “Tax Document Details” / “Cancel e-Invoice” | `TaxInvoiceDetailPanel.tsx` | Opens for B2C receipts |
| “This creator is currently updating their payment settings.” | `error.gatewayDown` | Also used for missing email |
| “Sign in with the invited email.” | signup mode `LoginPage.tsx` 209 | The form creates a workspace |
| “Identity Verified” | `portal/page.tsx` 58 | HMAC in the query string |
| “Super Admin” | `admin/Sidebar.tsx` 204 | Hard-coded, not `user.role` |
| “Member” | `portal/layout.tsx` 15 | Default for logged-out buyers |
| “WhatsApp Body *” | `TemplatesPage.tsx` 249 | Channel not connected |
| ARR tooltip | `DashboardPage.tsx` 78 | MRR definition |

---

## 8. Unread / not mounted (so the next person does not hunt them as product)

Intentionally unrouted or unimported. Not bugs unless someone remounts them.

**Ops**

- `components/OpsChatWorkspace.tsx`, `ConversationsDirectory.tsx`, `ActionApprovalCard.tsx` (`POST /ops/execute-action`), `hooks/use-chat-stream.ts`, `components/chat/*`, `lib/prompt-library.ts`, `types/chat.ts`
- `components/forms/CreateProductForm.tsx` (chat registry; no trial field)
- `modules/commerce/components/CreateProductForm.tsx` (duplicate)
- `components/PaymentSettingsModal.tsx` and `modules/workspace/components/PaymentSettingsModal.tsx` (Xendit fields were patched in cf0f07d anyway; grep finds no importer)
- `hooks/use-mobile.ts` (logic inlined in `App.tsx`)
- Most of `components/ui/*` shadcn (scaffold)

**Portal**

- `modules/community/components/CommunityPortalView.tsx`
- `modules/community/lib/api.ts`

**Admin**

- `lib/prompt-library.ts`, `types/chat.ts`, `hooks/use-mobile.ts`
- Same shadcn pile as ops
- No tenant list, impersonation, credit grant, feature flag, dead-letter, LHDN platform keys (those live under ops)

**Docs / developers**

Not opened. No broken in-app links from these three trees pointed at them except `ApiKeysPage` `VITE_DOCS_URL || "/docs"` (Caddy `/docs*` → developers). Not followed in this pass.

---

## 9. Ranked open bugs

P0 first, then P1, then P2. Fix order is “what a human hits this week,” not ticket age.

1. **U01** — Checkout success dashboard has no token. Every paid guest lands on the magic-link gate. This is the buyer’s first post-pay click.
2. **U02** — Cookie “session” on the portal is a 404. Merchants previewing the portal, and any future cookie-login work, are already broken.
3. **U03** — Update-payment `?token=undefined` + tokenless CTAs. Stacks on U01.
4. **U04** — Viewer Schedule / Set seats / Pause collection / Export CSV. The product’s own Team sentence is the spec. The Member Console violates it.
5. **U05** — Phone-width ops/admin cannot open the rail. The merchant cannot click *anything* except the current page.
6. **U08** — Cancel Plan can no-op. Worst when the token expired (24h).
7. **U06** — Baked portal 302 to localhost:3003. Only leftover `/portal/accept-invite` links, but those are exactly the emails from before 297ba98.
8. **U07** — Admin open redirect. Small audience, real issue.
9. **U12 / U13** — Receipts dressed as e-invoices in ops and as LHDN rows in the portal. Compliance chrome.
10. **U09 / U10 / U11** — Quote CTA, silent `err=1`, Buyer Dashboard 404. Same buyer, same hour.
11. **U14 / U15 / U25** — Role chrome. Member cannot trust the dashboard. Viewer can click everything.
12. **U16 / U22** — TIN and checkout error copy. Merchant configures the opposite of what the buyer hits.
13. **U17 / U18** — Invite / empty-state lockouts after the returnUrl fix.
14. **U19 / U20** — Public pricing + legal still describe a product that is not this tree.
15. **U21** — Superadmin cannot rename a workspace.
16. **U23 / U26 / U27 / U28 / U29 / U30** — Console lies and pagination. Daily merchant pain.
17. **U31–U58** — P2 pile. Do not start here while U01–U05 are open.

Closed on this branch and not to be re-filed: Xendit field block, Razorpay e-mandate label, ops `/accept-invite`, ops relative-only returnUrl-with-search, trial cancel chrome on ops and portal.

Do not claim LHDN UI is hidden. It is on Legal & Billing and Sales documents — and then the pricing page claims it is hidden (U19).  
Do not claim `/pay/{id}` 404s. It renders `QuoteView`.  
Do not claim TIN is stripped. Product checkout validates. Quotes do not.  
Do not claim Viewers cannot change money-adjacent state. They can, on three POSTs plus CSV.  
Do not claim Xendit is unconfigurable. The form is there.  
Do not claim the portal cookie session works. GET portal is token-only.  
Do not claim checkout success hands the buyer a portal token. `Token = null`.  
Do not treat `lazuar-admin` as a control plane. It is a vault page with a Super Admin label and an open redirect.

---

*End of 09. Uncondensed. No fixes implemented.*
