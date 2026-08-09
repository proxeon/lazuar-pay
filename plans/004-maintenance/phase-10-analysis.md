# Phase 10 — Analysis (`DunningEngineJob` split)

**Date:** 2026-08-09  
**Branch:** `chore/backend-maintenance-004`  
**Goal:** Separable claim / past-due / pre-dunning / dispatch without behavior change.  
**Evidence:** `checklists/phase-10-dunning-engine-split.md`, `02-large-files-chunking.md` §3.3

---

## 1. Behavioral inventory (pre-split)

**Path:** `Modules/Commerce/Infrastructure/Workers/DunningEngineJob.cs`  
**Size before:** **519 LOC** (single file)

### 1.1 Phases inside the job

| Phase | Members | Notes |
|-------|---------|-------|
| Hosted loop + orchestration | `ExecuteAsync`, `RunOnceAsync`, `ProcessDunningAsync` | Interval from `BackgroundWorkerOptions.DunningEngineInterval`; load active campaigns once |
| Claim / lock batch | `ClaimMode`, `ProcessClaimedBatchAsync`, `ClaimSubscriptionAsync`, `ClaimSubscriptionInMemoryAsync` | Postgres `FOR UPDATE SKIP LOCKED` vs in-memory for tests; failed-id exclusion; per-sub scope + tx |
| Pre-dunning | `ProcessPreDunningSubscriptionAsync` | `DayOffset < 0`, ACTIVE subs in 14-day window |
| Past-due | `ProcessPastDueSubscriptionAsync` | Campaign assign, grace final CANCEL/SUSPEND + metrics, AUTOCHARGE, communication steps |
| Dispatch | `ResolveEffectiveCommunicationAction`, `DispatchCommunicationStepAsync` | WhatsApp demotion; `FulfillmentRequestedIntegrationEvent` → COMMUNICATIONS |

### 1.2 Stability constraints

| Surface | Callers / registration |
|---------|------------------------|
| Type name `DunningEngineJob` | `Commerce.Infrastructure.DependencyInjection` → `AddHostedService<DunningEngineJob>()` |
| `RunOnceAsync` | Hosted loop + future/module tests (internal) |
| Interval source | `BackgroundWorkerOptions.DunningEngineInterval` — **not changed** |
| Claim safety | Same scoped DbContext + transaction around claim→process→save; no nested scopes mid-claim |

---

## 2. Target layout (implemented)

Prefer **partial class** files (low ceremony; no DI churn) over extracted collaborator services — matches `CommerceQueryService` / phase 09 provision handler.

```
Modules/Commerce/Infrastructure/Workers/
  DunningEngineJob.cs              # BackgroundService loop + ProcessDunningAsync orchestration
  DunningEngineJob.Claim.cs        # ClaimMode + batch claim/lock + Postgres/in-memory claim
  DunningEngineJob.PreDunning.cs   # ProcessPreDunningSubscriptionAsync
  DunningEngineJob.PastDue.cs      # ProcessPastDueSubscriptionAsync (largest block)
  DunningEngineJob.Dispatch.cs     # ResolveEffective + DispatchCommunicationStepAsync
```

| File | ~LOC after split |
|------|------------------|
| `DunningEngineJob.cs` | 89 |
| `DunningEngineJob.Claim.cs` | 163 |
| `DunningEngineJob.PreDunning.cs` | 65 |
| `DunningEngineJob.PastDue.cs` | 195 |
| `DunningEngineJob.Dispatch.cs` | 69 |

Largest single file after split: **PastDue ~195 LOC** (was 519 monolith).

---

## 3. Move rules applied

- [x] Type name `DunningEngineJob` unchanged (`partial class` still `: BackgroundService`)
- [x] Hosted service registration unchanged (single `AddHostedService<DunningEngineJob>()`)
- [x] Polling interval source unchanged
- [x] Claim safety semantics unchanged (relational tx + SKIP LOCKED; in-memory path for tests)
- [x] WhatsApp effective-action demotion preserved exactly in Dispatch partial
- [x] Metrics (`LazuarMetrics.RecordDunningCancel`) remain only in PastDue grace path
- [x] No new DI registrations / no collaborator service types

---

## 4. Risk mitigations

| Risk | Mitigation |
|------|------------|
| Transaction / claim semantics | Claim + process + save stay in same scope/tx in `ProcessClaimedBatchAsync` |
| Double-dispatch reminders | Unchanged `ReminderLogs` matching; domain tests green |
| Metrics double-count | Cancel metric only in PastDue final-action branch |
| WhatsApp fallback | `ResolveEffectiveCommunicationAction` moved byte-stable into Dispatch partial |
