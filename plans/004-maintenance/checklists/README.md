# 004 — Implementation checklists (backend + TypeSpec maintenance)

**Status:** Ready to execute (no code changes required to start Phase 00)  
**Date:** 2026-08-09  
**Scope:** `apps/lazuar-api`, `packages/api-spec`, related contracts/tests/CI/gen pipeline  
**Out of scope:** Frontend apps, Caddy/DX gateway (already shipped)

## How to use

1. Work **one phase at a time** (or one PR per phase where noted).
2. Prefer **many small PRs** over one mega maintenance PR.
3. Check items only when done with evidence (test/gen/CI).
4. Full analysis (why) lives in parent `../01-…`–`../10-…` reports — these checklists are **what to do**.
5. Do **not** invent new modules until Phase 15 product triggers are met.

## Phase map

| Phase | File | Horizon | Goal |
|-------|------|---------|------|
| 00 | [`phase-00-align-decisions.md`](./phase-00-align-decisions.md) | Align | Lock product end-states before dual-path work |
| 01 | [`phase-01-secrets-and-dead-residue.md`](./phase-01-secrets-and-dead-residue.md) | H1 Safety | Secrets, dead files, gen twins |
| 02 | [`phase-02-community-vault-doc-honesty.md`](./phase-02-community-vault-doc-honesty.md) | H1 Safety | Kill Community/Vault fiction in backend docs |
| 03 | [`phase-03-dual-api-keys-cutover.md`](./phase-03-dual-api-keys-cutover.md) | H1 Honesty | One credentials only (end dual LHDN keys) |
| 04 | [`phase-04-webhooks-converge.md`](./phase-04-webhooks-converge.md) | H1 Honesty | One durable webhook story |
| 05 | [`phase-05-typespec-contract-honesty.md`](./phase-05-typespec-contract-honesty.md) | H1 Honesty | TypeSpec ↔ Minimal API ↔ generated clients |
| 06 | [`phase-06-ci-taskfile-alignment.md`](./phase-06-ci-taskfile-alignment.md) | H1 Honesty | CI matches Taskfile test surface |
| 07 | [`phase-07-one-endpoints-split.md`](./phase-07-one-endpoints-split.md) | H2 Nav | Split One `Endpoints.cs` (house style) |
| 08 | [`phase-08-program-composition.md`](./phase-08-program-composition.md) | H2 Nav | Thin `Program.cs` into composition helpers |
| 09 | [`phase-09-provision-command-split.md`](./phase-09-provision-command-split.md) | H2 Nav | Decompose Aura provision command |
| 10 | [`phase-10-dunning-engine-split.md`](./phase-10-dunning-engine-split.md) | H2 Nav | Split `DunningEngineJob` |
| 11 | [`phase-11-more-god-file-splits.md`](./phase-11-more-god-file-splits.md) | H2 Nav | Payment-completed, public commerce, webhooks, LHDN gateway |
| 12 | [`phase-12-folder-alignment.md`](./phase-12-folder-alignment.md) | H2 Nav | Messaging Workers, endpoint folders elsewhere |
| 13 | [`phase-13-test-fixtures-and-errors.md`](./phase-13-test-fixtures-and-errors.md) | H2 Nav | Shared fixtures, ProblemDetails, paging |
| 14 | [`phase-14-typespec-structure-polish.md`](./phase-14-typespec-structure-polish.md) | H2 Nav | Split large TSP models; orphans |
| 15 | [`phase-15-building-blocks-thin.md`](./phase-15-building-blocks-thin.md) | H3 Fit | Move product ports out of BB |
| 16 | [`phase-16-optional-extract-merge.md`](./phase-16-optional-extract-merge.md) | H3 Fit | Credits / Webhooks / Messaging merge (triggered only) |
| 17 | [`phase-17-deferred-jobs-and-flags.md`](./phase-17-deferred-jobs-and-flags.md) | H1–H2 | Revenue recognition, probes, freeze list |
| 18 | [`phase-18-definition-of-done.md`](./phase-18-definition-of-done.md) | Meta | When maintenance track is “healthy” |

## Recommended execution order

```
00 → 01 → 02 → 17 (quick product freezes)
    → 05 (contracts) in parallel with 06 (CI) if capacity
    → 03 → 04 (dual paths — need 00 decisions)
    → 07 → 08 → 09 → 10 → 11 → 12 → 13 → 14
    → 15 when touching BB
    → 16 only with product trigger
    → 18 continuous exit criteria
```

## PR hygiene rules (every phase)

- [ ] Backend-only or TypeSpec-only unless regen forces client packages
- [ ] `dotnet build` / relevant tests green for touched modules
- [ ] `task gen` if TypeSpec changed; commit clients if policy requires
- [ ] Architecture tests green if boundaries moved
- [ ] No outbox type renames without a deliberate migration note
- [ ] Do not hand-edit `*ModelSnapshot.cs`, NSwag mega-files, or openapi-typescript blobs

## Related analysis (read when stuck)

| Topic | Report |
|-------|--------|
| Delete inventory | `../01-removable-dead-code.md` |
| File splits | `../02-large-files-chunking.md` |
| Folders | `../03-folder-organization.md` |
| Modules | `../04-module-boundaries-modularization.md` |
| TypeSpec | `../05-typespec-contracts.md` |
| BuildingBlocks | `../06-building-blocks-shared-kernel.md` |
| Tests/migrations | `../07-tests-migrations-hygiene.md` |
| Program/DI | `../08-composition-di-endpoints.md` |
| Duplication | `../09-duplication-tech-debt.md` |
| Roadmap Qs | `../10-maintenance-questions-roadmap.md` |
