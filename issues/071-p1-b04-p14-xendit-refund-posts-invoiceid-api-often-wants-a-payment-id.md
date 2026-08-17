---
number: "071"
id: B04-P14
severity: P1
status: open
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
---

# 071 — B04-P14 — Xendit refund posts `invoice_id`; API often wants a payment id

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P14 — P1 — Xendit refund posts `invoice_id`; API often wants a payment id

**Where.** `XenditGatewayAdapter.cs:126-131`. `GatewayTransactionId` is the invoice id (`327`). `RequiresMarkRefunded("XENDIT")` is false.

**What.** Unsoaked. Failure is at least visible (`GatewayRefundFailed`). There is no mark-refunded escape hatch. No refund test exists for Xendit.

