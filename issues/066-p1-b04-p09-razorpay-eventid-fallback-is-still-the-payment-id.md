---
number: "066"
id: B04-P09
severity: P1
status: resolved
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
resolved_branch: fix/066-razorpay-eventid-fallback
---

# 066 — B04-P09 — Razorpay EventId fallback is still the payment id

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/066-razorpay-eventid-fallback`

Missing X-Razorpay-Event-Id uses PAYMENT_COMPLETED:pay_ / PAYMENT_FAILED:pay_ so fail-then-capture cannot collide.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P09 — P1 — Razorpay EventId fallback is still the payment id

**Where.** `RazorpayGatewayAdapter.cs:138-149`, `336-343`. `a1afc09` did not touch this file.

**What.** Missing `X-Razorpay-Event-Id` on both `payment.failed` and `payment.captured` for the same `pay_…` → same EventId → `GetByEventId` finds the fail → `HandleExistingLogAsync` AlreadyActive → **completed dropped**. Same 008 P0 shape, residual rail. Header is usual; fallback is the hole. No test for fail-then-capture **without** the header.

