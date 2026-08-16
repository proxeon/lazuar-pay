# W1-LP-025 — Checkout branding (logo, colors)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 1 row `LP-025` (“Checkout branding”). Tracker label in [00-checklist-tracker.md](../00-checklist-tracker.md) is “Branding (logo, colors) on checkout” (`Ours = P`). Sequencing alias in [20-sequencing-and-tracker-schema.md](../20-sequencing-and-tracker-schema.md) is `LP-UX-005` (logo / colors / merchant name). Checkout report row `CK-025` in [09-checkout-and-payment-links.md](../09-checkout-and-payment-links.md) is the same job.  
**Not this ID:** [01-lazuar-feature-inventory.md](../01-lazuar-feature-inventory.md) reuses `LP-025` for “Usage-based subscriptions.” [18-pricing-onboarding-trust.md](../18-pricing-onboarding-trust.md) reuses `LP-025` for “PDPA merchant notice + export.” Ignore those meanings.

**Invariant:** A buyer on hosted product checkout must be able to tell **who they are paying** from workspace identity (trading name + optional logo + optional one accent color). Platform chrome stays secondary. This is a cash-register skin, not a storefront builder.

---

## 0. Scope lock

In scope:

- Workspace **General Settings** as the merchant editor
- `one.Organizations` as the SSoT for **trading** identity (name already; logo/color missing)
- A **public, unauthenticated** branding snapshot by tenant slug
- Hosted product checkout chrome: hop-1 form, success (same `[productSlug]` layout), and the update-payment money page
- Optional document title `{product} · {workspace}` on those pages

Out of scope (do not expand this ticket):

- Custom fonts, CSS, themes, brand kits, hero/banner, “about” copy, product images (`LP-200` / `LP-201` refuse)
- Custom domain (`LP-017` / `CK-026`)
- Product OG / WhatsApp unfurl image (`CK-027`)
- BM/EN (`LP-020`), mobile layout rewrite (`LP-021`), quantity (`LP-014`)
- Company + TIN on the form (`LP-022`) and legal profile un-hide (`LP-122`)
- PDF / QuestPDF branding (`LP-107`) — legal name, TIN, invoice logo
- Hop-2 Billplz / Stripe / CHIP hosted pages (their own branding dashboards)
- M2M cashier (`examples/hub-cashier-next`) and Aura `/book`
- Custom quotes `/{tenant}/pay/{id}` (`[MVP-HIDE]` / `notFound()`)
- Email HTML skins (emails already resolve `{{business_name}}` from workspace **name**)
- Buyer portal dashboard chrome (nice later; not conversion)
- Reading or writing `billing.TenantBillingProfiles` from checkout

**Refuse boundary:** [19-refuse-list-and-adjacents.md](../19-refuse-list-and-adjacents.md) explicitly keeps “checkout logo/colors (`LP-025`) as a cash-register skin” and refuses “custom fonts, themes, and brand kits that turn portal into a site builder.” Stay on the skin side.

---

## 1. What “minimal branding” means here

Three fields. Nothing else.

| Field | Source today | Buyer job |
|-------|--------------|-----------|
| **Trading name** | `Organization.Name` (workspace name) | “Who am I paying?” |
| **Logo** | **None** on workspace. Invoice `LogoUrl` is a different plane and the editor is hidden | Mark in the header |
| **Primary color** | **None** anywhere | Accent the pay CTA / header hairline. Not a theme rewrite |

Defaults when unset:

- Name: always present (required on create / general settings)
- Logo: omit the `<img>`; show the name as text
- Color: keep current zinc / `bg-foreground` CTA

“Powered by Lazuar” **stays**, small, secondary (padlock row). Platform Terms/Privacy/Refund footer stays — we are the processor; the creator is the seller. Do not replace those links with merchant legal pages.

---

## 2. Two identity planes — do not mix

| Plane | Aggregate | Editor (ops) | Public today | Use for checkout? |
|-------|-----------|--------------|--------------|-------------------|
| **Workspace / trading** | `Modules.One.Domain.Organization` | `/workspace/general` (`GeneralSettingsPage`) | Slug is the URL. **Name is not published** to portal | **Yes — this ticket** |
| **Legal / tax** | `Modules.Billing.Domain.Aggregates.TenantBillingProfile` | `/workspace/billing-profile` **`[MVP-HIDE]`** | `GET /public/billing/{tenantSlug}/profile` returns full DTO including **TIN + address + logo_url** | **No** |

