# 10 — Program sequencing and risks (remaining work F00–F16)

**Status:** Uncondensed program plan for post-004 remaining work  
**Date:** 2026-08-09  
**Scope:** How to run `plans/004-maintenance/checklists-future/` (F00–F16) as a **phased program**, not one mega-PR  
**Sources of truth:**

| Artifact | Path |
|----------|------|
| Future workstreams FW-1…FW-7 | [`../004-maintenance/FUTURE-WORK.md`](../004-maintenance/FUTURE-WORK.md) |
| Locked product/eng decisions | [`../004-maintenance/decisions.md`](../004-maintenance/decisions.md) |
| Phase checklists F00–F16 | [`../004-maintenance/checklists-future/README.md`](../004-maintenance/checklists-future/README.md) |
| API key cutover design | [`../004-maintenance/api-key-cutover-design.md`](../004-maintenance/api-key-cutover-design.md) |
| Track close-out (what deferred) | [`../004-maintenance/phase-18-done.md`](../004-maintenance/phase-18-done.md) |

**Non-goals of this document:** Application code changes; inventing new modules; reopening freezes without product; frontend work except forced `task gen` clients.

**Companion analyses in this folder (`plans/005-remaining/`):**

| # | Planned analysis file | Focus |
|---|----------------------|--------|
| 01 | `01-api-key-one-only-cutover.md` | FW-1 inventory, migrator, dual-read removal, table drop |
| 02 | `02-webhooks-lhdn-to-one.md` | FW-2 product locks, enqueue path, fire-and-forget retirement |
| 03 | `03-building-blocks-port-moves.md` | FW-3 ownership moves (ports, LLM, email, metrics) |
| 04 | `04-cross-schema-sql-leaks.md` | FW-4 runtime boundary SQL inventory + fix pattern |
| 05 | `05-typespec-wave-b.md` | FW-6 dual DTOs, honesty, optional path CI |
| 06 | `06-polish-god-files-testsupport.md` | FW-7 residual navigability + TestSupport |
| 07 | `07-module-extract-gate.md` | FW-5 product-gated extracts only |
| 08 | `08-parked-freezes-and-non-goals.md` | 00.3 revenue, 00.4 WhatsApp, Community/Vault, explicit rejects |
| 09 | `09-delivery-gates-and-pr-hygiene.md` | F00 align, PR shape, calendar, ops runbooks |
| **10** | **This file** | Wave plan, parallel vs serial, risk matrix, whole-program DoD |

---

## 0. Executive summary

Maintenance track **004 (phases 00–18)** is closed as **healthy enough**. What remains is **not** “one more cleanup mega-diff.” It is a **second program** with the same delivery style as 00–18: many small phases, one intent per PR, hard gates, honest freezes.

That program is already scaffolded as **F00–F16** under `checklists-future/`. This document synthesizes **how to run it**:

1. **Six implementable feature areas** (user bullets) map cleanly onto FW-1/2/3/4/6/7 and phases F01–F14.  
2. **One product-gated area** (module extract, FW-5 / F15) stays **SKIP** unless `decisions.md` is reopened.  
3. **Three tracks are calendar- or product-gated** (Keys, Webhooks, Extract); the rest are **opportunistic / hygiene**.  
4. **Most tracks can run in parallel** after F00, except where they share the same files or where serial data migration is required *inside* a track.  
5. **Whole-program “done”** is when selected tracks meet their F16 criteria **and** residuals are normal tickets — not an open “future mega-program.”

**Recommended posture for the first execution wave (default F00 answers):**

| Track | This wave? | Why |
|-------|------------|-----|
| Keys F01–F04 | **Yes if** calendar approaching / row counts known; else **plan only** until ~2026-11 | Hard calendar 2026-11-30 → 2026-12-15 |
| Webhooks F05–F06 | **F05 yes** (product decisions); **F06 only after F05 locks** | Product-blocked, not eng-blocked |
| SQL F07–F08 | **Yes** | Hygiene; unblocks clean metrics later |
| TypeSpec F09 | **Yes** | Independent DX; low coupling |
| BB F10–F13 | **Yes (thin slices)** | Opportunistic; one concern per PR |
| Polish F14 | **Opportunistic** | Only when touching those files |
| Extract F15 | **No / N/A** | No product trigger |
| Close-out F16 | End of wave | Meta |

---

## 1. Mapping: 6 user bullets → F-phases → analysis files 01–09

The “six remaining feature areas” are the **implementable** residual streams from `FUTURE-WORK.md` + `phase-18-done.md`, excluding pure freezes and the extract gate.

