---
number: "064"
id: B04-P07
severity: P1
status: resolved
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
resolved_branch: fix/064-offsession-pending-not-success
---

# 064 — B04-P07 — Off-session success is webhook-only; `processing` / `pending_charge` are adapter-true

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/064-offsession-pending-not-success`

Stripe off-session is success only when `succeeded`. CHIP off-session is success only when `paid`.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P07 — P1 — Off-session success is webhook-only; `processing` / `pending_charge` are adapter-true

**Where.** `ExecuteOffSessionChargeIntegrationEventHandler` publishes nothing on success. Stripe `intent.Status == "succeeded" || "processing"` (`289`). CHIP `status == "paid" || "pending_charge"` (`311`).

**What.** A Stripe PI in `processing` that later fails publishes `PAYMENT_FAILED` (good, different EventId). Until then Commerce has no completed event and the adapter already returned true to the inbox handler (which does not tell Commerce it succeeded). A CHIP `pending_charge` that never becomes `purchase.paid` is a silent hole: adapter true, no completed webhook, subscription renewal hangs. This is the designed loop; it is still a bug when `pending_*` is treated as success at the adapter.

