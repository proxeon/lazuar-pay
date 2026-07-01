
# ADR 022: Hide and Plan Removal of Community & Vault Modules

**Date:** July 2026  
**Status:** Accepted (Phase 1 — Hide — complete; Phase 2 — Removal — planned for next iteration)  
**Context:** Modular Monolith, Frontend, API Contract, Product Strategy

## Context & Problem Statement

The Community (Spaces) and Vault (Digital Files) modules were built as part of the earlier "Super App" feature factory. ADR 021 pivoted the platform to a Compliance-First Checkout Engine and explicitly **killed** community DRM and vault-style digital asset hosting as "Vitamin" features outside the core transaction/compliance loop.

Rather than delete the modules immediately (irreversible, and risky given cross-module dependencies we had not fully mapped), we are doing this in two phases:

1. **Phase 1 (current, reversible):** Hide Community & Vault from the frontend and disable their backend wiring via commented-out code.
2. **Phase 2 (next iteration):** Remove the module codebases, contracts, generated types, and (eventually) database schemas entirely.

This ADR documents both the current hide state and the Phase 2 removal plan. Inline `TODO(ADR-022)` comments at the hide sites (`Program.cs`, `main.tsp`, `Sidebar.tsx`) point here.

## Decision

### Phase 1 — Hide (done)
- **Backend (`Program.cs`):** Community & Vault DI, MediatR assembly scanning, event subscriptions, and endpoint mapping are commented out. Only Vault's `/admin/vault/presigned-url` endpoint is re-exposed, because it is a shared R2 file-upload utility used by the active workspace `BillingProfilePage`. It depends only on shared `IR2StorageService` + `IExecutionContextAccessor`, not on Vault DI/DbContext/workers.
- **ops-page:** Community Spaces & Vault entries removed from the sidebar `MODULES` array and `App.tsx` routes. The Communications module (Broadcasts/Templates, which share the `/community/*` URL prefix) is preserved. Community/Vault fulfillment targets are filtered out of the Commerce product UI via `filterHiddenFulfillmentTargets` (`lib/utils.ts`).
- **portal-page:** Community (Telegram/Zoom) and Vault (Digital Vault downloads) sections removed from the aggregated dashboard; `/community/portal` route returns `notFound()`; checkout fulfillment badges removed from `OrderSummaryCard`.
- **TypeSpec (`main.tsp`):** Community & Vault imports commented out. Types **not** regenerated (see Phase 2 blocker).

### Phase 2 — Full Removal (planned, next iteration)

**Critical prerequisite:** relocate the `/admin/vault/presigned-url` endpoint out of Vault (into a shared building block, or `One`/`Ops`) and update `BillingProfilePage.tsx` to the new path. Until this is done, Vault cannot be deleted.

**Removal sequence (ordered):**
1. Relocate `presigned-url` out of Vault → update `BillingProfilePage`.
2. Strip defaults: remove `COMMUNITY` from `provision_apps` in `RegisterPublicUserCommand.cs` and `CreateWorkspaceModal.tsx`.
3. Delete frontend orphans:
   - ops-page: `modules/vault/`, `modules/community/pages/SpacesPage.tsx`, `modules/community/components/{SpaceDetailPanel,CreateSpaceModal,MessageTemplateEditor}.tsx`, `hooks/use-product-associations.ts`
   - portal-page: `modules/community/components/CommunityPortalView.tsx`, `modules/community/lib/api.ts`, `app/[tenantSlug]/community/portal/`
4. Resolve the Communications folder collision: `BroadcastsPage`/`TemplatesPage` live in `modules/community/` but belong to the Communications module. Either selectively delete only the Spaces files, or move the Communications pages into a `modules/communications/` folder (preferred).
5. Delete TypeSpec: `packages/api-spec/modules/{community,vault}/`, `docs-community.tsp`, `docs-vault.tsp`, and the `main.tsp` imports.
6. Delete backend modules: remove `Modules/Community/` + `Modules/Vault/` directories, drop them from `Lazuar.slnx`, remove them from `ModuleBoundaryTests.cs` (`_moduleNamespaces`), remove them from the Taskfile (`api:db:migrate`, `api:migrations:purge`, `api:migrations:init`), and drop the unused `ProjectReference` to `Community.Contracts` in `One.Application`/`Messaging.Application` csproj.
7. Regenerate: run `task gen` — now safe; `api-types-ts` and `api-types-dotnet` lose Community/Vault schemas cleanly.
8. Database: add a migration to drop the `community`/`vault` schemas. **Recommendation:** leave the tables dormant initially and drop in a later migration for rollback safety.

## Consequences

### Positive
- Phase 1 is fully reversible (uncomment / `git revert`) — a low-risk way to validate the change with real traffic before deleting code.
- Phase 2 eliminates dead code, shrinks the build/test surface, unblocks `task gen`, and aligns the codebase with ADR 021's strategic scope.

### Trade-offs & Risks
- **Fulfillment breakage:** products configured with `internal:community`/`internal:vault` fulfillment targets no longer provision post-purchase, and existing customers lose portal access to those resources. Audit existing products/customers before/at Phase 2.
- **`task gen` is unsafe during Phase 1:** regenerating types removes Community/Vault schemas that the still-present C# module projects (and orphan frontend files) depend on. This is resolved only at Phase 2 step 7.
- **Database removal is irreversible:** schema drops cannot be rolled back without restoring from backup. Phase 2 step 8 deliberately defers the drop.
- **Naming collision:** the word "community" also appears in the Communications module and in generic prompt text (e.g. `superadmin-page/src/lib/prompt-library.ts`). Phase 2 removal must target module code paths, not string-match on "community".
- **Reversibility changes at Phase 2:** once projects and DB schemas are deleted, re-enabling means git-restore + schema recreation, not uncommenting.

## Open Decisions
- [ ] Customer comms + data plan for existing community/vault product purchasers.
- [ ] Whether to reorganize `modules/community/` → `modules/communications/` for the Communications pages during Phase 2.
- [ ] Timing of the DB schema drop (immediate vs. deferred).
- [ ] Where to relocate `presigned-url` (building block vs. `One` vs. `Ops`).
