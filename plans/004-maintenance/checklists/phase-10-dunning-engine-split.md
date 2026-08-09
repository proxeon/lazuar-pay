# Phase 10 — Split `DunningEngineJob`

**Goal:** Separable claim / past-due / pre-dunning / dispatch without behavior change.  
**File:** `Modules/Commerce/Infrastructure/Workers/DunningEngineJob.cs` (~519 LOC)  
**Evidence:** `../02-large-files-chunking.md` §3.3

---

## 10.1 Inventory phases inside job

- [ ] Claim / lock loop
- [ ] Pre-dunning branch
- [ ] Past-due branch
- [ ] Communications dispatch
- [ ] Metrics / logging

## 10.2 Split approach

- [ ] Prefer `partial class DunningEngineJob` files **or** private collaborator services registered in Commerce DI
- [ ] Keep job type name stable (hosted service registration)
- [ ] Do not change polling interval sources without intent

## 10.3 Suggested file split

- [ ] `DunningEngineJob.cs` — orchestrate tick
- [ ] `DunningEngineJob.Claim.cs` / claim helper
- [ ] `DunningEngineJob.PastDue.cs`
- [ ] `DunningEngineJob.PreDunning.cs`
- [ ] `DunningEngineJob.Dispatch.cs`

## 10.4 Tests

- [ ] Domain dunning tests still pass
- [ ] Add or extend job-level tests if missing (gap: DunningEngineJob under-tested — optional improve)
- [ ] Manual/staging: one dunning cycle dry observation if available

## 10.5 Exit criteria

- [ ] Each phase readable in isolation
- [ ] Hosted service still registered once
- [ ] No change to claim safety semantics