| # | User bullet (feature area) | FW | F-phases | Gate | Analysis file(s) | Primary 004 evidence |
|---|----------------------------|----|----------|------|------------------|----------------------|
| **1** | **API key dual-read cutover → One-only** | FW-1 | **F01 → F02 → F03 → (wait ≥30d) → F04** | Calendar + row migration; dual-read allowed until **2026-11-30**; One-only target **2026-12-15** | **01** (cutover deep dive); **09** (ops/calendar) | `api-key-cutover-design.md`, phase-03-*, decisions §00.1 |
| **2** | **LHDN customer webhooks → One dispatcher** | FW-2 | **F05 → F06** | **Product** signing/payload/routing locks before F06 | **02** (convergence); **09** (integrator notice) | phase-04-*, decisions §00.2, Lhdn/One READMEs |
| **3** | **BuildingBlocks product-port code moves** | FW-3 | **F10 → F11 → F12 → F13** | None hard; respect 00.4 freeze on multi-channel product | **03** (BB moves); F13 overlaps **04** (metrics SQL) | `009-building-blocks-ownership.md`, phase-15-*, plan 06 |
| **4** | **Cross-schema SQL / runtime boundary leaks** | FW-4 | **F07 → F08** (F13 for metrics contributors) | F07 inventory before F08 fixes | **04** (leaks); metrics slice also **03** | plan 04 modularization, phase-18.4 open |
| **5** | **TypeSpec Wave B + residual contract DX** | FW-6 | **F09** | None; can parallel anytime | **05** | phase-05-*, plan 05 TypeSpec |
| **6** | **Residual god-file / TestSupport polish** | FW-7 | **F14** | Opportunistic; no global deadline | **06** | phase-11/13 residuals, plan 02 large files |

### Supporting (not “feature bullets,” but required program files)

| Topic | FW / decision | F-phases | Analysis | Role in program |
|-------|---------------|----------|----------|-----------------|
| Module extract / merge | FW-5 | **F15** (default **SKIP**) | **07** | Product trigger only; never parallel “for tidiness” |
| Parked freezes & non-goals | 00.3, 00.4, 00.6; Community/Vault | N/A (do not implement) | **08** | Guardrails: what must not be smuggled into phases |
| Delivery style, F00 align, PR hygiene | Meta | **F00**, continuous | **09** | How waves start; branch strategy; runbooks |
| Whole-program sequencing & risks | Meta | **F16** close-out | **10** (this file) | Waves, parallel matrix, risk, DoD |

### Full F-phase → bullet / analysis crosswalk

| Phase | Intent | User bullet | Analysis |
|-------|--------|-------------|----------|
| **F00** | Which tracks this wave | Meta (all) | **09**, **10** |
| **F01** | API key inventory | 1 | **01** |
| **F02** | Migrate LHDN keys → One | 1 | **01**, **09** |
| **F03** | One-only middleware | 1 | **01** |
| **F04** | Drop/archive `DeveloperApiKeys` | 1 | **01** |
| **F05** | Webhook product decisions | 2 | **02**, **09** |
| **F06** | One dispatcher for LHDN events | 2 | **02** |
| **F07** | Cross-schema inventory | 4 | **04** |
| **F08** | Fix leaks (one PR per family) | 4 | **04** |
| **F09** | TypeSpec Wave B | 5 | **05** |
| **F10** | BB port hygiene | 3 | **03** |
| **F11** | LLM stack → Ops | 3 | **03** |
| **F12** | Email/messaging ownership | 3 | **03**, **08** (00.4) |
| **F13** | Metrics contributors | 3 + 4 | **03**, **04** |
| **F14** | Polish / TestSupport | 6 | **06** |
| **F15** | Module extract gate | (not a bullet) | **07** |
| **F16** | Definition of done | Meta | **10** |

### Mapping to suggested ticket titles (from FUTURE-WORK)

| Ticket title | Bullet | Phases |
|--------------|--------|--------|
| `feat(one): migrate legacy LHDN API keys and remove dual-read (FW-1)` | 1 | F01–F03 (F04 separate) |
| `feat(webhooks): deliver LHDN lifecycle events via One dispatcher (FW-2)` | 2 | F05–F06 |
| `refactor(ops): move LLM factory from BuildingBlocks to Ops (FW-3)` | 3 | F11 (after F10) |
| `refactor(metrics): pluginize PlatformMetricsCollector contributors (FW-3/FW-4)` | 3+4 | F13 (+ F07/F08 inventory) |
| `fix(comms): replace cross-schema receipt SQL with Contracts query (FW-4)` | 4 | F08 family |
| `chore(api-spec): Wave B contract honesty — products + security schemes (FW-6)` | 5 | F09 |

Polish (bullet 6) does not get a single mega-ticket; it is a **queue of small PRs** under F14.

---

## 2. Parallel tracks vs serial (dependency graph)

### 2.1 Track graph (after F00)