`TenantBillingProfile` fields: `legal_name`, `tin`, `registration_number`, `sst_registration_number`, `logo_url`, address. Ops copy: “official corporate identity used for LHDN… quotations and LHDN tax invoices.” That is `LP-107` / Wave 2.

QuoteView (orphaned) already paints `profile.logo_url` + `legal_name` + TIN + SSM. That is a **proforma**, not hop-1 product checkout. Do not wire product checkout to the public billing profile: it would (a) couple CaaS checkout to a lobotomized compliance page, (b) put TIN/SSM/address on an unauthenticated product URL, (c) 404 when the merchant never saved a billing profile.

Emails already treat workspace name as the merchant display name (`{{business_name}}` ← `WorkspaceSnapshotDto.Name`). Checkout should match that name, not `LegalName`.

---

## 3. Current files

### 3.1 Workspace general settings (the intended editor)

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/GeneralSettingsPage.tsx` | Only **Workspace Name** + **slug**. Loads both from `GET /one/me/entitlements`. Saves `PUT /one/workspaces/{id}` `{ name, slug }`. No logo. No color. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/App.tsx` | Route `/workspace/general` is live. Billing profile route is commented `[MVP-HIDE]`. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/components/Sidebar.tsx` | Workspace nav: General Settings, Payment Gateways, Email Provider. No branding item. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/components/LoginPage.tsx` | Public register sends `workspace_name` + slug → creates `Organization`. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/core/components/PageLayout.tsx` | Ops chrome shows `workspace_name` from entitlements (staff only). |

There is **no** `GET /one/workspaces/{id}` for a tenant admin. List `GET /one/workspaces` is platform `OrgAdmin`. General Settings therefore cannot load fields that are not on `EntitlementDto` (`workspace_id`, `workspace_name`, `workspace_slug`, `role`).

### 3.2 Tenant / organization model

`one.Organizations` (EF snapshot + `Organization.cs`):

| Column | Type | Notes |
|--------|------|--------|
| `Id` | uuid | Tenant id |
| `Name` | text, required | Trading / workspace name |
| `Slug` | text, required, unique | Public URL key |
| `IsActive` | bool | Archive flag |
| `CreatedAt` / `UpdatedAt` | timestamptz | |
| `ExternalProduct` / `ExternalOrgId` | optional | Aura bind |

**No `LogoUrl`. No `PrimaryColor`. No JSON settings bag.**

Mutators: constructor `(name, slug)`; `UpdateDetails(name, slug)` → `OrganizationUpdatedDomainEvent(Id, Name, Slug)` → `WorkspaceUpdatedIntegrationEvent(OrganizationId, Name, Slug)`. Messaging `TenantReplica` copies **name + slug only**. Host `WorkspaceUpdatedIntegrationEventHandler` only busts API-key cache.

Contracts:

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/one/models/workspace.tsp` | `UpdateWorkspaceRequestDto { name, slug }`. `WorkspaceDto` / `EntitlementDto` have no brand fields. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs` | `PUT /workspaces/{id}` → `UpdateWorkspaceCommand` (membership `Role == "ADMIN"`). |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Application/Commands/UpdateWorkspaceCommand.cs` | Name + slug only. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Contracts/IOneQueryService.cs` | `WorkspaceSnapshotDto(Id, Name, Slug, IsActive, CreatedAt)`. `GetWorkspaceBySlugAsync` exists but is **not** exposed publicly. `GetTenantIdBySlugAsync` is id-only and already used by public commerce. |

### 3.3 The only logo field (wrong plane)

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Domain/Aggregates/TenantBillingProfile.cs` | `LogoUrl` string? |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/billing/models.tsp` | `logo_url?` on admin + public DTOs |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/BillingProfilePage.tsx` | Upload via `POST /one/storage/presigned-url` then PUT R2; save `logo_url` on billing profile. **Unrouted.** |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/One/Infrastructure/Endpoints/StorageEndpoints.cs` | Tenant-keyed R2 object `vault/{tenantId}/{guid}{ext}`. Auth + `X-Tenant-Id`. Reusable. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints/PublicBillingEndpoints.cs` | `GET /{tenantSlug}/profile` → full `TenantBillingProfileDto` or 404 |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/Commands/GenerateAndStoreDocumentCommandHandler.cs` | PDF: `CompanyLogo` from billing `LogoUrl`; `CompanyName` prefers **legal** name; workspace name is only a fallback / slug lookup |

No `primary_color` / `accent` / `brand_color` column in One, Billing, Commerce, or TypeSpec.

### 3.4 Public checkout payload

`GET /public/commerce/{tenantSlug}/products/{slug}` (`PublicProductEndpoints` + `ProductDto`):

`id`, `slug`, `name`, `price`, `pricing_model`, `minimum_price`, `currency`, `interval`, `is_active`, `gateway_name`, `supports_off_session`, `fulfillment_targets`, `checkout_configuration`.

**No merchant name, logo, or color.** Tenant slug is resolved only to bind `OrganizationId`.

Portal checkout page fetches that DTO and nothing else.

### 3.5 Portal chrome (what the buyer sees)

| Path | What is branded |
|------|-----------------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/layout.tsx` | Title “Lazuar Portal”. Footer © Lazuar Platform + platform legal. Lazuar favicon. `html lang="en"`. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/[tenantSlug]/layout.tsx` | Passthrough. ADR 017 promised “Fetches Tenant Theme/Colors.” **It does not.** |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/layout.tsx` | Blind checkout: sticky header, padlock, **“Powered by Lazuar”** right-aligned. No merchant name, no logo. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/CheckoutView.tsx` | `itemName: product.name` only. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/OrderSummaryCard.tsx` | Product title + money. No seller line. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx` | CTA `bg-foreground`. Legal: “Lazuar’s Terms… purchase is a direct transaction with the Creator” — Creator unnamed. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/success/page.tsx` + `CheckoutSuccessView.tsx` | Product name only. Same checkout layout header. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/[tenantSlug]/update-payment/[subId]/page.tsx` | Product name + amount. No workspace name. Own page chrome (not checkout layout). |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/[tenantSlug]/portal/layout.tsx` | Header “Buyer Dashboard”. Out of scope. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/app/[tenantSlug]/pay/[sessionId]/page.tsx` | `notFound()`. Commented code **would** fetch billing profile for QuoteView. Out of scope. |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-portal/src/modules/checkout/components/QuoteView.tsx` | Only existing logo UI. Unreachable. |

