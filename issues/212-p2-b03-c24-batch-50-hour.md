---
number: "212"
id: B03-C24
severity: P2
status: resolved
resolved_branch: fix/212-dunning-batch-size
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
---

# 212 — B03-C24 — Batch 50 / hour

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/212-dunning-batch-size`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C24 — P2 — Batch 50 / hour

`BatchSize = 50`, interval 1 hour, both modes. 2 000 PAST_DUE rows → ~40 hours to visit each. Catch-up still fires when visited; terminal is delayed by the queue. Pre-dunning has the same cap.

---

