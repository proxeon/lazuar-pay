---
number: "222"
id: B04-P21
severity: P2
status: open
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
---

# 222 — B04-P21 — Stripe / CHIP fee expand failure is silent `GatewayFee=0`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P21 — P2 — Stripe / CHIP fee expand failure is silent `GatewayFee=0`

**Where.** Stripe `99-102`, `182-186`; CHIP missing `payment` node leaves fee 0 (`185-192`); Billplz always 0 (B04-P — 008 leftover, still true).

**What.** Ledger net = gross. Honesty, not fulfillment.