```text
F00 Program align
  │
  ├─► TRACK KEYS (serial inside) ───────────────────────────────┐
  │     F01 inventory                                           │
  │       → F02 migrate (staging then prod)                     │
  │         → F03 One-only code cutover                         │
  │           → [calendar: ≥30d One-only in prod]               │
  │             → F04 table drop/archive                        │
  │                                                             │
  ├─► TRACK WEBHOOKS (serial inside; product-gated)             │
  │     F05 product decisions (no code)                         │
  │       → F06 One dispatcher enqueue + kill fire-and-forget   │
  │                                                             │
  ├─► TRACK SQL (serial inside; parallel with Keys/TypeSpec)    │
  │     F07 inventory                                           │
  │       → F08 fix P0/P1 one family per PR                     │
  │                                                             │
  ├─► TRACK TYPESPEC (independent)                              │
  │     F09 Wave B (may split into multiple PRs)                │
  │                                                             │
  ├─► TRACK BB (mostly serial recommended; parallel OK vs Keys) │
  │     F10 port hygiene                                        │
  │       → F11 LLM → Ops                                       │
  │         → F12 email/messaging (respect 00.4)                │
  │           → F13 metrics plugins  ← ideally after F07        │
  │                                                             │
  ├─► TRACK POLISH (anytime, low risk)                          │
  │     F14 opportunistic PRs                                   │
  │                                                             │
  ├─► TRACK EXTRACT (default closed)                            │
  │     F15 only if product reopens decisions                   │
  │                                                             │
  └─► F16 close-out (when selected tracks complete)
```

### 2.2 What may run in parallel

| Parallel set | Tracks / phases | Why safe |
|--------------|-----------------|----------|
| **A — Broad parallel band** | Keys F01–F02, SQL F07, TypeSpec F09, BB F10, Polish F14, Webhooks **F05 only** | Different modules/files; F05 is docs/product only |
| **B — After inventories** | Keys F02–F03, SQL F08 (non-metrics), TypeSpec F09 remaining, BB F11–F12, Polish F14 | Still low file overlap if PR scopes are disciplined |
| **C — Metrics convergence** | F08 metrics-related leak + F13 | Same problem family — **coordinate as one design**, implement as 1–2 PRs not three concurrent rewrites of `PlatformMetricsCollector` |
| **D — Webhooks impl** | F06 alone or with TypeSpec/Polish | After F05; avoid same-time big Lhdn event-handler churn with F11 LLM if same author capacity is the bottleneck (not a hard tech gate) |

**Rule of thumb:** Tracks are **logically independent**. Capacity and **file-level conflict** (not architecture) are the usual reasons to serialize further.

### 2.3 What is strictly serial *inside* a track

| Track | Serial chain | Reason |
|-------|--------------|--------|
| Keys | F01 → F02 → F03 → wait → F04 | Inventory before migrate; migrate before remove dual-read; stability before drop table |
| Webhooks | F05 → F06 | Product locks before enqueue/signing code |
| SQL | F07 → F08 | Ticket list before fix PRs |
| BB | F10 recommended before F11–F13 | Port hygiene reduces churn when moving implementations |
| Extract | Gate checks → design → implement | Product + ADR before any `.csproj` surgery |

### 2.4 Soft edges (not hard serial, but preferred order)

| Preferred order | Why |
|-----------------|-----|
| F07 inventory before F13 metrics plugins | F13 is a specialized F08 fix; inventory avoids double-work |
| F10 before F11 | Moving LLM after port cleanup is less noisy |
| F11 before heavy F14 on `LlmOrchestratorService` | Avoid partial-split then re-home ownership |
| F06 after (or coordinated with) F09 only if signing/docs surfaces change OpenAPI | Product may require TSP honesty for webhook contract claims |
| F03 not before F02 residual legacy-only = 0 | 401 risk for integrators |

### 2.5 Capacity model (how many parallel streams)

| Team size | Suggested concurrent streams | Rationale |
|-----------|------------------------------|-----------|
| 1 engineer | 1 hard track + 1 soft track | e.g. Keys F01–F02 **or** SQL F07–F08 **plus** TypeSpec/Polish slices |
| 2 engineers | 2 hard tracks + shared polish | Eng A: Keys/Webhooks; Eng B: SQL + BB; either: F09 |
| 3+ | Up to 3 hard tracks | Still **one PR intent**; no “merge Friday mega-branch” |

Hard tracks = Keys, Webhooks F06, SQL F08, BB F11–F13. Soft = F09, F14, F05, F07, F10.

---

## 3. Recommended wave plan (implementing all 6 feature areas)

Waves are **time-boxed program slices**, not single PRs. Each wave starts with an F00 re-confirm (or a short “wave brief”) so calendars and product blockers stay honest.

### Wave 0 — Align (F00) — ~0.5–1 day

**Goal:** Lock which of the 6 bullets run this quarter; pick branch strategy; write F00 answers.

**Checklist (from phase-f00):**

- [ ] Keys: yes / later (calendar-aware)  
- [ ] Webhooks: F05 yes; F06 only if product will answer this quarter  
- [ ] SQL: yes  
- [ ] TypeSpec: yes  
- [ ] BB: yes (at least F10; schedule F11–F13)  
- [ ] Polish: opportunistic  
- [ ] Extract: **N/A** unless reopen  
- [ ] Delivery: many PRs on long-lived branch **or** stacked PRs — **pick one**  
- [ ] Confirm dual-read calendar, webhook A needs product, F15 closed  

**Exit:** Active track list ordered; owner named per track; link to this file + FUTURE-WORK.

