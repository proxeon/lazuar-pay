# 004 Maintenance — Future work (remaining modifications)

**Status:** Active backlog after maintenance track 00–18  
**Date:** 2026-08-09  
**Branch that closed the track:** `chore/backend-maintenance-004`  
**Locked decisions:** [`decisions.md`](./decisions.md)  
**Track close-out:** [`phase-18-done.md`](./phase-18-done.md)

This document describes **what is still intentionally unfinished**, why it was deferred, **when** to do it, and **what “done” looks like**. It is the handoff for future implementers — not a second 00–18 program.

**Phased execution checklists:**

| Detail level | Path |
|--------------|------|
| Coarse F00–F16 | [`checklists-future/README.md`](./checklists-future/README.md) |
| **Fine R00–R99 (prefer for implement)** | [`../005-remaining/checklists/README.md`](../005-remaining/checklists/README.md) |
| How-to analyses | [`../005-remaining/`](../005-remaining/) |

“Not one go” means **not one mega-PR**. A **long multi-phase checklist** is the intended way to implement this backlog.

---

## How to use this doc

| Audience | Use |
|----------|-----|
| Eng | Pick a workstream (FW-1…FW-7); follow “implementation outline”; open a focused PR |
| Product | Re-open Phase 16 / 00.2 / 00.3 only when triggers fire |
| Ops | Calendar: API key cutover after **2026-11-30** |

**Do not** treat this list as “must do before merge.” Maintenance track is already **healthy enough**. These are **future** modifications.

---

## Priority overview

| ID | Workstream | Urgency | Owner type |
|----|------------|---------|------------|
| **FW-1** | API key dual-read cutover (One-only middleware) | **Partial** — R05 code done; deploy gated on Q8 `active_legacy_only=0`; R06 table drop pending | Eng + Ops |
| **FW-2** | LHDN outbound webhooks → One dispatcher | **Product-scheduled** | Product + Eng |
| **FW-3** | BuildingBlocks product-port code moves | Opportunistic | Eng |
| **FW-4** | Cross-schema SQL / runtime boundary leaks | When touching area / P1 hygiene | Eng |
| **FW-5** | Optional module extract / merge (Phase 16) | **Only with product trigger** | Product + Eng |
| **FW-6** | TypeSpec Wave B + residual DX | Product DX | Eng |
| **FW-7** | Remaining god-file / test-fixture polish | Opportunistic | Eng |

---

# FW-1 — API key One-only cutover

## Status (2026-08-09)

**Partial:** R05 One-only middleware + One revoke subscription **implemented on** `chore/remaining-005` (code/docs).  
**Remaining:** staging/prod **DEPLOY** after Q8 `active_legacy_only = 0` (R04/R05.1/R05.5); table archive **R06** (FW-1.3).  
Notes: `plans/005-remaining/r05-notes.md`.

## Why deploy is gated

Removing dual-read **before** migrating remaining `lhdn.DeveloperApiKeys` rows causes **401** for legacy keys. Decisions locked dual-read until **2026-11-30** (or earlier if active legacy-only count is zero).

## What already exists

| Artifact | Path |
|----------|------|
| Locked dates | `decisions.md` §00.1 |
| Full design | [`api-key-cutover-design.md`](./api-key-cutover-design.md) |
| Inventory | [`phase-03-analysis.md`](./phase-03-analysis.md) |
| R05 code | One-only middleware; One revoke only; Lhdn dual-read closed in READMEs |

## Target end-state

1. Auth middleware queries **only** `one.ApiCredentials`.
2. Only **One** `ApiKeyRevokedIntegrationEvent` is subscribed (no Lhdn twin).
3. No application writes to `lhdn.DeveloperApiKeys`.
4. Optional later: drop/archive Lhdn key table ≥ **30 days** after One-only in prod.
5. LHDN “api-keys” HTTP routes (if kept) are pure façades over One.

## Calendar

| Milestone | Date | Action |
|-----------|------|--------|
| Dual-read allowed until | **2026-11-30** | May still read Lhdn table |
| One-only target | **2026-12-15** | Middleware + revoke event cleaned |
| Table drop | ≥ 30 days after One-only prod | Separate PR |

If **active** legacy row count is **zero** earlier, cutover may move **forward**.

## Implementation outline (future PR sequence)

### FW-1.1 — Measure and migrate (staging first)

- [ ] Count active rows: `lhdn."DeveloperApiKeys"` vs already-migrated hashes in `one."ApiCredentials"`.
- [ ] Implement **idempotent** migrator (job or ops script):
  - Copy `KeyHash` **as-is** (do not re-hash).
  - Copy prefix, hint, scopes, org, name, active flag.
  - Map scopes to One allowlist; quarantine unknown scopes.
