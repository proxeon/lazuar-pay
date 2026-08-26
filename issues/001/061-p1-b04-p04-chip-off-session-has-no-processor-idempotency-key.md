---
number: "061"
id: B04-P04
severity: P1
status: resolved
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
resolved_branch: fix/061-chip-offsession-idempotency
---

# 061 — B04-P04 — CHIP off-session has no processor idempotency key

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/061-chip-offsession-idempotency`

Off-session create/charge send `Idempotency-Key` and purchase `reference`. A retry looks up the existing purchase instead of creating a second one.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P04 — P1 — CHIP off-session has no processor idempotency key

**Where.** `ChipCollectGatewayAdapter.cs:236` (`_ = idempotencyKey`). Handler still passes `lazuar-offsession:{attempt}` (`ExecuteOffSessionChargeIntegrationEventHandler.cs:66-80`).

**What.** Inbox redelivery after CHIP charged and the HTTP response was lost creates a **second** purchase and a **second** `/charge/`. Stripe is the only adapter with a real off-session idempotency key. Capability says CHIP will be called.

