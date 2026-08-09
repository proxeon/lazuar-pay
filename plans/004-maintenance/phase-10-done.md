# Phase 10 — Done

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Commit subject:** `refactor(commerce): split DunningEngineJob into partials (phase 10)`

## What landed

### Partials (`Modules/Commerce/Infrastructure/Workers/`)

| File | Responsibility |
|------|----------------|
| `DunningEngineJob.cs` | Fields, ctor, `ExecuteAsync`, `RunOnceAsync`, `ProcessDunningAsync` orchestration |
| `DunningEngineJob.Claim.cs` | `ClaimMode`, batch claim/lock loop, Postgres SKIP LOCKED + in-memory claim |
| `DunningEngineJob.PreDunning.cs` | Pre-due reminder steps (`DayOffset < 0`) |
| `DunningEngineJob.PastDue.cs` | Campaign assign, grace CANCEL/SUSPEND + metrics, AUTOCHARGE + communication steps |
| `DunningEngineJob.Dispatch.cs` | WhatsApp effective-action resolve + COMMUNICATIONS fulfillment dispatch |

### Size

- Pre-split monolith: **519 LOC**
- Post-split largest file: PastDue **~195 LOC**
- Orchestrator: **~89 LOC**
- No single file owns the full dunning pipeline at 500+ LOC

### Plans

- `phase-10-analysis.md` — inventory, layout, rules  
- `checklists/phase-10-dunning-engine-split.md` — criteria marked done  

## Verification

| Check | Result |
|-------|--------|
| `Modules.Commerce.Infrastructure` build | **0 warnings, 0 errors** |
| Filter: Dunning + SubscriptionRecovery + ChargeAttemptLog + GatewayPaymentFailed + BillingEngineJob | **34/34 passed** |
| Hosted registration | Still `services.AddHostedService<DunningEngineJob>()` once |
| Type / interval renames | None |

## Exit criteria

| Criterion | Status |
|-----------|--------|
| Each phase readable in isolation | Yes — Claim / PreDunning / PastDue / Dispatch partials |
| Hosted service still registered once | Yes |
| No change to claim safety semantics | Yes — same scope/tx + SKIP LOCKED path |
| Behavior parity | Related Commerce suite green; mechanical partial split only |

## Explicitly not done

- New job-level `DunningEngineJob` integration tests (optional gap; still under-tested per hygiene docs)
- Extract collaborator DI services (`DunningSubscriptionClaimer`, etc.) — partials preferred first
- Polling interval or claim SQL changes
- Manual/staging dry-run observation

## Next

Phase 11 more god-file splits (or next checklist item on the maintenance track).