**Analysis to complete first if missing:** **09** (delivery), skim **08** (non-goals), start **01**/**04** inventories as pure analysis if eng starts next day.

---

### Wave 1 — Inventories & product unblockers (parallel)

**Duration:** ~3–10 engineering days depending on prod access and product responsiveness  
**Implement bullets:** prep for 1, 2, 4; partial 5  
**Phases:**

| Stream | Phases | Output |
|--------|--------|--------|
| Keys | **F01** | Staging/prod row counts; accelerate vs calendar decision |
| Webhooks | **F05** | Written signing/payload/routing doc; F06 unblocked **or** deferred date |
| SQL | **F07** | `cross-schema-leaks.md` ticket table with P0/P1/P2 |
| TypeSpec | **F09.1 start** | Dual-DTO inventory list (may land inventory-only PR) |
| BB | **F10** | Ports hygiene PR(s); 009 map touch-ups |
| Polish | **F14** optional | Only if idle capacity and a single file family is already open |

**Parallelism:** Full band A (all of the above simultaneously if capacity allows).

**What not to do in Wave 1:**

- F03 (One-only) without F02 migration complete  
- F06 without F05 written locks  
- F08 mega-PR “fix all leaks”  
- F15 extracts  
- F12 WhatsApp / multi-channel product work  

**Exit criteria:**

- [ ] F01 counts recorded; F02 go/no-go written  
- [ ] F05 decision doc committed **or** explicit “product deferred to DATE”  
- [ ] F07 inventory committed  
- [ ] F10 landed or explicitly deferred with reason  
- [ ] At least Wave B inventory listed for F09  

---

### Wave 2 — Migrations, fixes, and first ownership moves

**Duration:** ~1–3 weeks (Keys migration may wait on staging windows)  
**Implement bullets:** 1 (migrate), 3 (LLM), 4 (P0 leaks), 5 (first Wave B PRs), 6 (opportunistic)

| Stream | Phases | Notes |
|--------|--------|-------|
| Keys | **F02** | Staging dry-run → prod migrator; residual legacy-only → 0 |
| SQL | **F08 P0** | One PR per leak family; Contracts ports |
| BB | **F11** | LLM factory/policies → Ops |
| TypeSpec | **F09** slices | Products dual DTOs; security schemes; honesty items |
| Polish | **F14** | Only when files already in diff |
| Webhooks | idle or design-only | Unless F05 already locked → start F06 design |

**Parallelism:** Keys F02, SQL F08, BB F11, TypeSpec F09 can all proceed together. Prefer **not** to open F13 until F07 is done (already is).

**Exit criteria:**

- [ ] F02: migration report; active legacy-only keys = 0 (or signed risk)  
- [ ] F08: all P0 fixed; P1 scheduled  
- [ ] F11: Ops owns Ops-only LLM surface; tests green  
- [ ] F09: at least one Wave B honesty PR merged  

---

### Wave 3 — Hard cutovers (Keys One-only + Webhooks A + metrics)

**Duration:** calendar-bound for Keys; product-bound for Webhooks  
**Implement bullets:** 1 (cutover), 2 (if F05 locked), 3–4 (metrics), 5–6 remainder

| Stream | Phases | Notes |
|--------|--------|-------|
| Keys | **F03** | Only after F02 residual 0; prefer after **2026-11-30** unless accelerated |
| Webhooks | **F06** | Only after F05; dual-verify window if signing changes |
| BB/SQL | **F13** (+ remaining F08 P1) | Pluginize metrics; kill multi-schema SQL in BB collector |
| BB | **F12** | Email/messaging ownership; **no** Meta WhatsApp |
| TypeSpec | remaining F09 | PDF/broadcast/compliance honesty; optional path CI |
| Polish | F14 | LhdnGatewayAdapter / TestSupport batches if capacity |

**Keys calendar reminder (decisions §00.1):**

| Milestone | Date |
|-----------|------|
| Dual-read allowed until | **2026-11-30** |
| One-only target | **2026-12-15** |
| Table drop | ≥ **30 days after** One-only in prod → Wave 4 |

**Exit criteria:**

- [ ] F03: One-only in staging + prod; no dual-read path; 401 monitoring clean  
- [ ] F06: complete **or** deferred with new product date in FUTURE-WORK  
- [ ] F13: metrics collector thin or residual listed  
- [ ] F12: brand/product HTML not in BB (or residual ticketed)  
- [ ] F09 Wave B targets for this program done  

---

### Wave 4 — Stabilization & Keys table archive

**Duration:** starts ≥30 days after F03 prod (or waiver)  
**Implement bullets:** 1 (F04), residual 3/4/6

| Stream | Phases | Notes |
|--------|--------|-------|
| Keys | **F04** | Drop or archive `lhdn.DeveloperApiKeys`; remove dead domain |
| Any residual | F08 P2, F14, F12 leftovers | Ticket-shaped only |
| Extract | F15 | Still N/A unless product reopened mid-program |

**Exit criteria:**

- [ ] F04 done or scheduled with date + owner  
- [ ] No open P0 from F07 inventory without ticket  
- [ ] FUTURE-WORK FW-1 marked done  

---

### Wave 5 — Program close-out (F16)

**Goal:** Convert “remaining program” into **normal backlog**.

- [ ] F16.1–F16.6 checkboxes for **selected** tracks  
- [ ] FUTURE-WORK section statuses updated with dates + PR links  
- [ ] One/Lhdn READMEs: no dual-path lies  
- [ ] Residuals are ordinary tickets (not F17+)  
- [ ] F15 recorded N/A or complete  

**Then stop.** Do not invent F17 “just one more cleanup.”

---

### Wave plan summary (Gantt-style)

```text
Time →

Wave 0  F00
Wave 1  F01 | F05 | F07 | F09inv | F10 | (F14)
Wave 2  F02 | F08P0 | F11 | F09… | (F14)
Wave 3  F03 | F06* | F12 | F13 | F09done | (F14)
        [*F06 only if F05 locked]
Wave 4  F04 (after 30d) | residuals
Wave 5  F16 close-out

Calendar anchors:
  …… dual-read OK ……| 2026-11-30 |…… F03 target by 2026-12-15 ……| +30d → F04
```

### Sequencing if the calendar is **far** from 2026-11-30

Still run Waves 0–2 without F03. Prefer:

1. F07 → F08 (hygiene now)  
2. F09 (DX now)  
3. F10 → F11 → F12 → F13 (BB)  
4. F05 early (product decisions cool while waiting)  
5. F01 anytime (inventory is cheap and may accelerate cutover)  
6. Park F02/F03 close to the dual-read end date so dual-read window is not “closed early” then reopened by incomplete migration — **unless** prod legacy count is already 0  

If F01 shows **prod active legacy = 0**, accelerate: F02 no-op → F03 immediately (decisions allow forward move).

### Sequencing if product **cannot** answer webhooks this quarter

- Complete F05 as “deferred to DATE” with freeze reaffirmed (00.2 C still holds).  
- **Do not** start F06.  
- **Do not** invent Lhdn durable stack B.  
- Program can still reach F16 for other tracks with webhooks marked deferred.

---

## 4. What NOT to parallel

These are **anti-patterns**, not optional optimizations.

### 4.1 Never parallel (hard)

| Forbidden combination | Why |
|-----------------------|-----|
| **F03 with incomplete F02** | Legacy-only keys → production 401s |
| **F04 with F03 < 30d in prod** (no waiver) | Premature table drop destroys rollback/audit |
| **F06 without F05 written locks** | Signing/payload thrash; integrator break without notice |
| **F15 with “tidiness” motive** | Violates 00.5/00.6; extract needs product trigger + design |
| **Second Lhdn outbox (decision B) alongside F06-A** | Explicit reject; do not fork platform webhooks |
| **F12 multi-channel / WhatsApp product** under BB move PR | 00.4 freeze; reopen formally |
| **Mega-PR combining Keys + Webhooks + BB + TypeSpec** | Unreviewable; mixed rollback domains |
| **Hand-edit giant generated OpenAPI / EF snapshots as cleanup** | FUTURE-WORK non-goal; regen via pipeline |

### 4.2 Do not parallel on the same file family (soft but strong)

| Conflict zone | Phases that touch it | Rule |
|---------------|----------------------|------|
| `ApiKeyAuthenticationMiddleware` / revoke composition | F01 (read), F02, F03 | Serialize Keys stream only |
| `WebhookSenderService` / Lhdn lifecycle handlers / One outbox | F05 (docs), F06 | One owner; no parallel “improve F&F” |
| `PlatformMetricsCollector` | F07, F08 metrics, F13 | Design once; one rewrite PR |
| BB Application email/LLM types | F10, F11, F12 | Prefer F10 → F11 → F12 order |
| Commerce product endpoints + TypeSpec product models | F09, maybe F14 | Coordinate; regenerate clients once per PR |
| Lhdn gateway adapter | F14, possibly F06 correlation only | Prefer F14 partials **not** same PR as F06 behavior change |

### 4.3 Do not parallel *process* anti-patterns

| Anti-pattern | Prefer |
|--------------|--------|
| Long-lived branch with 6 tracks unmerged for weeks | Merge each phase PR to main/trunk often |
| Dual “future programs” (005 vs ad-hoc tickets fighting) | FUTURE-WORK is index; tickets link to F-phases |
| Reopening dual-read “temporarily” after F03 | Hotfix One data; do not reintroduce Lhdn branch |
| “While I’m here” extract of Credits/Webhooks module | F15 gate only |
| Squashing F01–F04 into one commit | Separate inventory, migrate, cutover, drop |

### 4.4 Explicit non-goals still out of this program

From FUTURE-WORK + decisions (see analysis **08**):

- New modules for Dunning, Catalog, Identity, Tax, Analytics, Marketplace  
- Microservice split of the modular monolith  
- Meta WhatsApp / multi-country tax / Xero as “cleanup”  
- Community / Vault resurrection  
- Frontend feature work beyond forced client regen  
- `RevenueRecognitionJob` registration without finance epic (00.3 park)  

---

## 5. Risk matrix

Severity × likelihood after mitigations. **S** = severity if it hits; **L** = likelihood without discipline; **R** residual after program mitigations.

| ID | Risk | Track | S | L | Mitigation | Residual |
|----|------|-------|---|---|------------|----------|
| **R1** | Integrator 401s after dual-read removal | Keys F03 | Critical | Med | F01 counts; F02 migrate to residual 0; staging One-only smoke; 401 dashboards | Low if residual 0 |
| **R2** | Scope allowlist rejects legacy scopes | Keys F02 | High | Med | Quarantine list + manual remap per cutover design §4.3 | Low |
| **R3** | List/revoke UI misses unmigrated keys | Keys F02 | High | Med | Migrate before F03; verify list UI on staging | Low |
| **R4** | Cache stale after revoke during dual window | Keys | Med | Low | Keep One revoke event; do not remove until F03 | Low |
| **R5** | Hash re-hash mistake breaks all keys | Keys F02 | Critical | Low | **Copy KeyHash as-is**; design already forbids re-hash | Very low if reviewed |
| **R6** | Early table drop removes forensic rollback | Keys F04 | High | Low | ≥30d gate; archive option over hard drop | Low |
| **R7** | Breaking webhook signing without notice | Webhooks F06 | Critical | Med | F05 locks; dual-verify window; changelog + hub docs | Low–Med |
| **R8** | Payload shape change breaks customers | Webhooks F06 | High | Med | F05 envelope decision; versioned mapping tests | Low–Med |
| **R9** | Silent half-migration (F&F still used for some events) | Webhooks F06 | High | Med | Event type checklist; metrics re-tag; delete/gut paths | Low |
| **R10** | Building second durable Lhdn stack | Webhooks | High | Low | Reject B; code review gate | Very low if enforced |
| **R11** | Cross-schema “fix” by moving SQL into BB | SQL F08 | Med | Med | Rule: ports on owning Contracts; arch tests | Low |
| **R12** | Incomplete inventory → leaks return | SQL F07 | Med | Med | Fresh grep every wave; architecture review checklist | Med |
| **R13** | Tenant isolation regression via new query ports | SQL F08 | Critical | Low | Module tests + tenant filter assertions | Low |
| **R14** | BB move breaks DI / circular refs | BB F10–F12 | High | Med | One concern per PR; arch tests BB ↛ Modules | Low–Med |
| **R15** | LLM move breaks Ops streaming | BB F11 | High | Med | Ops.Tests green; staged DI registration | Low |
| **R16** | Email templates break billing/comms | BB F12 | High | Med | Host resolve smoke; module tests; no WA expansion | Low |
| **R17** | Metrics endpoint shape change breaks ops | BB F13 | Med | Med | Contract test for metrics payload; thin aggregator | Low |
| **R18** | TypeSpec regen churn / client mismatch | TypeSpec F09 | Med | High | `task gen` + commit clients per policy; small PRs | Med |
| **R19** | Dual DTO “fixed” then reintroduced | TypeSpec F09 | Med | Med | Optional path-honesty CI (F09.3) | Low if CI lands |
| **R20** | Polish PR changes behavior without tests | Polish F14 | Med | Med | No behavior change without tests; one file family | Low |
| **R21** | F15 extract without dual-write design | Extract | Critical | Low | Gate all-or-nothing; default SKIP | Very low |
| **R22** | Calendar slip past 2026-12-15 with dual-read | Keys | High | Med | F01 early; F00 wave owned; escalate if legacy > 0 late | Med |
| **R23** | Program never closes (infinite F-phases) | Meta F16 | Med | High | F16 stop criteria; residuals → normal tickets | Low if F16 enforced |
| **R24** | Parallel PRs thrash same files; merge hell | Meta | Med | High | §4.2 conflict zones; one owner per zone | Med |
| **R25** | Prod migration without backup/runbook | Keys F02 | Critical | Low | F02.3 backup note + report | Low |

### Risk heat by wave

| Wave | Highest residual risks | Watch |
|------|------------------------|-------|
| 0–1 | R22 (calendar), R12 (inventory quality), product silence on F05 | F01 early; ping product for F05 |
| 2 | R2–R3 (migration), R14–R15 (BB), R18 (TSP churn) | Staging auth smoke; small PRs |
| 3 | R1 (401), R7–R9 (webhooks), R17 (metrics) | Feature flags only if already house style; prefer clean cut with rollback plan |
| 4–5 | R6 (drop), R23 (never close) | Enforce 30d; run F16 |

### Rollback postures (by track)

| Track | Rollback story |
|-------|----------------|
| Keys F02 | Idempotent migrator; dual-read still on — rollback = stop migrator; One rows can remain |
| Keys F03 | Revert middleware PR only if dual-read code restored **and** legacy rows still present; after F04, rollback is restore-from-archive |
| Keys F04 | Prefer rename-to-archive over drop first release |
| Webhooks F06 | Feature switch hard; prefer dual-path **delivery** only if F05 dual-verify planned — not a second stack |
| SQL F08 | Revert single leak PR; ports can stay unused |
| BB moves | Revert PR; avoid multi-move mega-PR so revert is clean |
| TypeSpec | Revert TSP + regen; clients move with PR |
| Polish | Revert single file-family PR |

---

## 6. Definition of done — whole remaining program

This is broader than any single FW “done when.” It is the **program** bar, aligned with **F16** and FUTURE-WORK maintenance rules.

### 6.1 Structural DoD (must)

1. **Phased delivery used:** Work landed as F-phase PRs (or tightly related sub-items), not one mega-merge of F01–F16.  
2. **F00 recorded** for each execution wave (or a single wave brief covering the full program).  
3. **Selected tracks complete or explicitly deferred** with date + owner in `FUTURE-WORK.md`.  
4. **F15 is N/A or complete** — never “half extract.”  
5. **No dual-path lies** in One/Lhdn READMEs (keys dual-read window closed **or** still dated honestly if Keys deferred).  
6. **Residuals are normal tickets** — no open special program name required to ship product work.  
7. **Architecture tests green** on main for every merged phase.  
8. **decisions.md** only changed when a lock actually changes (early cutover, 00.2 reopen, etc.).

### 6.2 Per-bullet DoD (the 6 feature areas)

| Bullet | Done when |
|--------|-----------|
| **1 Keys** | F03 One-only in prod (or accelerated path with residual 0); dual revoke gone; F04 done **or** scheduled ≥30d with owner; design doc status = executed; FW-1 Done |
| **2 Webhooks** | F06: customer LHDN lifecycle webhooks only via One dispatcher; fire-and-forget not relied on; tests + integrator docs; freeze section removed; **or** product deferred with new date and C freeze reaffirmed; FW-2 Done or deferred |
| **3 BB moves** | Map §3 items moved **or** explicitly re-accepted as grey with rationale in 009; F10–F13 done/deferred with tickets; FW-3 status updated |
| **4 Cross-schema** | F07 inventory committed; F08 all P0 fixed; P1 fixed or ticketed; no production path uses private foreign-schema SQL without approved exception; FW-4 status updated |
| **5 TypeSpec Wave B** | Targeted dual DTO pairs gone on shipping surfaces; docs security accurate; optional CI honesty gate green **or** deferred with ticket; FW-6 status updated |
| **6 Polish** | Wave either landed ≥1 polish PR **or** explicitly skipped; remaining candidates are normal “when touching” notes — no global deadline; FW-7 opportunistic |

### 6.3 F16 checklist (program close)

Reproduce and check at close-out (from `phase-f16-definition-of-done.md`):

- [ ] **Keys (if selected):** F03 in prod or waived; F04 done or dated  
- [ ] **Webhooks (if selected):** F06 complete or product deferred with date  
- [ ] **SQL / BB / TypeSpec (if selected):** F08 P0; F09 targets; F10–F13 done or ticketed  
- [ ] **Extract:** F15 N/A or complete  
- [ ] **Docs:** FUTURE-WORK statuses; README honesty  
- [ ] **Stop:** residuals are normal tickets, not mega-program  

### 6.4 Program success metrics (optional but useful)

| Metric | Target |
|--------|--------|
| Open dual-read in prod after 2026-12-15 | **No** (unless waiver with security owner) |
| LHDN customer webhook delivery path | One dispatcher **or** documented deferral |
| P0 cross-schema leaks from F07 | **0** open |
| BB ownership map §3 “must move” | **0** without rationale |
| Known dual DTOs on shipping admin/public surfaces (Wave B list) | **0** |
| Open F-phases without owner | **0** |
| Mega-PRs (>~1 phase intent) merged | **0** preferred |

### 6.5 Explicit “not required for program done”

- Completing every F14 god-file partial  
- Landing optional path-honesty CI (recommended, not mandatory)  
- F15 extract  
- WhatsApp provider  
- Revenue recognition job registration  
- Migration squash  
- 100% ModuleTests on TestSupport  

---

## 7. Delivery mechanics (program operating system)

### 7.1 Branch / PR model (pick one in F00)

| Model | When | Rules |
|-------|------|-------|
| **Many PRs → main** | Default if main is healthy | One phase intent per PR; merge often |
| **Stacked PRs / long-lived branch** | If cutover needs coordinated release train | Still one phase per PR; rebase often; no silent scope creep |

### 7.2 Every phase PR (hygiene from checklists-future README)

- [ ] One phase intent (or tightly related sub-items only)  
- [ ] Tests / `task gen` as applicable  
- [ ] No outbox type renames without migration note  
- [ ] Update FUTURE-WORK section status when a **stream** completes  
- [ ] Prefer `phase-fXX-done.md` notes when useful  

### 7.3 Owner model

| Role | Responsibility |
|------|----------------|
| **Program shepherd** | F00, F16, calendar R22, FUTURE-WORK index truth |
| **Keys owner** | F01–F04, cutover design execution |
| **Webhooks owner** | F05 product facilitation + F06 |
| **Boundaries owner** | F07–F08 + F13 coordination |
| **Contracts owner** | F09 |
| **BB owner** | F10–F12 (and F13 with boundaries) |
| **Polish** | Anyone; shepherd prevents mega-polish |

One person may hold multiple roles; **Keys and Webhooks should not both be at cutover week without explicit capacity**.

### 7.4 Analysis work order (005-remaining docs)

If writing analyses before coding:

1. **09** delivery gates (short)  
2. **08** non-goals (short)  
3. **01**, **04**, **02** (inventories first)  
4. **03**, **05**, **06**  
5. **07** only if product signal  
6. Keep **10** (this file) updated when wave answers change  

---

## 8. Worked example: default “full six bullets” program

Assume: product will answer webhooks this quarter; calendar is mid-2026; two engineers; extract closed.

| Week | Eng A | Eng B | Product |
|------|-------|-------|---------|
| 0 | F00 + F01 | F07 | Kickoff F05 questions |
| 1 | F02 staging | F08 P0 #1 + F10 | F05 answers locked |
| 2 | F02 prod | F11 + F09 products | Integrator notice draft if signing changes |
| 3 | F08 P0 remain / F13 design | F09 security + F12 | — |
| 4 | F03 prep + staging One-only | F06 enqueue | Dual-verify comms if needed |
| 5 | F03 prod (if calendar allows) | F06 disable F&F + docs | Changelog |
| 6 | F14 / residuals | F09 CI optional | — |
| +30d after F03 | F04 | — | — |
| End | F16 both | F16 both | Accept residual tickets |

If F03 must wait until Dec 2026, weeks 4–5 become F06/F12/F13/F09 only; schedule F03 as a mini-wave near the calendar.

---

## 9. Decision traceability (locks that constrain sequencing)

| Decision | Constraint on program |
|----------|----------------------|
| **00.1** dual-read until 2026-11-30; One-only by 2026-12-15 | Keys serial; F03 calendar; accelerate only if residual 0 |
| **00.2** end-state A; interim C freeze; reject B | F05 before F06; no second stack PRs ever in parallel |
| **00.3** park RevenueRecognitionJob | Not an F-phase; do not “fix” under polish |
| **00.4** no WhatsApp / multi-channel 6 months; no Messaging merge | F12 is ownership move only; F15C closed |
| **00.5** credits stay in Billing ≥ ~2027-02 | F15A closed |
| **00.6** no new modules; FE out of scope | F15 default N/A; F09 clients only as forced |

---

## 10. Quick reference cards

### 10.1 Parallel green-light matrix

|  | Keys | Webhooks F05 | Webhooks F06 | SQL | TypeSpec | BB F10–12 | BB F13 | Polish | Extract |
|--|------|--------------|--------------|-----|----------|-----------|--------|--------|---------|
| **Keys** | serial self | ✅ | ✅* | ✅ | ✅ | ✅ | ✅* | ✅ | ❌ need |
| **Webhooks F05** | ✅ | — | serial | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ |
| **Webhooks F06** | ✅* | after F05 | — | ✅ | ✅ | ✅* | ✅* | ✅* | ❌ |
| **SQL** | ✅ | ✅ | ✅ | serial self | ✅ | ✅ | **coord** | ✅ | ❌ |
| **TypeSpec** | ✅ | ✅ | ✅ | ✅ | — | ✅ | ✅ | **coord** products | ❌ |
| **BB F10–12** | ✅ | ✅ | ✅* | ✅ | ✅ | serial preferred | after F10 | ✅* LLM | ❌ |
| **BB F13** | ✅* | ✅ | ✅* | **coord** | ✅ | after F10 | — | ✅ | ❌ |
| **Polish** | ✅ | ✅ | ✅* | ✅ | **coord** | ✅* | ✅ | — | ❌ |
| **Extract** | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | gate only |

\* = fine architecturally; watch file conflicts / same-author load.

### 10.2 One-page phase order (recommended)

```text
F00
→ parallel: F01, F05, F07, F09, F10, (F14)
→ parallel: F02, F08, F11, F09…, (F14)
→ parallel: F03†, F06‡, F12, F13, F09 done, (F14)
→ later: F04 (§30d after F03)
→ never unless product: F15
→ F16

† after F02 residual 0 + calendar  
‡ after F05 only
```

---

## 11. Document maintenance

When waves complete:

1. Update track statuses in `FUTURE-WORK.md` (Done + date + PR).  
2. Tick F-phase checklists; add `phase-fXX-done.md` if useful.  
3. If a lock changes, edit `decisions.md` and note here in §9.  
4. If wave plan dates shift, update §3 wave table — do not fork a third program index.  
5. When F16 passes, mark this program **closed** and leave residuals as ordinary backlog.

**Last updated:** 2026-08-09  
**Authoring intent:** Uncondensed sequencing analysis for `plans/005-remaining/`; no application code changes.

---

*End of 10 — program sequencing and risks. Implement via `plans/004-maintenance/checklists-future/` F00–F16; analyze details in `01`–`09` of this folder as they are written.*
