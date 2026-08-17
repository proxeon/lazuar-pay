---
number: "162"
id: B10-X06
severity: P1
status: open
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 162 — B10-X06 — `TypeResolver` caches null for the process lifetime

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X06 — P1 — `TypeResolver` caches null for the process lifetime

Quoted in §2.5. Combined with B10-X05 / retry: unresolvable type → five `ApplyFailure` → Dead. Combined with a late-loaded plugin assembly: first outbox of a new event type after a partial deploy can Dead-letter every row of that type until restart — and after restart the AQN might work, but the Dead rows will not be polled (`ProcessedAt` set).

No test of `TypeResolver` exists under `Lazuar.ModuleTests/BuildingBlocks`.

