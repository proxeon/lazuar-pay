# 004 Future work — phase checklists (FW-1…FW-7)

**Status:** Ready to execute as a **phased program** (not one mega-PR)  
**Date:** 2026-08-09  
**Parent handoff:** [`../FUTURE-WORK.md`](../FUTURE-WORK.md)  
**Locked decisions:** [`../decisions.md`](../decisions.md)

## Clarification: “not one go” vs “long checklist”

| Approach | Meaning | Recommended? |
|----------|---------|----------------|
| **One go / one PR** | Land keys cutover + webhooks + BB moves + SQL + extracts + TypeSpec in a single branch | **No** — unreviewable, mixed risk, hard rollback |
| **Long multi-phase checklist** (like 00–18) | One **program** with many small phases; each phase = analyze → implement → commit | **Yes** — this folder |

We **can and should** plan remaining work like the previous maintenance track. We should **not** squash all phases into one commit/merge.

## How to run

1. Work **one phase file at a time** (or one PR per phase / small phase group).  
2. Honor **gates** (product / calendar / row counts) before starting gated phases.  
3. Prefer many PRs on a long-lived branch **or** stacked PRs — not one giant diff.  
4. When a phase finishes, mark its checklist and add `phase-XX-done.md` under `../` if useful.

## Phase map

| Phase | File | Maps to | Gate |
|-------|------|---------|------|
| **F00** | [`phase-f00-program-align.md`](./phase-f00-program-align.md) | Kickoff | None — lock which tracks run now |
| **F01** | [`phase-f01-api-key-inventory.md`](./phase-f01-api-key-inventory.md) | FW-1 | None |
| **F02** | [`phase-f02-api-key-migrate.md`](./phase-f02-api-key-migrate.md) | FW-1 | F01 complete |
| **F03** | [`phase-f03-api-key-one-only.md`](./phase-f03-api-key-one-only.md) | FW-1 | F02 complete; dual-read window rules |
| **F04** | [`phase-f04-api-key-table-drop.md`](./phase-f04-api-key-table-drop.md) | FW-1 | ≥30d One-only in prod (or N/A) |
| **F05** | [`phase-f05-webhooks-product-decisions.md`](./phase-f05-webhooks-product-decisions.md) | FW-2 | Product |
| **F06** | [`phase-f06-webhooks-one-dispatcher.md`](./phase-f06-webhooks-one-dispatcher.md) | FW-2 | F05 locked |
| **F07** | [`phase-f07-cross-schema-inventory.md`](./phase-f07-cross-schema-inventory.md) | FW-4 | None |
| **F08** | [`phase-f08-cross-schema-fix-leaks.md`](./phase-f08-cross-schema-fix-leaks.md) | FW-4 | F07 ticket list |
| **F09** | [`phase-f09-typespec-wave-b.md`](./phase-f09-typespec-wave-b.md) | FW-6 | None |
| **F10** | [`phase-f10-bb-port-hygiene.md`](./phase-f10-bb-port-hygiene.md) | FW-3 | None |
| **F11** | [`phase-f11-bb-llm-to-ops.md`](./phase-f11-bb-llm-to-ops.md) | FW-3 | Optional after F10 |
| **F12** | [`phase-f12-bb-email-messaging.md`](./phase-f12-bb-email-messaging.md) | FW-3 | Optional; respect 00.4 freeze |
| **F13** | [`phase-f13-bb-metrics-plugins.md`](./phase-f13-bb-metrics-plugins.md) | FW-3 / FW-4 | Optional |
| **F14** | [`phase-f14-polish-god-files-testsupport.md`](./phase-f14-polish-god-files-testsupport.md) | FW-7 | Opportunistic |
| **F15** | [`phase-f15-module-extract-gate.md`](./phase-f15-module-extract-gate.md) | FW-5 | **Product trigger only** |
| **F16** | [`phase-f16-definition-of-done.md`](./phase-f16-definition-of-done.md) | Meta | When tracks complete |

## Recommended execution order

```text
F00 (which tracks this quarter?)
  │
  ├─► Track Keys:     F01 → F02 → F03 → (wait) → F04
  ├─► Track SQL:      F07 → F08 (can parallel Keys after F00)
  ├─► Track TypeSpec: F09 (parallel anytime)
  ├─► Track BB:       F10 → F11 → F12 → F13 (parallel, low risk)
  ├─► Track Polish:   F14 (anytime)
  ├─► Track Webhooks: F05 → F06 (only after product)
  └─► Track Extract:  F15 (only if product reopens Phase 16)
  │
  └─► F16 close-out
```

## PR hygiene (every phase)

- [ ] One phase intent per PR (or tightly related sub-items only)
- [ ] Tests / `task gen` as applicable
- [ ] No outbox type renames without a migration note
- [ ] Update `FUTURE-WORK.md` section status when a stream completes
