---
number: "037"
id: B02-C02
severity: P1
status: resolved
source: plans/009-bugs/02-commerce-subscriptions-billing-engine.md
head: "297ba98"
resolved_branch: fix/037-pending-product-after-load
---

# 037 — B02-C02 — Missing pending product commits a broken ProductId

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/02-commerce-subscriptions-billing-engine.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/037-pending-product-after-load`

Pending plan apply waits until the target product loads. A missing target leaves ProductId and PendingProductId unchanged.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B02-C02 — P1 — Missing pending product commits a broken ProductId

**Evidence.** `ApplyPendingPlanChange()` writes `ProductId` and clears pending, then the job reloads. On null product it `failedIds.Add` and `return`s. `ProcessBillingAsync` treats that as success and `SaveChanges`. There is no restore of the old ProductId.

**Repro.** ACTIVE due, `PendingProductId` = random guid not in `Products`. `RunOnce`. Row is still ACTIVE (or TRIALING), `ProductId` is the missing guid, `PendingProductId` is null. Next ticks hit the first missing-product skip forever. Buyer cannot be billed. Ops change-plan undo looks at current ProductId, which is already the ghost.

**Blast radius.** Any scheduled change onto a product that was archived-and-deleted, or a bad id written by hand. Low frequency, high stuckness. No self-heal.

**Tests.** None. `RunOnce_AppliesPendingProductThenChargesNewPrice` only uses a live target. `RunOnce_MissingProduct_*` uses a sub whose **current** product is missing (no pending apply).

**Fix direction.** Apply pending only after the reload succeeds. Or throw (so the transaction rolls back and pending remains). Or restore `ProductId` / `PendingProductId` before return. Never `SaveChanges` a ghost id.

---