No `generateMetadata` under `lazuar-portal`. Tab / unfurl title is always “Lazuar Portal.”

### 3.6 Name already used off-checkout (do not regress)

| Consumer | Behavior |
|----------|----------|
| Communications handlers (`LifecycleEventHandlers`, `GatewayPaymentFailed…`, `FulfillmentRequested…`, `PortalAccessEmailHandlers`) | `workspace.Name` or fallback `"Lazuar Merchant"` / `"Business"` → `{{business_name}}` |
| `GenerateAndStoreDocumentCommandHandler` | `businessName = workspace?.Name ?? profile?.LegalName ?? "Business"` for lookup; PDF `CompanyName` is **legal** |
| Messaging `TenantReplica` | Name + slug replica |

Adding logo/color to `Organization` must not require Messaging/Billing schema. Keep `WorkspaceUpdatedIntegrationEvent` as name+slug unless a consumer needs more (none do for this ticket).

---

## 4. Why tracker is `P` (not `N`)

Honest partial:

1. Workspace **name exists** and merchants can edit it.
2. Emails already say that name.
3. URL already contains the slug (`/{tenantSlug}/checkout/{productSlug}`).
4. A logo **upload path** exists (R2 presign) and a logo **column** exists on the billing profile.
5. ADR 017 reserved tenant layout for theme fetch.

Not shipped:

- Checkout never reads workspace name.
- No workspace logo/color fields.
- Billing logo is hidden and is the wrong document.
- Header and metadata say Lazuar.

That matches [09](../09-checkout-and-payment-links.md) §13 (“poor for on-page merchant brand”) and competitor table “Merchant branding on page = none / L1 is Lazuar-branded.”

---

## 5. Exact gaps

### G1 — Workspace name never reaches hop 1

`Organization.Name` is written on register / general settings / `UpdateDetails`. Public product GET and portal layouts ignore it. Buyer sees product title + “Powered by Lazuar.”

### G2 — No workspace logo or color columns

Cannot persist checkout brand without a One migration. Billing `LogoUrl` is not a substitute (plane + hidden UI + public TIN leak).

### G3 — No public branding snapshot

Portal has no unauthenticated “who is this slug?” DTO that is **safe** (name + optional logo URL + optional hex). The only public identity GET is billing profile (unsafe / often 404).

### G4 — Ops cannot edit brand fields

