# Phase 10 — Split `DunningEngineJob`

**Goal:** Separable claim / past-due / pre-dunning / dispatch without behavior change.  
**File:** `Modules/Commerce/Infrastructure/Workers/DunningEngineJob.cs` (~519 LOC)  
**Evidence:** `../02-large-files-chunking.md` §3.3

---

## 10.1 Inventory phases inside job

- [x] Claim / lock loop → `DunningEngineJob.Claim.cs`
- [x] Pre-dunning branch → `DunningEngineJob.PreDunning.cs`
- [x] Past-due branch → `DunningEngineJob.PastDue.cs`
- [x] Communications dispatch → `DunningEngineJob.Dispatch.cs`
- [x] Metrics / logging → remain in PastDue / PreDunning / Claim (no behavior move)

## 10.2 Split approach

- [x] Prefer `partial class DunningEngineJob` files **or** private collaborator services registered in Commerce DI  
  → partials (no DI churn)
- [x] Keep job type name stable (hosted service registration)
- [x] Do not change polling interval sources without intent

## 10.3 Suggested file split

- [x] `DunningEngineJob.cs` — orchestrate tick
- [x] `DunningEngineJob.Claim.cs` / claim helper
- [x] `DunningEngineJob.PastDue.cs`
- [x] `DunningEngineJob.PreDunning.cs`
- [x] `DunningEngineJob.Dispatch.cs`

## 10.4 Tests

- [x] Domain dunning tests still pass → Dunning + related Commerce filters **34/34**
- [x] Add or extend job-level tests if missing (gap: DunningEngineJob under-tested — optional improve)  
  → deferred; no new job suite this phase
- [ ] Manual/staging: one dunning cycle dry observation if available → N/A this pass

## 10.5 Exit criteria

- [x] Each phase readable in isolation
- [x] Hosted service still registered once
- [x] No change to claim safety semantics
