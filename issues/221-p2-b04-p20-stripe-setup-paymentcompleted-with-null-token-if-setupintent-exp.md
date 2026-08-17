---
number: "221"
id: B04-P20
severity: P2
status: open
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
---

# 221 — B04-P20 — Stripe setup `PAYMENT_COMPLETED` with null token if SetupIntent expand fails

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P20 — P2 — Stripe setup `PAYMENT_COMPLETED` with null token if SetupIntent expand fails

**Where.** `StripeGatewayAdapter.cs:107-125`. Catch logs warning, continues, still returns `PAYMENT_COMPLETED` amount 0 (`130-146`).

**What.** Buyer finished setup. We tell Commerce “paid / vaulted” with no PM. Commerce vault persist requires both ids (other slice). Subscription may activate reminder-only after a setup checkout. `setup_intent.succeeded` is not a backup map.

