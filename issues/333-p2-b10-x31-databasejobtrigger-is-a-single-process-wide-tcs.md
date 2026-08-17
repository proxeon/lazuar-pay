---
number: "333"
id: B10-X31
severity: P2
status: open
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 333 — B10-X31 — `DatabaseJobTrigger` is a single process-wide TCS

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X31 — P2 — `DatabaseJobTrigger` is a single process-wide TCS

Any module’s `SaveChanges` wakes **all** outbox/inbox jobs. Harmless extra polls. Does not cross replicas (those rely on 5s). Tests construct it; none prove multi-waiter correctness (the swap is racy-looking but `Interlocked.Exchange` + `TrySetResult` is the usual pattern).

