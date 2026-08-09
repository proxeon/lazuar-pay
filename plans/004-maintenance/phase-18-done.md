# Phase 18 — Done (maintenance track healthy enough)

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Commit subject:** `docs(plans): phase 18 maintenance track definition of done`  
**Outcome:** Track closed as **healthy enough** — residual dual-path and BB moves are dated/documented backlog, not an open special program.

---

## Assessment method

1. Read `checklists/phase-18-definition-of-done.md` exit criteria.  
2. Cross-checked every `phase-*-done.md` (00 under checklists; 01–17 under this folder) + locked `decisions.md`.  
3. Filled 18.1–18.7 honestly: **[x]** met this track (including dated dual-read / freeze as acceptable interim), **[ ]** only for cross-schema SQL not yet filed as fixed or external issues.

Checklist: [`checklists/phase-18-definition-of-done.md`](./checklists/phase-18-definition-of-done.md).

---

## Track status (phases 00–17)

| Phase | Horizon | Result this branch |
|-------|---------|-------------------|
| **00** | Align | Locked `decisions.md` (keys, webhooks, revenue, WA, credits, scope) |
| **01** | H1 Safety | Secrets + dead gen twins + residue removed |
| **02** | H1 Safety | Community/Vault docs honesty |
| **03** | H1 Honesty | **Interim only** — design + dated dual-read; cutover after **2026-11-30** |
| **04** | H1 Honesty | LHDN webhook **C freeze** + metrics; **A** convergence deferred |
| **05** | H1 Honesty | TypeSpec P0 dual DTOs, path slash, broadcast honesty; clients regen |
| **06** | H1 Honesty | CI ↔ Taskfile; Ops tests in CI; pnpm 11.5.2 |
| **07** | H2 Nav | One `Endpoints/` Commerce-style split |
| **08** | H2 Nav | `Program.cs` thinned → `Composition/*` |
| **09** | H2 Nav | `ProvisionAuraWorkspace` handler partials |
| **10** | H2 Nav | `DunningEngineJob` partials |
| **11** | H2 Nav | Public commerce, payment-completed, gateway webhook splits |
| **12** | H2 Nav | Messaging Workers/EventHandlers; Billing/Lhdn/Ops endpoint folders |
| **13** | H2 Nav | `Lazuar.TestSupport` pilot; Ops paging honesty; ProblemDetails codes |
| **14** | H2 Nav | Commerce/One models split; orphans cleaned; gen clean |
| **15** | H3 Fit | BB ownership map + SharedKernel marker; **no** product port code moves |
| **16** | H3 Fit | Extract/merge **not triggered** (gate not met) |
| **17** | H1–H2 | Revenue job parked; `_scope-probe` deleted; host Application refs fixed |
| **18** | Meta | This close-out |

---

## What shipped (high-signal)

### Safety & honesty

- Cookie jar / dead NSwag twin / obsolete test fixtures gone (01).  
- Backend + api-spec no longer teach Community/Vault as live modules (02).  
- API keys: One is SSoT for **mint**; dual-read **documented with calendar** until **2026-11-30** (03 interim).  
- Webhooks: One durable = platform; LHDN fire-and-forget **frozen** with failure metrics (04).  
- Contracts: P0 dual DTOs removed; payments path and broadcast model honest; README tree accurate (05, 14).  
- CI runs the same five test projects as Taskfile including Ops (06).  
- Product liminals decided: park revenue recognition, freeze WhatsApp, delete scope probe (17).

### Navigability

- One, Billing, Lhdn, Ops, Commerce public endpoints → Commerce-style composers.  
- Host composition root readable (~166 LOC Program).  
- Provision + dunning + payment-completed + gateway webhook god files → partials.  
- Messaging folder layout matches Workers/EventHandlers convention.

### Structural consciousness

- No new modules; Phase 16 extract deferred without product trigger.  
- BuildingBlocks thinning **plan** exists (`009-building-blocks-ownership.md`); SharedKernel stays empty marker.  
- Migration squash never required for done.

---

## What is deferred (normal backlog — not maintenance program)

| Item | When / trigger | Notes |
|------|----------------|-------|
| **API key dual-read cutover** | after **2026-11-30**; One-only target **2026-12-15** | Migrate rows, remove dual middleware + dual revoke, later drop `lhdn.DeveloperApiKeys` (≥30d after One-only prod). Design: `api-key-cutover-design.md` |
| **Webhook A convergence** | product schedules LHDN → One dispatcher | C freeze remains until then; do not invent second durable stack |
| **BB code moves** (ports, LLM→Ops, email/messaging, metrics plugins, worker options) | separate PRs when touching those surfaces | Ownership map only this track |
| **Cross-schema SQL hygiene** | normal boundary tickets | Inventory: plan 04 §7.1, Communications receipt joins, metrics SQL; **not fixed** (18.4 open note) |
| **Credits / Webhooks extract / Messaging→Communications merge** | Phase 16 product reopen only | Gate documented in `phase-16-done.md` |
| **Revenue recognition product path** | finance / Xero epic | Job stays unregistered (00.3 park) |
| Wave B TypeSpec (billing PDF, broadcast preview, security schemes, product dual DTOs) | product DX | Deferred from Phase 05 |
| Remaining god-file partials (LhdnGatewayAdapter, LlmOrchestrator, …) | when touching those files | Phase 11.4–11.6 |
| Full ModuleTests → TestSupport migration | gradual | Phase 13 pilot only |

**Explicit non-goals remain:** new modules, microservice split, Meta WhatsApp as “cleanup,” Community/Vault rebuild, migration squash as a done gate.

---

## 18.1–18.7 scorecard (summary)

| Section | Met? | Caveat |
|---------|------|--------|
| 18.1 Safety | **Yes** | Dual-read is **dated**, not closed |
| 18.2 Contracts | **Yes** | P0 only; Wave B backlog |
| 18.3 Navigability | **Yes** | — |
| 18.4 Quality loops | **Mostly** | Cross-schema SQL still open inventory → backlog tickets |
| 18.5 Structural debt | **Yes** | Thinning plan incomplete by design |
| 18.6 Product honesty | **Yes** | — |
| 18.7 Stop criteria | **Yes** | Healthy enough; residuals are tickets |

---

## Stop criteria declaration

**Maintenance track 004 is closed as healthy enough** on branch `chore/backend-maintenance-004`.

Remaining work is **normal product/engineering backlog** with dates and owners above — not a continuing special “maintenance mode” program. Dual-read not fully removed is **intentional interim** until the calendar cutover.

---

## Next (outside this track)

1. Merge branch when product accepts residual dual-path windows.  
2. Calendar: dual-read cutover after **2026-11-30**.  
3. File or schedule tickets for webhook **A**, BB moves, and cross-schema SQL.  
4. Resume product work; reopen Phase 16 only with written product trigger.

---

*Phase 18 closes the 004 maintenance track documentation. Do not expand scope under “one more cleanup phase” without a new program charter.*
