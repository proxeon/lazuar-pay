# 004 Maintenance — Future work (remaining modifications)

**Status:** **005 wave closed** (`chore/remaining-005`, R99) — implementable streams done or SKIP; **ops residual** only (keys migrate+deploy One-only, webhook staging migrate, table-drop clocks). See [`../005-remaining/r99-notes.md`](../005-remaining/r99-notes.md).  
**Date:** 2026-08-09  
**Branch that closed the track:** `chore/backend-maintenance-004`  
**005 execution branch:** `chore/remaining-005`  
**Locked decisions:** [`decisions.md`](./decisions.md)  
**Track close-out:** [`phase-18-done.md`](./phase-18-done.md)

This document describes **what is still intentionally unfinished**, why it was deferred, **when** to do it, and **what “done” looks like**. After R99, residuals are **normal ops tickets**, not an open mega-program.

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
| Eng | Prefer residual ops tickets (below) or opportunistic follow-ups; completed FW streams are closed |
| Product | Re-open Phase 16 / 00.2 / 00.3 only when triggers fire |
| Ops | Residual tickets: keys migrate+deploy One-only; webhook staging migrate; R06 table drop after ≥30d One-only prod. Dual-read calendar until **2026-11-30** |

**Do not** treat closed FW streams as open mega-program work. After R99, only ops residuals + product-gated extract remain.

---

## Priority overview

| ID | Workstream | Urgency | Owner type |
|----|------------|---------|------------|
| **FW-1** | API key dual-read cutover (One-only middleware) | **Partial** — code complete; **ops** migrate + deploy One-only; R06 dated | Eng + Ops |
| **FW-2** | LHDN outbound webhooks → One dispatcher | **Done** — R40–R43 (staging/prod ops residual) | Product + Eng |
| **FW-3** | BuildingBlocks product-port code moves | **Done** — R30–R35 | Eng |
| **FW-4** | Cross-schema SQL / runtime boundary leaks | **Done** — R11–R15 fixed; R16→R35; R17→R05 | Eng |
| **FW-5** | Optional module extract / merge (Phase 16) | **SKIP (R60)** — only with product trigger | Product + Eng |
| **FW-6** | TypeSpec Wave B + residual DX | **Done** — R20–R25 | Eng |
| **FW-7** | Remaining god-file / test-fixture polish | **Done** — R50–R53 (wave scope) | Eng |

---

# FW-1 — API key One-only cutover

## Status (2026-08-09)

**Partial — code complete, ops/deploy residual** (R99).

| Band | State |
|------|--------|
| R01–R03 | **Code done** (inventory, migrator, runbook) |
| R04 | **Ops pending** — staging/prod migrate |
| R05 | **Code on branch**, deploy-gated on Q8 `active_legacy_only = 0` |
| R06 | **Deferred** — ≥30d after One-only **in prod** (clock not started) |

Notes: `plans/005-remaining/r04-notes.md`, `r05-notes.md`, `r06-notes.md`, `r99-notes.md`.

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

**Status: Done** (R40–R43, 2026-08-09). Staging/prod verification remains ops (R42.4 exit).

## Outcome

End-state **A** shipped:

1. LHDN lifecycle customer webhooks (`invoice.valid` / `invoice.invalid`) deliver via One:
   - `DispatchExternalWebhookCommand` → `OutboundWebhookRequestedIntegrationEvent` → `WebhookDeliveryOutbox` + `OutboundWebhookDispatcherJob`
   - Same retry / multi-endpoint model as platform webhooks
2. Fire-and-forget `WebhookSenderService` / `IWebhookSenderService` **deleted** (R43). No `RecordWebhookFailed("lhdn")` call sites.
3. Docs: Lhdn README §5 (One path; C freeze removed); One README §7 lists invoice events.

## Product locks (R40)

See `plans/005-remaining/webhook-convergence-decisions.md`:

1. **Signing:** One Standard Webhooks–style (`t=,v1=`); dual-sign window skipped.
2. **Payload:** data-only snake_case; One wraps platform envelope.
3. **Routing:** fan-out `TargetUrl: null` → endpoints with matching `EnabledEvents`.
4. **Event types:** `invoice.valid`, `invoice.invalid` only.

## Implementation checklist (closed)

### FW-2.1 — Design note

- [x] Payload mapping / signing / retry — R40 decisions + R42 notes.
- [x] Dual-window plan — **skipped** (hard cut after R41).

### FW-2.2 — Enqueue path (R42)

- [x] On VALID/INVALID, publish `OutboundWebhookRequestedIntegrationEvent` via LhdnEventBus.
- [x] Org id + data payload preserved; tests green.