General Settings has no upload, no color picker, no GET for extra fields. Entitlements are a poor place to stuff `logo_url`.

### G5 — Checkout chrome is platform-first

`BlindCheckoutLayout` is Lazuar-only. CTA is zinc/black. Success / update-payment inherit the same anonymity.

### G6 — No tests for public identity

No `UpdateWorkspace` tests. No public branding tests. Architecture tests already treat `/api/v1/public` as tenant-exempt — a new public branding route under that prefix fits.

**Not gaps for this ticket**

| Observation | Why not LP-025 |
|-------------|----------------|
| Hop 2 page is Billplz/Stripe/CHIP-branded | Wrap, don’t restyle their hosts |
| QuoteView already has a logo | Hidden B2B surface; legal plane |
| No custom domain | `LP-017` |
| Favicon is Lazuar | Acceptable; do not add per-tenant favicon this wave |
| `UpdateWorkspaceCommand` requires membership `ADMIN` (not entitlement `SUPER_ADMIN`) | Pre-existing; do not redesign auth |
| Public billing profile leaks TIN | Do not call it. Fixing that leak is a billing ticket |

---

## 6. Recommended model

```
ops General Settings
  → PUT /one/workspaces/{id}  { name, slug, logo_url?, primary_color? }
  → one.Organizations

portal [tenantSlug] layout (and/or checkout layout)
  → GET /public/one/{tenantSlug}/branding
  → { name, slug, logo_url?, primary_color? }
  → header mark + CSS var --brand
```

Rules:

1. **SSoT = `Organization`.** One optional `LogoUrl` (`text`, null) and `PrimaryColor` (`varchar(7)`, null, `#RRGGBB` only).
2. **Public DTO is a subset.** Never include TIN, legal name, address, ids beyond slug, or `is_active` internals. Unknown / inactive slug → **404** (same as product GET).
3. **Logo URL** is an https URL from the existing presign (`final_url`). Reject `data:`, `javascript:`, and non-http(s). Do not proxy-download on the public GET (portal `<img src>`).
4. **Color** validate `^#[0-9A-Fa-f]{6}$`. Store canonical `#RRGGBB`. Apply as `--brand` on a wrapper. CTA: `background: var(--brand, …foreground fallback)`. Do not generate a full palette. Do not accept `rgb()`, named colors, or CSS.
5. **Name** remains required, trimmed. Empty name stays invalid.
6. **Do not** put branding on `ProductDto` (wrong grain; update-payment has no product GET).
7. **Do not** extend `WorkspaceUpdatedIntegrationEvent` for logo/color. Messaging replica does not need them.
8. **Do not** copy billing `LogoUrl` into Organization automatically.

Public route prefix `/api/v1/public/...` is already `IsTenantExemptPath`. Prefer:

`GET /api/v1/public/one/{tenantSlug}/branding`

(TypeSpec under a small public One interface, or a single operation next to existing public surfaces.) Commerce-owned `/public/commerce/{slug}/branding` is worse: branding is not a sellable SKU.

Authenticated read for the editor: add `GET /one/workspaces/{id}` (member of that workspace) returning `WorkspaceDto` **plus** `logo_url?` / `primary_color?`. Do not overload entitlements.

---

## 7. Minimal code changes

### 7.1 Must change

