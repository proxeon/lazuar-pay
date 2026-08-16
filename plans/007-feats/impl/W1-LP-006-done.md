# W1-LP-006 — done

A stranger can open Hub `/pricing`, read an honest card, and create a workspace on `/signup` **without a Superadmin queue**. `POST /one/public/register` still writes user + org + ADMIN + `OPS,BILLING,PAYMENTS,CRM,LHDN` and issues `lazuar_auth`. `AppAccessRequest` was README fiction and was not built.

Pricing is **not** a GMV take. The card hard-codes **0%** of guest sales and shows the **Hub Starter SaaS plan from LP-004** (`Saas:Plan`). Repo `AmountMyr` is **0**, so the page says checkout software is **free today**. Credit packs and starter grant come from `ICreditCostService` (same config as billing). SST footnote uses `Saas:Seller` (0% + reason). LHDN / WhatsApp are **not** sold as live products.

Logged-out `/` on ops goes to `/pricing`. Authenticated `/` still goes to `/commerce/dashboard`. No marketing site. No card/KYC at signup.

Tracker LP-006 Lazuar **N → Y**.

## Files changed

### Spec + types

- `packages/api-spec/modules/one/models/pricing.tsp` — **new.** `PublicPricingDto` + hub plan + packs.
- `packages/api-spec/modules/one/models/auth.tsp` — `accepted_terms` on register.
- `packages/api-spec/modules/one/routes.tsp` — anonymous `GET /one/public/pricing`.
- `task gen` — OpenAPI + `@repo/api-types-ts` + `Lazuar.ApiContracts`.

### Backend

- `AuthEndpoints.cs` — `GET /public/pricing`; register requires `accepted_terms=true`; token-bucket 10 / 10 min per email+IP.
- `GetPublicPricingQuery` + `GetPublicPricingQueryHandler` — `gmv_take_percent` is a constant `0`; hub plan / SST from config; packs from `ICreditCostService`; `lhdn_credits_live` / `whatsapp_credits_live` stay `false`.
- `PublicRegisterRateLimiter` — **new.** Registered in One DI.
- `RegisterPublicUserCommand` — validate slug (`Organization` ctor) **before** tracking a user.
- `Modules.One.Infrastructure.csproj` — `Billing.Contracts` (credits only).
- `Modules/One/README.md` — delete queue / `AppAccessRequest` / `one.AppAccessRequests`. Instant public register.

### Ops (public funnel)

- `App.tsx` — public `/pricing`, `/signup`, `/login`; `HomeRedirect` on `/`.
- `PricingPage.tsx` — **new.** RM 0 on sales; Hub plan; packs; SST; not-MoR copy; CTA `/signup`.
- `LoginPage.tsx` — `/signup` and `?mode=signup`; TOS/Privacy clickwrap → `/portal/legal/*`; `accepted_terms`; pricing link.

### Docs + tracker

- `apps/lazuar-docs/docs/integrations/hub-vs-diy.md` — one link to Hub `/pricing` (no numbers).
- `plans/007-feats/00-checklist-tracker.md` — LP-006 **Y**.

### Tests

- `RegisterPublicUserCommandHandlerTests` — happy path (ADMIN + 5 apps, not COMMERCE, not system admin); duplicate email; taken slug; reserved / malformed slug writes nothing; empty name → email local-part; no `AppAccessRequest` type or DbSet.
- `OrganizationSlugMustBeValidRuleTests` — valid 3/63/`acme`; broken shapes; every reserved slug.
- `GetPublicPricingQueryHandlerTests` — GMV 0 even if a 5 is planted in config; packs 50/500, 100/1100, 200/2500; `AmountMyr=0` ⇒ checkout free; positive plan is a software fee, still 0% GMV.
- `PublicRegisterRateLimiterTests` — 11th acquire denied.
- `TenantIsolationArchitectureTests` — `/api/v1/one/public/pricing` tenant-exempt.

## Tests run

- `Lazuar.ModuleTests` filter `RegisterPublicUserCommandHandlerTests|OrganizationSlugMustBeValidRuleTests|GetPublicPricingQueryHandlerTests|CreateWorkspaceCommandHandlerTests|WorkspaceCreateAuthorizationTests|PublicRegisterRateLimiterTests` — **39 passed**, 0 failed, 0 skipped. Duration 87 ms.
- `Lazuar.ArchitectureTests` — **14 passed**, 0 failed, 0 skipped. Duration 616 ms.
- `npx tsc --noEmit -p apps/lazuar-ops/tsconfig.json` — clean.

Manual Hub `/pricing` → `/signup` → cookie → dashboard **not run** here.

Not committed. Not pushed.

No `applicationFee`. No waitlist. No Hub Pro RM invented — listed amount is config (`0` in repo). Extra workspaces are LP-184.
