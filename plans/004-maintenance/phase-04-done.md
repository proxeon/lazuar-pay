# Phase 04 — Done

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Commit subject:** `docs(api): freeze LHDN webhooks path; document One platform model (phase 04)`  
**Decision applied:** 00.2 — One durable = platform; LHDN end-state **A**; interim **C freeze**

## What landed

### Analysis

- `plans/004-maintenance/phase-04-analysis.md` — full inventory of One vs Lhdn outbound paths (file paths, events, signing differences)

### Docs (module honesty)

1. `Modules/Lhdn/README.md` — §5 freeze: fire-and-forget special-case; reject B; end-state A through One
2. `Modules/One/README.md` — §7 platform webhook model; schema rows for webhook tables; LHDN exception pointer

### Observability (no stack rewrite)

- `Modules/Lhdn/Infrastructure/Services/WebhookSenderService.cs`
  - Structured failure logs: `OrganizationId`, `SubscriptionId`, `Url`, status/exception
  - `LazuarMetrics.RecordWebhookFailed("lhdn")` on non-success and exceptions
  - Comment: frozen until A; do not expand to second durable stack

### Checklist

- `checklists/phase-04-webhooks-converge.md` — 04.1 + C freeze + exit criteria marked; A/B implementation and 04.3 cleanup left open for later convergence

## Explicitly not done (by design)

- Full convergence of LHDN → One dispatcher (option A code)
- Lhdn delivery outbox (option B — rejected)
- Deleting fire-and-forget service / dual registry migration
- Changing LHDN HMAC scheme (would break integrators without dual-verify window)

## Verification

- Phase 03 not in flight on this branch (clean tree; no phase-03 analysis/done); no shared-file conflict beyond independent READMEs/plans
- Webhook change is additive observability only

## Next

Phase 03 dual API keys and/or Phase 05 TypeSpec honesty per checklist order; reopen Phase 04 A when product schedules LHDN webhook convergence.