| File | Function | Change |
|------|----------|--------|
| `apps/lazuar-api/Modules/One/Domain/Organization.cs` | aggregate | Add `LogoUrl`, `PrimaryColor`. `UpdateDetails` keeps name/slug. Add `UpdateBranding(logoUrl, primaryColor)` **or** extend `UpdateDetails` with optional brand args. Validate color hex; allow null to clear. |
| `apps/lazuar-api/Modules/One/Infrastructure/OneDbContext.cs` + new One migration | EF | `LogoUrl` text null; `PrimaryColor` `varchar(7)` null. |
| `packages/api-spec/modules/one/models/workspace.tsp` | DTOs | `UpdateWorkspaceRequestDto`: optional `logo_url`, `primary_color`. `WorkspaceDto`: same. New `PublicWorkspaceBrandingDto { name, slug, logo_url?, primary_color? }`. |
| `packages/api-spec/modules/one/routes.tsp` (or new public One tsp) | routes | `GET /one/workspaces/{id}` for members. Public `GET /public/one/{tenantSlug}/branding`. |
| `apps/lazuar-api/Modules/One/Application/Commands/UpdateWorkspaceCommand.cs` | handler | Persist branding. Still ADMIN membership. Clearing: send `null` / omit vs empty string — pick **empty or null clears**. |
| `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs` | PUT + new GET | Map new fields. GET 404 if no access / missing. |
| `apps/lazuar-api/Modules/One/Contracts/IOneQueryService.cs` + `OneQueryService.cs` | snapshot | Extend `WorkspaceSnapshotDto` **or** add `WorkspaceBrandingDto` so public GET does not need a new service. Prefer a dedicated branding record so email consumers do not churn. |
| New `…/One/Infrastructure/Endpoints/PublicWorkspaceBrandingEndpoints.cs` (name as you like) | `GET` | Slug → active org → `{ name, slug, logo_url, primary_color }`. Inactive / missing → 404. |
| `apps/lazuar-ops/src/modules/workspace/pages/GeneralSettingsPage.tsx` | page | Load `GET /one/workspaces/{id}`. Keep name + slug. Add logo upload (copy the BillingProfilePage presign PUT). Add `<input type="color">` or hex field. Save all four. Copy: “Shown on your hosted checkout. Legal invoices use Legal & Billing later.” |
| `apps/lazuar-portal/src/app/[tenantSlug]/layout.tsx` | server layout | Fetch public branding (`revalidate` ~60s). Set wrapper `style={{ ['--brand' as string]: color }}`. Missing branding (404 tenant) can stay passthrough; checkout product page already 404s. |
| `apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/layout.tsx` | header | Left: logo (`max-h-8`) or **workspace name**. Right: existing padlock + “Powered by Lazuar”. Need branding in this layout: fetch the same public GET (Next will dedupe) or pass via the tenant layout (React context / CSS only — **name/logo need the fetch here**). |
| `apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx` | CTA + legal | CTA uses `--brand` when set. Legal line: “transaction with **{name}**” instead of unnamed “the Creator.” Keep Lazuar terms links. |
| `apps/lazuar-portal/src/app/[tenantSlug]/checkout/[productSlug]/page.tsx` + `success/page.tsx` | metadata | `generateMetadata`: title `{product.name} · {branding.name}` (product fetch already exists). |
| `apps/lazuar-portal/src/app/[tenantSlug]/update-payment/[subId]/page.tsx` | header line | Show workspace name (and logo if cheap) above the arrears card so recovery is not anonymous. |

Regenerate `@repo/api-types-ts` / `Lazuar.ApiContracts` as this repo already does after TypeSpec edits.

### 7.2 Should change (same ticket, small)

| File | Change |
|------|--------|
| `OrderSummaryCard` | Optional one-line “Sold by {name}” under the product title. |
| `CheckoutSuccessView` | “Your order for {product} from {name} is confirmed.” |
| `BlindCheckoutLayout` | If no logo and no name fetch failure, still show slug as last-resort text — but prefer 404 tenant over a blank mark. |

### 7.3 Do not change

- `TenantBillingProfile` / `BillingProfilePage` / public billing profile (leave TIN leak for a billing ticket)
- `QuoteView` / `pay/[sessionId]`
- QuestPDF / `GenerateAndStoreDocumentCommandHandler` (LP-107)
- Gateway adapters / Stripe Checkout branding APIs
- `WorkspaceUpdatedIntegrationEvent` payload
- Messaging `TenantReplica`
- `ProductDto` / initiate checkout
- Sample cashier
- Root portal favicon
- Communications template variable list (`{{business_name}}` already correct)

### 7.4 Optional later (not required to close LP-025)

- Contrast check (reject yellow-on-white CTA)
- Image dimension / MIME allow-list on upload (PNG/JPG/WebP, max ~1 MB)
- Per-tenant favicon
- Apply `--brand` on portal dashboard header
- WhatsApp OG image (`CK-027`)
- Copy workspace logo into PDF if billing logo empty (Wave 2)

---

## 8. Tests to add

Portal has no test runner. Put contracts in **API module tests**. Manual smoke for ops + portal.

### 8.1 Domain / command

| Case | Expect |
|------|--------|
| `UpdateBranding(null, null)` | Clears both |
| `primary_color = "#0a7c42"` | Stored `#0A7C42` (or keep input case — pick one and test it) |
| `primary_color = "red"` / `"rgb(0,0,0)"` / `"#fff"` / `"#0a7c42ff"` | Reject |
| `logo_url = "javascript:alert(1)"` / `"data:image/png;base64,…"` | Reject |
| `logo_url = "https://cdn.example/vault/…/x.png"` | Stored |
| Name/slug still unique + slug rules unchanged | Existing `OrganizationSlugMustBeValidRule` still fires |