- [ ] Dry-run staging; fix non-migratable rows (document in cutover design §4.3).
- [ ] Migrate production; verify auth for sample keys.
- [ ] Ensure list/revoke UI sees migrated keys (One only).

### FW-1.2 — Code cutover

- [x] `ApiKeyAuthenticationMiddleware`: remove Lhdn SQL branch. (R05 code)
- [x] `Program.cs` / composition: remove dual Lhdn revoke subscription. (R05 code)
- [x] Remove or gut Lhdn domain mint paths that could reintroduce inserts. (already façades; no reintro)
- [x] Tests: One-only auth; revoke invalidates cache; no Lhdn dual-read path. (R05)
- [x] Update Lhdn/One READMEs: dual-read window closed. (R05)

### FW-1.3 — Table archive (later)

- [ ] Monitoring window ≥ 30 days.
- [ ] EF migration drop/rename `lhdn.DeveloperApiKeys` (or archive schema).
- [ ] Architecture tests / docs updated.

## Risks

| Risk | Mitigation |
|------|------------|
| Integrator key only on Lhdn table | Complete migration before FW-1.2 |
| Scope allowlist rejects legacy scopes | Quarantine list + manual remap |
| Cache stale after revoke | Keep One revoke event + existing cache keys |

## Done when

Middleware One-only in prod; dual-read gone; revoke single-path; staging + prod verified; design doc marked **executed** with date.

---

# FW-2 — LHDN customer webhooks → One dispatcher

## Why deferred

End-state is **A** (route through One). Interim is **C freeze** (fire-and-forget + metrics only). Full A needs product decisions on **payload shape** and **signing** (body HMAC vs Standard Webhooks). Building a second Lhdn durable stack (**B**) is **rejected**.

## What already exists

| Artifact | Path |
|----------|------|
| Decision | `decisions.md` §00.2 |
| Inventory | [`phase-04-analysis.md`](./phase-04-analysis.md) |
| Freeze docs | `Modules/Lhdn/README.md` §5, `Modules/One/README.md` §7 |
| Observability | `WebhookSenderService` failure logs + `RecordWebhookFailed("lhdn")` |

## Target end-state

1. LHDN lifecycle events that customers subscribe to are delivered via One:
   - `WebhookDeliveryOutbox` + `OutboundWebhookDispatcherJob`
   - Same retry / DLQ / multi-endpoint model as platform webhooks
2. Fire-and-forget `WebhookSenderService` no longer used for those events (deleted or dead-code free).
3. Integrator docs describe the **new** delivery contract (or dual-verify window if signing changes).

## Product decisions before code (blockers)

Write answers before implementation PR:

1. **Signing:** keep LHDN body-only HMAC hex, or move to One `t=,v1=` Standard Webhooks-style?  
   - If change: dual-verify window + customer migration notice.
2. **Payload:** keep current LHDN JSON envelope, or wrap in One event envelope?  
3. **Routing:** which workspace `TenantWebhookEndpoint` rows receive LHDN events (all workspace endpoints vs filtered by event type)?
4. **Event types:** confirm list (`invoice.valid`, `invoice.invalid`, others?).

If product cannot share One signing without breaking customers, **re-open 00.2** and consider B with a formal ADR — do not silently fork.

## Implementation outline

### FW-2.1 — Design note (short ADR or plan appendix)

- [ ] Payload mapping table (LHDN event → outbox body).
- [ ] Signing dual-window plan if needed.
- [ ] Failure/retry semantics (inherit One job).

### FW-2.2 — Enqueue path

- [ ] On LHDN lifecycle (validated/invalid/etc.), enqueue One delivery commands for matching endpoints.
- [ ] Preserve correlation ids / organization ids.
- [ ] Tests: event → outbox row(s).

### FW-2.3 — Disable fire-and-forget

- [ ] Stop calling `WebhookSenderService` for migrated events.
- [ ] Remove service / Lhdn subscription tables if fully superseded (or keep table as config that maps into One endpoints — product choice).
- [ ] Metrics: retire or re-tag `RecordWebhookFailed("lhdn")` once path is One.

### FW-2.4 — Integrator honesty

- [ ] Update hub docs / api-spec descriptions.
- [ ] Changelog for breaking signing/payload if any.

## Done when

No customer LHDN webhook relies on fire-and-forget; One dispatcher is the only delivery path; tests + docs green; freeze section removed from Lhdn README.

---

# FW-3 — BuildingBlocks product-port moves

## Why deferred

Ownership map shipped (Phase 15). Moving LLM/email/metrics is multi-PR, no calendar deadline, high merge noise if bundled with endpoint refactors.

