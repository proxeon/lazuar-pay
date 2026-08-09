# Phase 02 — Community / Vault documentation honesty

**Goal:** Stop teaching deleted modules as live systems.  
**PR shape:** Docs-only (backend docs + api-spec README + module READMEs).  
**Do not:** Drop production schemas in this phase without explicit DBA plan (optional later item).

---

## 02.1 TypeSpec package README

- [x] Rewrite `packages/api-spec/README.md` from current tree (`main.tsp`, `modules/*`)
- [x] Remove `modules/auth`, `modules/community`, community refund examples
- [x] Document real modules: one, commerce, payments, billing, lhdn, ops, communications, crm, platform, messaging (if models-only)
- [x] Document `docs-*.tsp` product-scoped docs purpose (ADR 007)
- [x] Document gen entry: `task gen` / `pnpm --filter @repo/api-spec build`

## 02.2 Backend module docs under `apps/lazuar-api/docs/`

For each file, either **archive** (move to `docs/archive/` or mark obsolete banner) or **rewrite** against Commerce/CRM/current modules:

- [x] `001-cross-module-communication.md` — remove Community subscription examples
- [x] `002-shared-kernel-vs-building-blocks.md` — remove Community circular-dep examples if present
- [x] `003-data-sanitization-domain-rule-alignment.md` — CommunityPlan/Subscription → Commerce/current or archive
- [x] `004-transactional-import-protocol.md` — **do not leave** seed steps for `community.*` schemas
- [x] `005-tenant-isolation-mapping-backfilling.md` — remove Community entities or archive
- [x] `006-payment-webhook-idempotency-backfilling.md` — remove community lifecycle SQL or archive
- [x] Keep valuable runbooks (`007-outbox…`, `008-password…`) if still accurate — skim for Community mentions

## 02.3 Module README copy

- [x] `Modules/Messaging/README.md` — consumers are not Community/Vault
- [x] `Modules/CRM/README.md` — same
- [x] Other module READMEs: grep `Community|Vault` and fix

## 02.4 Code comments / config comments (light touch)

- [x] `AppOptions` / appsettings comments: “Community Enrollment” → portal/checkout language
- [x] `packages/api-spec/modules/messaging/models.tsp` comment “Templates migrated to Community” → Communications ownership
- [x] Template preview sample URL `community.lazuar.com` → portal/hub path if easy (else ticket for Phase Communications copy)

## 02.5 Template orphan names (do not delete blindly)

- [x] Document that `DefaultMessageTemplates.OrphanNames` still lists Community* strings until ops run legacy-cleanup
- [x] Optional: add comment above OrphanNames explaining ADR 022 cleanup
- [x] Do **not** remove names until product confirms cleanup endpoint has been run on all tenants

## 02.6 Keep (do not delete)

- [x] Confirm `DropLegacySchemas` migration remains in One migrations history
- [x] Confirm no attempt to re-add Community/Vault modules

## 02.7 Optional schema drop (separate PR, ops-gated)

- [x] Check whether any shared DB still has `community` / `vault` schemas
- [x] If yes: write separate runbook + migration; backup note; **not** same PR as doc pass
- [x] If no: mark N/A

**02.7 result:** N/A for this PR. `DropLegacySchemas` already drops `community`/`vault` in One migrations history. No additional schema-drop work in this docs-only phase. Live shared-DB verification remains ops-gated outside the repo.

## 02.8 Exit criteria

- [x] Grep `Community|Vault` under `packages/api-spec/README.md` and `apps/lazuar-api/docs/**` returns only intentional historical/archive mentions
- [x] New engineer reading api-spec README sees current CaaS module tree