### FW-2.3 — Disable fire-and-forget (R43)

- [x] No `IWebhookSenderService` on customer path.
- [x] Service + interface deleted; DI registration removed.
- [x] Metrics: pure Lhdn failure counter call sites gone (`RecordWebhookFailed` remains for `outbound` / `payment`).
- [x] `lhdn.WebhookSubscriptions` **kept** (optional later drop / façade).

### FW-2.4 — Integrator honesty

- [x] Lhdn/One module READMEs updated.
- [x] Signing/payload change documented in R40/R42 notes (breaking vs legacy body-only HMAC).

## Residual (not FW-2 exit blockers)

- Staging/prod: confirm durable outbox delivery on live env (R42.4).
- Optional: dual-write/façade `/lhdn/webhooks` over One; drop `lhdn.WebhookSubscriptions` after period.
- Expand catalog beyond `invoice.valid` / `invoice.invalid` if product asks.

## Evidence

| Artifact | Path |
|----------|------|
| Decisions | `plans/005-remaining/webhook-convergence-decisions.md` |
| Notes | `plans/005-remaining/r40`…`r43-notes.md` |
| Lhdn path | `Modules/Lhdn/README.md` §5 |
| One path | `Modules/One/README.md` §7 |

---

# FW-3 — BuildingBlocks product-port moves

**Status: Done** (R30–R35 on `chore/remaining-005`, 2026-08-09).

## Outcome

Planned BuildingBlocks product-port moves for the 005 wave landed:

| Phase | Move | State |
|-------|------|--------|
| R30 | Port hygiene (`IJwtService`, `IR2StorageService` → Application) | Done |
| R31 | LLM factory / policies / title → **Ops** | Done |
| R32 | Agent tools / prompt port → Ops.Contracts | Done |
| R33 | Magic-link shapes → **Commerce** | Done |
| R34 | Email + `IMessagingService` → **Messaging** | Done |
| R35 | Metrics plugins + outbox schema registration | Done |

## What already exists

| Artifact | Path |
|----------|------|
| Ownership map | [`apps/lazuar-api/docs/009-building-blocks-ownership.md`](../../apps/lazuar-api/docs/009-building-blocks-ownership.md) |
| SharedKernel policy | `SharedKernel/README.md`, marker xmldoc |
| Notes | `plans/005-remaining/r30-notes.md` … `r35-notes.md` |

## Soft / out-of-wave residuals (not FW-3 blockers)

- M4 dunning counter still soft BB process counter (R35 deferred)
- Document link payload helpers may stay BB (generic HMAC)
- Per-module worker options bag shrink — opportunistic when touching workers
- Further 009 grey items: re-accept or ticket when touched

## Implementation rules (still)

1. **One concern per PR.**  
2. Architecture tests must stay green (BB ↛ Modules).  
3. Prefer **ports + DI move** over renaming types widely.  
4. Do **not** invent a Storage or Email module unless product lifecycle demands it.  
5. Re-read 009 before each PR; update 009 when ownership changes.

---

# FW-4 — Cross-schema SQL / runtime boundary leaks

**Status: Done** (R10–R17 / R35 on `chore/remaining-005`, 2026-08-09).

## Outcome

| Phase | Leak | State |
|-------|------|--------|
| R11 | L-01 document published (Communications) | Fixed |
| R12 | L-02 platform superadmin (Payments→one) | Fixed |
| R13 | L-03 arrears update (Commerce) | Fixed |
| R14 | L-05 document lookup CRM | Fixed |
| R15 | L-04 dead template SQL | Fixed |
| R16 → R35 | L-06 metrics multi-schema product SQL | Resolved via metrics plugins |
| R17 → R05 | L-07 API-key dual-read SQL | Resolved via One-only middleware code |

## What already exists

| Artifact | Path |
|----------|------|
| Coupling analysis | [`04-module-boundaries-modularization.md`](./04-module-boundaries-modularization.md) |
| Live inventory | `plans/005-remaining/cross-schema-leaks-live.md` |
| Notes | `plans/005-remaining/r11-notes.md` … `r17-notes.md`, `r35-notes.md` |

## Ongoing hygiene (not open program)

| Area | Rule |
|------|------|
| **Any new code** | `FromSql` / Dapper across schemas blocked in review; prefer Contracts |
| **Approved exception** | Platform metrics may query registered `{schema}.OutboxMessages` / `InboxMessages` only (009) |
| New leaks | Ticket locally when found — not a “reopen FW-4 mega program”

---

# FW-5 — Optional module extract / merge (Phase 16)