### 8.2 Public GET branding

File: `apps/lazuar-api/tests/Lazuar.ModuleTests/One/PublicWorkspaceBrandingTests.cs` (or query-service tests).

| Case | Expect |
|------|--------|
| Unknown slug | `null` / HTTP 404 |
| Inactive org | 404 (match `GetTenantIdBySlugAsync` which filters `IsActive`) |
| Active, no logo/color | 200 `{ name, slug, logo_url: null, primary_color: null }` |
| Active with brand | 200 those three extras |
| Body | No `tin`, `legal_name`, `id` (or if `id` is tempting, **omit it** — slug is enough) |

Architecture: path starts with `/api/v1/public` → still exempt.

### 8.3 Authenticated GET/PUT workspace

| Case | Expect |
|------|--------|
| Member GET `{id}` | name, slug, brand fields |
| Non-member GET | 401/403 |
| ADMIN PUT logo + color | persisted; GET public matches |
| Non-admin member PUT | still unauthorized (today’s rule) |

### 8.4 Manual (ops + portal)

1. New workspace: checkout header shows **name**, CTA stays default black.
2. Upload logo + pick color → refresh hop 1: logo left, CTA that color, legal line names the workspace.
3. Clear logo/color → back to name-only + default CTA.
4. Success + update-payment show the same name.
5. Unknown slug checkout still 404.
6. Do **not** open Legal & Billing; product checkout must work with no billing profile row.

---

## 9. Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| Using public billing profile “because logo exists” | High (TIN on a buy link) | New public DTO; never call `/public/billing/{slug}/profile` from product checkout |
| CSS injection via color | Med | Hex-only server validation |
| Hotlinking / XSS via `logo_url` | Med | https-only; `<img>` not `dangerouslySetInnerHTML`; R2 upload is already tenant-scoped |
| R2 unset → upload fails | Low | Same as hidden billing page; name-only still ships |
| Contrast / unreadable CTA | Low | Document “pick a dark brand color”; optional later check |
| Caching stale logo (portal `revalidate: 60`) | Low | Accept 60s; merchant refresh |
| Messaging/event payload churn | Low | Do not add fields to `WorkspaceUpdatedIntegrationEvent` |
| Becoming a theme kit | High if over-built | One color, one logo, one name. No fonts/CSS |
| Header wrap on mobile | Low | Logo `max-h-8`; name `truncate`; Lazuar mark can shrink to icon+text |

---

## 10. Acceptance criteria

Close LP-025 when all of the following are true:

1. Hosted product checkout header shows the **workspace name**, and the **logo** when `logo_url` is set.
2. When `primary_color` is set, the hop-1 pay CTA (and only that kind of accent) uses it; unset keeps today’s foreground button.
3. Those three values are edited on **General Settings** (`/workspace/general`), stored on `one.Organizations`, and read by portal via **public branding** — not via billing profile.
4. `GET /public/one/{tenantSlug}/branding` (or the chosen public path) returns only `{ name, slug, logo_url?, primary_color? }` and 404s for unknown/inactive slugs.
5. Checkout legal microcopy names the workspace, not an anonymous “Creator,” while Lazuar terms/privacy links remain.
6. Update-payment and success are not anonymous (name at least).
7. No new fonts, CSS upload, custom domain, OG image, TIN, or PDF changes.
8. “Powered by Lazuar” remains visible and secondary.
9. Tests in §8.1–8.3 exist and pass. Manual §8.6 (no billing profile required) is true.

Tracker can move `LP-025` from `P` → `Y` after that. `LP-107` stays `P` / Wave 2.

---

## 11. Suggested implement order

1. One migration + `Organization` branding fields + validation tests  
2. `UpdateWorkspace` + `GET /one/workspaces/{id}` + TypeSpec  
3. Public branding GET + tests  
4. Ops General Settings: GET + logo upload + color + save  
5. Portal tenant + checkout layouts: header mark + `--brand` + CTA + copy  
6. Metadata title + update-payment name  
7. Manual smoke §8.4  

Name-only (step 5 without logo/color) already fixes “who am I paying?” Do not ship name-only and close the ticket — tracker is logo **and** colors — but land name in the same PR as the columns so a merchant with no upload still looks like themselves.

That is the whole ticket.
