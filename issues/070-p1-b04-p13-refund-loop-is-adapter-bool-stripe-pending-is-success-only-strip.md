---
number: "070"
id: B04-P13
severity: P1
status: open
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
---

# 070 — B04-P13 — Refund loop is adapter bool; Stripe `pending` is success; only Stripe has an idempotency key

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P13 — P1 — Refund loop is adapter bool; Stripe `pending` is success; only Stripe has an idempotency key

**Where.** `StripeGatewayAdapter.cs:313, 354-360`; CHIP `325-355`; Razorpay `280-294`; Xendit `119-148`; `GatewayRefundRequestedIntegrationEventHandler.cs:48-72`. CHIP subscribe list includes `payment.refunded` (`UpdatePaymentConfigCommandHandler.cs:133`); parser drops it.

**What.** Unchanged from 008. Dashboard refunds never enter. Fee reclaim is always 0. Worker retry of CHIP/Razorpay/Xendit `IssueRefundAsync` can double-refund.

