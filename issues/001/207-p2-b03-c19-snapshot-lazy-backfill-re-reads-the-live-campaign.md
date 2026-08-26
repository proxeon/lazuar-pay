---
number: "207"
id: B03-C19
severity: P2
status: resolved
resolved_branch: fix/207-snapshot-no-live-backfill
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
---

# 207 — B03-C19 — Snapshot lazy-backfill re-reads the live campaign

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/207-snapshot-no-live-backfill`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C19 — P2 — Snapshot lazy-backfill re-reads the live campaign

`ResolveSnapshotAsync` (`PastDueDunningProcessor.cs` 299–334): matching v1 JSON is frozen (E1–E5, `HandleAsync_AlreadyAssigned_LiveCampaignEditDoesNotRewriteSnapshot`). Null / corrupt / wrong `CampaignId` copies **live**, including later edits. `AssignDunningCampaign(Guid)` without JSON (still used in several tests and any pre-migration row) is that path. Production assign sites are supposed to use the snapshot overload (comment on `Subscription.cs` 370–372). They do, **after** first PAST_DUE. Manual pin + edit + first tick = mutated plan.

---

