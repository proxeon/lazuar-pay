---
number: "005"
id: B04-P01
severity: P0
status: resolved
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
resolved_branch: fix/005-chip-preauthorized-vault
---

# 005 — B04-P01 — CHIP `$0` + `skip_capture` never fulfills and never vaults

- **Severity:** P0
- **Status:** resolved
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/005-chip-preauthorized-vault`

`purchase.preauthorized` with a recurring token (CHIP `$0` + `skip_capture` vault) now maps to `PAYMENT_COMPLETED` and extracts customer + token. A non-zero auth-hold without a token stays `purchase.preauthorized` and is not treated as paid.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P01 — P0 — CHIP `$0` + `skip_capture` never fulfills and never vaults

**Where.** `ChipCollectGatewayAdapter.cs:79-87` (sets `skip_capture` when `setupFutureUsage` and cents == 0); `164-167` (drops `purchase.preauthorized`); `UpdatePaymentConfigCommandHandler.cs:133` (registers `purchase.preauthorized`); Commerce caller `InitiateCheckoutCommandHandler.cs:286-316` (now mints hop-2 for `$0` recurring on CHIP).

**What.** CHIP official callbacks: `skip_capture=true` success callback fires on **capture**, not on buyer completion. We never capture. `purchase.paid` does not fire. `purchase.preauthorized` is verified and returned as raw type; the handler returns without a log. No `GatewayPaymentCompleted`. `ExtractVaultIds` never runs.

**Why it is P0.** After `8b3567d`, a 100% coupon or `$0` recurring CHIP product is a hosted purchase the buyer finishes. Lazuar ACKs nothing and stores no token. Stripe on the same product works (setup mode). The commit claimed both rails.

**Not fixed by EventId namespacing.** There is no second event to namespace.

**Test that lies by omission.** `ParseWebhook_Preauthorized_IsVerified_NotPaymentCompleted` asserts the drop and never asserts a `$0` vault extract. There is **no** test that CHIP `$0` generate sets `skip_capture`. There is **no** test that a preauthorized payload with `is_recurring_token` / `recurring_token` yields vault ids.