## What already exists

| Artifact | Path |
|----------|------|
| Ownership map | [`apps/lazuar-api/docs/009-building-blocks-ownership.md`](../../apps/lazuar-api/docs/009-building-blocks-ownership.md) |
| SharedKernel policy | `SharedKernel/README.md`, marker xmldoc |
| Metrics comment | `PlatformMetricsCollector` future plugin note |

## Recommended move order (opportunistic PRs)

| Order | Move | Destination | Notes |
|------:|------|-------------|--------|
| 1 | Port hygiene (interfaces in Application) | BB Application | When touching security/storage ports |
| 2 | LLM factory + policies + title generator | **Ops** | Sole orchestrator is Ops |
| 3 | Agent prompt / tool attributes | Ops.Contracts / Ops.Application | Billing implements via Contracts |
| 4 | `IEmailService` / Resend / template HTML | **Messaging** (+ Communications for BYOK) | Brand HTML out of BB |
| 5 | `IMessagingService` console/adapters | **Messaging** | Respect 00.4 freeze on multi-channel product |
| 6 | Magic-link token shapes | **Commerce** | Product-shaped |
| 7 | Document link payload helpers | Billing / Commerce | Generic HMAC may stay BB |
| 8 | Metrics contributors | Modules + thin BB aggregator | Kill LHDN/dunning SQL in BB collector |
| 9 | Per-module worker options | Each module | Shrink `BackgroundWorkerOptions` god bag |

## Implementation rules

1. **One concern per PR.**  
2. Architecture tests must stay green (BB ↛ Modules).  
3. Prefer **ports + DI move** over renaming types widely.  
4. Do **not** invent a Storage or Email module unless product lifecycle demands it (R2 stays thin shared port).  
5. Re-read 009 before each PR; update 009 when ownership changes.

## Done when (per move)

Module owns the product code; BB only has technical spine; 009 map updated; builds/tests green.

**Entire FW-3 “done”** only when map §3 items are moved or explicitly re-accepted as grey with rationale.

---

# FW-4 — Cross-schema SQL / runtime boundary leaks

## Why deferred

Compile-time modularity is largely real. Runtime SQL that JOINs foreign schemas **bypasses** Contracts. Each leak needs a local design (query service, integration event, denormalized copy) — not a global rewrite.

## What already exists

| Artifact | Path |
|----------|------|
| Coupling analysis | [`04-module-boundaries-modularization.md`](./04-module-boundaries-modularization.md) (esp. runtime leaks) |
| Host Application refs fixed | Phase 17 architecture test |

## Known leak classes to ticket (verify with current grep before PR)

Re-inventory before fixing — code moves may have shifted lines:

| Area | Typical smell | Fix direction |
|------|---------------|---------------|
| **Communications** | JOIN other modules’ tables for receipts / fulfillment context | `I*QueryService` on owning module Contracts; or denormalize ids into Communications |
| **Commerce** | Cross-module template / billing reads via raw SQL | Same |
| **Payments / platform auth** | Direct SQL into `one."GlobalUsers"` or similar | One query port |
| **PlatformMetricsCollector** | Multi-schema product SQL | FW-3 metrics contributors |
| **Any new code** | `FromSql` / Dapper across schemas | Block in review; prefer Contracts |

## Implementation outline (per leak)

1. **Reproduce** with a test or documented query.  
2. **Define port** on owning module’s Contracts (read model DTO).  
3. **Implement** in owning Infrastructure.  
4. **Replace** cross-schema SQL in consumer.  
5. **Optional:** architecture/integration test that forbids the old pattern if practical.  
6. **Do not** “fix” by moving more SQL into BuildingBlocks.

## Done when

No known production paths use private foreign-schema SQL without an approved exception recorded in 009 or an ADR; metrics no longer hardcode foreign domain status SQL.

---

# FW-5 — Optional module extract / merge (Phase 16)

## Why deferred

**No product trigger.** Decisions: no new modules; credits in Billing; Messaging frozen; webhooks stay in One.

## Do not start until gate is true

| Candidate | Reopen when |
|-----------|-------------|
| **Credits/Wallet** module | Credit monetization is product-critical **and** change-rate diverges from merchant ledger (not before ~**2027-02** per 00.5 unless product reopens earlier) |
| **Webhooks/Developer** module | Multi-endpoint delivery product **dominates** One’s change log |
| **Messaging → Communications** merge | Real multi-channel provider (e.g. WhatsApp) is **funded** and 00.4 reopened |

## Pre-work before any extract

- [ ] Written design note (events, schemas, dual-write if any).  
- [ ] Product sign-off.  
- [ ] Update `decisions.md` (reopen section).  
- [ ] Prefer **internal folders** first (`Billing/Wallet`, `Commerce/Dunning`) before new `.csproj`.