**Status: SKIP (R60, 2026-08-09)** — no product trigger; decisions not reopened.  
Notes: `plans/005-remaining/r60-notes.md`.

## Why SKIP / deferred

**No product trigger.** Decisions: no new modules; credits in Billing; Messaging frozen; webhooks stay in One. R00 selected Extract **NO**; R60 gate all unchecked → **SKIP and stop**.

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
- R60: `plans/005-remaining/checklists/r60-extract-gate-only.md` — **SKIP**

## Done when

N/A until extract ships; then Contracts-only boundaries, host registration, TypeSpec, arch tests updated.

---

# FW-6 — TypeSpec Wave B and contract DX

**Status: Done** (R20–R25 on `chore/remaining-005`, 2026-08-09).

## Outcome

| Phase | Item | State |
|-------|------|--------|
| R20 | Commerce product dual DTOs | Done |
| R21 | Record refund dual DTO | Done |
| R22 | Broadcast preview / status honesty | Done |
| R23 | Billing signed PDF honesty | Done |
| R24 | Payments docs security schemes | Done |
| R25 | Path-honesty CI | Done — `scripts/check-openapi-minimal-honesty.mjs` + `contracts` CI + `task contracts:honesty`; allowlist `packages/api-spec/honesty-allowlist.yaml` |

Notes: `plans/005-remaining/r20-notes.md` … `r25-notes.md`.

## Soft residuals (not FW-6 blockers)

- Admin-routes.tsp split if file pain returns
- Future dual DTOs on new surfaces — same pattern, local PR
- Allowlist hygiene when adding `impl_only` routes

---

# FW-7 — Remaining navigability polish

**Status: Done** for 005 wave scope (R50–R53 on `chore/remaining-005`, 2026-08-09).

## Outcome (this wave)

| Phase | Item | State |
|-------|------|--------|
| R50 | TestSupport batch | Done |
| R51 | `LhdnGatewayAdapter` partials | Done |
| R52 | LLM stream partial | Done |
| R53 | GatewayCommon + outbox DI pilot | Done |

Notes: `plans/005-remaining/r50-notes.md` … `r53-notes.md`.

## Follow-ups (opportunistic, not open program)

| File / area | Suggestion |
|-------------|------------|
| Outbox/inbox DI | Roll `AddModuleOutboxInbox` to remaining modules (R53 pilot was CRM) |
| `BillingQueryService` / `B2cConsolidationJob` | Partials when editing |
| ProblemDetails | Expand stable `code` map as endpoints are touched |
| ModuleTests → `Lazuar.TestSupport` | Further batches when touching tests |

Prefer house style when the file is already in the PR — no global deadline.

---

# Related parked product paths (not “architecture debt”)

These are **product freezes**, not incomplete refactors:

| Topic | Decision | Future work |
|-------|----------|-------------|
| **RevenueRecognitionJob** | Parked unregistered (00.3) | Finance/Xero epic owns schedule creation before any register |
| **WhatsApp / multi-channel** | Frozen (00.4) | Product funds provider → reopen 00.4 → possible Messaging merge (FW-5) |
| **Community / Vault** | Deleted | No rebuild; only historical schema drop ops if a DB still has schemas |

---

## Residual ops tickets (post-R99 — prefer these)

1. `ops(keys): migrate legacy LHDN API keys staging/prod + deploy One-only (FW-1 / R04→R05)`  
2. `ops(webhooks): registry migrate staging + verify LHDN → One outbox delivery (R41/R42.4)`  
3. `ops(keys): drop/archive lhdn.DeveloperApiKeys after ≥30d One-only prod (R06)`  

Historical / closed titles:

1. ~~`feat(one): migrate legacy LHDN API keys and remove dual-read (FW-1)`~~ code done; ops above  
2. ~~`feat(webhooks): deliver LHDN lifecycle events via One dispatcher (FW-2)`~~ **done R40–R43**  
3. ~~`refactor(ops): move LLM factory from BuildingBlocks to Ops (FW-3)`~~ **done R31**  
4. ~~`refactor(metrics): pluginize PlatformMetricsCollector contributors (FW-3/FW-4)`~~ **done R35**  
5. ~~`fix(comms): replace cross-schema receipt SQL with Contracts query (FW-4)`~~ **done R11+**  
6. ~~`chore(api-spec): Wave B contract honesty — products + security schemes (FW-6)`~~ **done R20–R25**  

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
4. After R99, prefer residual ops tickets above over reopening a mega program.

**Last updated:** 2026-08-09 (R60 SKIP + R99 wave close)  

