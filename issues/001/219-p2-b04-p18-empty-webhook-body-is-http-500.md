---
number: "219"
id: B04-P18
severity: P2
status: resolved
resolved_branch: fix/219-empty-webhook-body-400
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
---

# 219 — B04-P18 — Empty webhook body is HTTP 500

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/219-empty-webhook-body-400`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P18 — P2 — Empty webhook body is HTTP 500

**Where.** `Endpoints.cs:45-48`, catch at `84-88` rethrows `InvalidOperationException`.

**What.** Bad sender / health check / empty retry storms the error log and the gateway retry queue. Not lost money.

