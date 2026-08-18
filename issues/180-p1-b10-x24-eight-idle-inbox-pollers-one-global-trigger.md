---
number: "180"
id: B10-X24
severity: P1
status: resolved
resolved_branch: fix/180-unify-outbox-inbox
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 180 — B10-X24 — Eight idle inbox pollers + one global trigger

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/180-unify-outbox-inbox`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X24 — P1 — Eight idle inbox pollers + one global trigger

Nine inbox consumers × `SELECT ... LIMIT 20 FOR UPDATE SKIP LOCKED` every 5 seconds, plus nine outbox pollers, plus `JobTrigger` waking **every** module on **any** successful `SaveChanges`. Cheap per query (filtered index), chatty on Neon (`Maximum Pool Size=50`). CRM and Ops inboxes are structurally unused.

`AddModuleOutboxInbox` exists to make this consistent and is used by CRM only. Other modules hand-register the same three lines. Drift risk, not a functional bug.

