# R99 — Definition of done (remaining-work program closed)

**Date:** 2026-08-09  
**Branch:** `chore/remaining-005`  
**Checklist:** `checklists/r99-definition-of-done.md`  
**Analysis:** `10-program-sequencing-and-risks.md`  
**Scope this pass:** **Docs only** — honest close-out of the 005 remaining program. Residuals become **normal ops tickets**, not an open mega-program.

---

## Declaration

**Wave closed** on `chore/remaining-005` for all **selected** code tracks.

| Band | Code / design | Residual |
|------|---------------|----------|
| Program | **Closed** | Ops tickets listed below |
| Extract (R60) | **SKIP** | Product gate only (not a residual ticket) |

This is not “everything is live in prod.” It is: **implementable 005 scope is done or explicitly deferred/dated**, and remaining work is **calendar/ops deploy**, not architecture backlog.

---

## Per-track honesty

### Keys (R01–R06)

| Phase | State |
|-------|--------|
| R01–R03 | **Code done** (inventory, migrator, runbook) |
| R04 | **Ops pending** — staging/prod migrate not executed from this branch |
| R05 | **Code on branch**, **deploy-gated** until Q8 `active_legacy_only = 0` (or signed residual) |
| R06 | **Deferred** — ≥ **30 days** after One-only **in prod** (clock not started) |

**Document as:** **code complete, ops/deploy residual.**

### SQL (R10–R17)

| Phase | State |
|-------|--------|
| R11–R15 | **Fixed** (L-01…L-05) |
| R16 → R35 | **Done** (metrics handoff resolved by plugins) |
| R17 → R05 | **Done** (API-key dual-read SQL handoff resolved by One-only code) |

### TypeSpec (R20–R25)

| Phase | State |
|-------|--------|
| R20–R25 | **All done** |

### BuildingBlocks (R30–R35)

| Phase | State |
|-------|--------|
| R30–R35 | **All done** |

### Webhooks (R40–R43)

| Phase | State |
|-------|--------|
| R40–R43 | **Code done** (product lock, registry backfill tooling, enqueue A1, fire-and-forget retired) |
| Staging/prod | **Ops residual** (registry migrate + live delivery verify) |

### Polish (R50–R53)

| Phase | State |
|-------|--------|
| R50–R53 | **Done** (TestSupport batch, Lhdn gateway partials, LLM stream partial, GatewayCommon + outbox DI pilot) |

### Extract (R60)

| Phase | State |
|-------|--------|
| R60 | **SKIP** — no product trigger; decisions not reopened (`r60-notes.md`) |

---

## Residual ops tickets (normal tickets, not mega-program)

These are the **only** open exits after R99. Track them as ops/deploy calendar work:

| # | Ticket (suggested title) | Track | Notes / clock |
|---|--------------------------|-------|----------------|
| 1 | **Keys: migrate + deploy One-only** | R04 → R05 | Staging then prod: run migrator → verify Q8 = 0 → deploy One-only middleware. **Do not** deploy R05 until residual 0 or signed quarantine. |
| 2 | **Webhooks: migrate staging** (then prod) | R41 ops / R42.4 | Registry backfill on live envs; confirm LHDN VALID/INVALID → `one.WebhookDeliveryOutboxes` + dispatcher delivery. |
| 3 | **Table drops clocks** | R06 (+ optional later webhook sub table) | Start **R06 clock** only after One-only **live in prod** ≥ **30d**; then drop/archive `lhdn.DeveloperApiKeys`. Optional later: `lhdn.WebhookSubscriptions` after façade period (not blocking). |

No second Lhdn webhook stack. No early dual-read removal without migrate. No extract without product reopen.

---

## Docs exit (R99.2)

| Artifact | Action |
|----------|--------|
| `FUTURE-WORK.md` | Statuses updated for completed FW streams; FW-1 remains partial (ops) |
| One / Lhdn READMEs | Dual-path language closed for finished tracks (keys code + webhooks code) |
| Residuals | Listed as ops tickets above — not open “005 mega remaining program” |

---

## FW map after close

| FW | Post-R99 |
|----|----------|
| FW-1 Keys | **Partial** — code complete; migrate + One-only deploy residual; R06 dated |
| FW-2 Webhooks | **Done** (code); staging/prod verify is ops ticket #2 |
| FW-3 BB moves | **Done** (R30–R35) |
| FW-4 SQL leaks | **Done** (R11–R15 + handoffs) |
| FW-5 Extract | **SKIP / product-gated** (R60) |
| FW-6 TypeSpec Wave B | **Done** (R20–R25) |
| FW-7 Polish | **Done** (R50–R53 for this wave) |

---

## Exit checklist

- [x] Per selected track assessed honestly
- [x] R60 SKIP documented
- [x] FUTURE-WORK statuses updated
- [x] Residuals are normal ops tickets
- [x] **Wave declared closed**

**Evidence:** checklists R60/R99, FULL-CHECKLIST, `wave-decisions.md`, this file.
