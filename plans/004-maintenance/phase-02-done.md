# Phase 02 — Done

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Commit subject:** `docs(api): Community/Vault honesty pass (phase 02)`

## What landed

### Rewritten

1. `packages/api-spec/README.md` — current module tree (one, commerce, payments, billing, lhdn, ops, communications, crm, platform, messaging); ADR 007 `docs-*.tsp`; gen via `task gen` / `pnpm --filter @repo/api-spec build`
2. `apps/lazuar-api/docs/001-cross-module-communication.md` — Commerce/CRM/Payments examples
3. `apps/lazuar-api/docs/002-shared-kernel-vs-building-blocks.md` — Commerce/One circular-dep example
4. `apps/lazuar-api/docs/003`–`006` — obsolete banners and/or Commerce retargeting; community.* SQL no longer presented as runnable
5. `Modules/Messaging/README.md`, `Modules/CRM/README.md` — live consumers only
6. Light fixes: Billing, Payments, One module READMEs

### Light code/comment fixes

- `AppOptions` ClientUrl comment → portal/checkout
- `packages/api-spec/modules/messaging/models.tsp` → Communications ownership
- `TemplateEndpoints` preview `{{renewal_link}}` → `https://portal.lazuar.com/checkout`
- `DefaultMessageTemplates.OrphanNames` comment documents ADR 022 retain-until-cleanup

### Confirmed kept

- `Modules/One/Infrastructure/Migrations/20260704104342_DropLegacySchemas.cs` still drops `community`/`vault`
- No Community/Vault modules re-added

### Checklist

- `checklists/phase-02-community-vault-doc-honesty.md` marked complete
- Analysis: `phase-02-analysis.md`

## Verification

- Grep `Community|Vault` under `packages/api-spec/README.md` and `apps/lazuar-api/docs/**` is only intentional historical/obsolete banners
- No live `INSERT INTO community.*` playbooks remain
- `community.lazuar.com` removed from template preview mock

## Next

Phase 03+ per `plans/004-maintenance/checklists/`.
