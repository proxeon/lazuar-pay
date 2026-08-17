---
number: "066"
id: B04-P09
severity: P1
status: open
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
---

# 066 — B04-P09 — Razorpay EventId fallback is still the payment id

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P09 — P1 — Razorpay EventId fallback is still the payment id

**Where.** `RazorpayGatewayAdapter.cs:138-149`, `336-343`. `a1afc09` did not touch this file.

**What.** Missing `X-Razorpay-Event-Id` on both `payment.failed` and `payment.captured` for the same `pay_…` → same EventId → `GetByEventId` finds the fail → `HandleExistingLogAsync` AlreadyActive → **completed dropped**. Same 008 P0 shape, residual rail. Header is usual; fallback is the hole. No test for fail-then-capture **without** the header.

