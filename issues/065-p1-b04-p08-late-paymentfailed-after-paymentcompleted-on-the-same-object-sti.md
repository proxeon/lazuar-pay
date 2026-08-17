---
number: "065"
id: B04-P08
severity: P1
status: resolved
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
resolved_branch: fix/065-ignore-late-payment-failed
---

# 065 — B04-P08 — Late `PAYMENT_FAILED` after `PAYMENT_COMPLETED` on the same object still publishes

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/065-ignore-late-payment-failed`

A late PAYMENT_FAILED for a GatewayTransactionId that already has PAYMENT_COMPLETED is ignored.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P08 — P1 — Late `PAYMENT_FAILED` after `PAYMENT_COMPLETED` on the same object still publishes

**Where.** Handler has no “if completed already exists for this `GatewayTransactionId`, ignore fail” check. EventIds after `a1afc09` are different, so the fail is fresh.

**What.** Billplz replay of `paid=false` after pay. CHIP `purchase.payment_failure` after `purchase.paid`. Xendit `EXPIRED` after `PAID` (rare). Payments will emit `GatewayPaymentFailed` after completed. M2M ignores it if already completed (good). Commerce is out of scope; the cashier still lies that the payment failed.