## Explicit rejects (still)

- Catalog / Identity / Dunning as separate modules “for tidiness”  
- Microservices split of the modular monolith  
- Community / Vault resurrection  

## Checklist / history

- [`checklists/phase-16-optional-extract-merge.md`](./checklists/phase-16-optional-extract-merge.md)  
- [`phase-16-done.md`](./phase-16-done.md) — gate not met  

## Done when

N/A until extract ships; then Contracts-only boundaries, host registration, TypeSpec, arch tests updated.

---

# FW-6 — TypeSpec Wave B and contract DX

## Why deferred

Phase 05 closed **P0** honesty (dual DTOs for listed surfaces, path slash, broadcast phantoms). Remaining items are product/DX polish.

## Remaining items (from Phase 05 deferrals)

| Item | Action |
|------|--------|
| Billing signed PDF download | Add to TypeSpec if public/admin API, or mark internal-only |
| Broadcast preview / status | Implement + TSP **or** remove from docs surface |
| Communications public compliance routes | Honesty pass (TSP ↔ Minimal API) |
| Payments product docs security schemes | OpenAPI security where routes require auth |
| Commerce **product** dual DTOs (`CreateProductRequest` locals, etc.) | Same pattern as Phase 05 P0 for subscribers |
| Optional path-honesty CI | Script/test: OpenAPI paths vs Minimal API maps |
| Admin-routes.tsp split | Optional if file pain returns |

## Implementation outline

1. Inventory remaining local C# DTOs that mirror OpenAPI.  
2. Prefer generated `Lazuar.ApiTypes` after TSP fix + `task gen`.  
3. Commit clients per repo policy.  
4. CI gate optional but high value long-term.

## Done when

No known dual DTO pairs on shipping surfaces; docs OpenAPI security accurate; optional CI honesty gate green.

---

# FW-7 — Remaining navigability polish

## Why deferred

Highest-ROI god files already split (One endpoints, Program, provision, dunning, public commerce, payment-completed, webhook handler). Residual files can wait until touched.

## Candidates

| File / area | Suggestion |
|-------------|------------|
| `LhdnGatewayAdapter` | Partials by operation (token, submit, status, TIN, cancel) |
| `LlmOrchestratorService` | Finish stream vs non-stream / tool partials |
| Payment gateway adapters | Shared name/amount utils only — no mega base class |
| `BillingQueryService` / `B2cConsolidationJob` | Partials when editing |
| ModuleTests → `Lazuar.TestSupport` | Gradual migration beyond 2 pilots |
| Outbox/inbox DI helper | `AddModuleOutboxInbox<T>` pilot then roll out |
| ProblemDetails | Expand stable `code` map as endpoints are touched |

## Done when

Opportunistic — no global deadline. Prefer house style when the file is already in the PR.

---

# Related parked product paths (not “architecture debt”)

These are **product freezes**, not incomplete refactors:

| Topic | Decision | Future work |
|-------|----------|-------------|
| **RevenueRecognitionJob** | Parked unregistered (00.3) | Finance/Xero epic owns schedule creation before any register |
| **WhatsApp / multi-channel** | Frozen (00.4) | Product funds provider → reopen 00.4 → possible Messaging merge (FW-5) |
| **Community / Vault** | Deleted | No rebuild; only historical schema drop ops if a DB still has schemas |

---

## Suggested ticket titles (copy-paste)

1. `feat(one): migrate legacy LHDN API keys and remove dual-read (FW-1)`  
2. `feat(webhooks): deliver LHDN lifecycle events via One dispatcher (FW-2)`  
3. `refactor(ops): move LLM factory from BuildingBlocks to Ops (FW-3)`  
4. `refactor(metrics): pluginize PlatformMetricsCollector contributors (FW-3/FW-4)`  
5. `fix(comms): replace cross-schema receipt SQL with Contracts query (FW-4)`  
6. `chore(api-spec): Wave B contract honesty — products + security schemes (FW-6)`  

---

## Explicit non-goals (still)

- Starting FW-5 extracts without product reopen  
- Building a **second** Lhdn durable webhook stack (decision B)  
- Early dual-read removal without migration (FW-1)  
- Hand-editing giant generated OpenAPI / EF snapshot files as “cleanup”  
- Frontend work except forced client regen from TypeSpec  

---

## Document maintenance

When a workstream completes:

1. Mark the section **Done** with date + PR link at the top of that FW section.  
2. Update `decisions.md` only if a lock changes (e.g. cutover completed early).  
3. Keep this file as the **single index** of post-004 future modifications.

**Last updated:** 2026-08-09  
