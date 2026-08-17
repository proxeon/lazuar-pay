---
number: "009"
id: B05-L04
severity: P0
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 009 — B05-L04 — Utility chargeback claw is not idempotent

- **Severity:** P0
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L04 — P0 — Utility chargeback claw is not idempotent

**Where.** `ChargebackClawbackHandler` sends `ClawbackCreditsCommand` **before** `ReverseUtilityTopUpLedgerAsync`. `ClawbackCreditsCommandHandler` has no idempotency log. Reverse is idempotent on `SYSTEM_CREDIT_CHARGEBACK` + gateway tx.

Inbox redelivery after a full success: ledger count stays 1, wallet claws again. `TenantCreditBalance.Clawback` clamps at 0, so the second pass takes leftover starter credits (50) and any unrelated top-up.

**Lying test.** `ChargebackClawbackHandlerTests.UtilityChargeback_IsIdempotent_OnSecondDispute` only asserts ledger count == 1. It does **not** assert `ClawbackCreditsCommand` was sent once. Name says idempotent. Wallet is not.

`UtilityChargeback_ReversesSystemCreditTopupLedger` uses `Received(1)` because it calls Handle once. A two-call assertion on the mediator would fail today.

---

