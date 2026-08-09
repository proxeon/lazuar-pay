# 005 — Remaining work: how-to analyses

**Status:** **Wave closed** on `chore/remaining-005` (R99) — code tracks done/SKIP; **ops residual** (keys migrate+deploy One-only, webhook migrate staging, table-drop clocks). See `r99-notes.md`, `r60-notes.md`.  
**Date:** 2026-08-09  
**Scope:** Remaining items from `plans/004-maintenance/FUTURE-WORK.md` (after maintenance track 00–18)

**Related**

| Resource | Role |
|----------|------|
| [`../004-maintenance/FUTURE-WORK.md`](../004-maintenance/FUTURE-WORK.md) | What / when / done criteria |
| [`../004-maintenance/checklists-future/`](../004-maintenance/checklists-future/) | Phased checklists F00–F16 (execute here) |
| [`../004-maintenance/decisions.md`](../004-maintenance/decisions.md) | Locked product gates |

## Subagent reports (full text — uncondensed)

| # | File | Maps to user bullet | Focus |
|---|------|---------------------|--------|
| 01 | [`01-api-key-one-only-cutover.md`](./01-api-key-one-only-cutover.md) | **1** API key One-only + migration | Code map, migrator, F01–F04 PRs, early cutover |
| 02 | [`02-lhdn-webhooks-one-dispatcher.md`](./02-lhdn-webhooks-one-dispatcher.md) | **2** LHDN → One dispatcher | Dual paths, signing, A1 design, PR-0…cleanup |
| 03 | [`03-bb-llm-move-to-ops.md`](./03-bb-llm-move-to-ops.md) | **3** BB moves (LLM) | OpenAI leak, Ops.Contracts, PR-A/B |
| 04 | [`04-bb-email-messaging-move.md`](./04-bb-email-messaging-move.md) | **3** BB moves (email/msg) | Messaging/Commerce owners, PR order |
| 05 | [`05-bb-metrics-plugins.md`](./05-bb-metrics-plugins.md) | **3** + **4** metrics | Contributors + schema registration |
| 06 | [`06-cross-schema-sql-leaks.md`](./06-cross-schema-sql-leaks.md) | **4** SQL leaks | L-01…L-07, PR A–G |
| 07 | [`07-module-extract-and-merge.md`](./07-module-extract-and-merge.md) | **5** New modules / merge | Stay deferred; full steps if triggered |
| 08 | [`08-typespec-wave-b.md`](./08-typespec-wave-b.md) | **6** TypeSpec Wave B | Dual DTOs left, allowlist, CI honesty |
| 09 | [`09-polish-godfiles-testsupport.md`](./09-polish-godfiles-testsupport.md) | **6** polish / TestSupport | Partials, TestSupport batches, outbox DI |
| 10 | [`10-program-sequencing-and-risks.md`](./10-program-sequencing-and-risks.md) | **All** | Waves, parallel bans, risk matrix, DoD |

## User bullets → analysis map

| # | Remaining feature | Primary analysis | Checklist phases |
|---|-------------------|------------------|------------------|
| 1 | API key One-only + data migration | **01** | F01 → F02 → F03 → F04 |
| 2 | LHDN webhooks → One dispatcher | **02** | F05 → F06 |
| 3 | Full BuildingBlocks moves | **03, 04, 05** | F10 → F13 |
| 4 | Cross-schema SQL leak fixes | **06** (+ **05** for metrics SQL) | F07 → F08 |
| 5 | New modules / Messaging merge | **07** | F15 (gate only) |
| 6 | TypeSpec Wave B + god-file polish + TestSupport | **08, 09** | F09, F14 |

Program orchestration: **10**.

## Detailed implementation checklists (execute here)

→ **[`checklists/README.md`](./checklists/README.md)** — **R00–R99**, many small phases (one phase ≈ one PR)

Granular tracks: Keys R01–R06, SQL R10–R17 (per leak), TypeSpec R20–R25, BB R30–R35, Webhooks R40–R43, Polish R50–R53, Extract R60 (default skip).

Also available: `../004-maintenance/checklists-future/` (F00–F16) as coarser map — prefer **R-series** for accurate implementation.

## How to implement (process)

1. Read **10** for wave plan and “do not parallel” rules.  
2. Start **R00** (which tracks this wave).  
3. For each active phase: use matching **01–09** analysis as the how-to; execute **Rxx** checklist; analyze → implement → commit (same as 004).  
4. Do **not** implement extract (R60) unless product reopens decisions.

## Explicit non-goals of this folder

- No application code changes in the analysis pass itself  
- No forced extract/merge  
- No single mega-PR for all six bullets  
