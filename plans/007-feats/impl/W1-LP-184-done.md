# W1-LP-184 — done

A signed-in human can create a **new** workspace (slug + name) without SQL or Superadmin, including when they have **zero** entitlements. Register still creates workspace #1. Switcher “Create New Workspace” still works.

Empty-entitlement Ops is no longer “Access Denied.” It is **Create your workspace** + logout, mounting the same `CreateWorkspaceModal` (`POST /one/workspaces`, `provision_apps` OPS/BILLING/PAYMENTS/CRM/LHDN). Success awaits entitlements refetch then `onWorkspaceSelect` → dashboard.

Slug rules match register: shared `slugify` / `RESERVED_SLUGS` / `validateSlug`. API still rejects reserved / taken slugs. TOS checkbox lives on **register** (LP-006 clickwrap); create-another does not block on legal pages.

Tracker LP-184 Lazuar **P → Y**.

## Files changed

### API (existing command, tighter)

- `CreateWorkspaceCommand` — normalize slug to lowercase before uniqueness + `Organization` ctor. Still only needs `UserId` (no prior membership).
- No provision-secret path change. `API_CLIENT` still cannot create human workspaces (JWT `RequireAuthorization` + `UserId`).

### Ops

- `App.tsx` — zero entitlements → `EmptyWorkspaceState` (not logout-only).
- `EmptyWorkspaceState.tsx` — **new.**
- `lib/workspace-slug.ts` — **new.** Shared with register + modal.
- `CreateWorkspaceModal.tsx` — same slug helpers as signup; await entitlements invalidate; show API `detail` on failure.
- `LoginPage.tsx` — TOS on register (LP-184 should + LP-006 must).

### Tracker

- `plans/007-feats/00-checklist-tracker.md` — LP-184 **Y**.

### Tests

- `CreateWorkspaceCommandHandlerTests` — authenticated user, **no memberships**, 200 id + ADMIN + core apps; duplicate slug 400/`already taken`; reserved slug `BusinessRuleValidationException`; missing user.
- `WorkspaceCreateAuthorizationTests` — `POST /workspaces` requires auth + empty `UserId` → 401; `GET /public/pricing` stays anonymous.
- Slug rule tests shared with LP-006 (`OrganizationSlugMustBeValidRuleTests`).

## Tests run

- `Lazuar.ModuleTests` filter `RegisterPublicUserCommandHandlerTests|OrganizationSlugMustBeValidRuleTests|GetPublicPricingQueryHandlerTests|CreateWorkspaceCommandHandlerTests|WorkspaceCreateAuthorizationTests|PublicRegisterRateLimiterTests` — **39 passed**, 0 failed, 0 skipped. Duration 87 ms.
- `Lazuar.ArchitectureTests` — **14 passed**, 0 failed, 0 skipped. Duration 616 ms.
- `npx tsc --noEmit -p apps/lazuar-ops/tsconfig.json` — clean.

Manual empty-entitlement create in Ops **not run** here.

Not committed. Not pushed.

Invite-member UI, email verify, archive UX, and provision secret remain out of scope.
