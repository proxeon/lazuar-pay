---
number: "067"
id: B04-P10
severity: P1
status: resolved
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
resolved_branch: fix/067-offsession-fail-txid
---

# 067 — B04-P10 — Off-session fail `GatewayTransactionId` is `off_session:{subscriptionId}`

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/067-offsession-fail-txid`

Off-session fail uses `off_session_attempt:{chargeAttemptId}` so two fails on the same seat do not share a transaction id.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P10 — P1 — Off-session fail `GatewayTransactionId` is `off_session:{subscriptionId}`

**Where.** `ExecuteOffSessionChargeIntegrationEventHandler.cs:149-152`.

**What.** Every fail for that subscription shares the same transaction id. Not logged in `PaymentWebhookLog` today. Any consumer that keys on it (or a future Payments-side dedupe) collapses attempts. `ChargeAttemptId` is only metadata.

