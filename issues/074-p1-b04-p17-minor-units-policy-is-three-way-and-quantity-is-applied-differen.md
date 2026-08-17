---
number: "074"
id: B04-P17
severity: P1
status: resolved
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
resolved_branch: fix/074-minor-units-policy
---

# 074 — B04-P17 — Minor-units policy is three-way and quantity is applied differently

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/074-minor-units-policy`

One ToMinorUnits policy: half away from zero. Zero-decimal currencies are not ×100. Stripe uses the same helper.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P17 — P1 — Minor-units policy is three-way and quantity is applied differently

**Where.** `GatewayCommon.ToMinorUnitsRounded` (banker's, CHIP/Xendit); `ToMinorUnitsTruncating` (Billplz/Razorpay); Stripe checkout `amount * 100` decimal; Stripe off-session/refund `(long)(amount * 100)` truncate.

**What.** `10.005` MYR: CHIP banker's `1000` sen; Billplz truncate `1000`; Stripe checkout `1000.5` sen; Stripe refund `1000`. Quantity: Stripe is unit × line qty; others pre-multiply `amount * quantity * 100`. Callers that pass a line total **and** `quantity > 1` double-count on CHIP/Billplz/Razorpay/Xendit. The query comment says line-total callers must pass `quantity = 1` (`GenerateCheckoutSessionQuery.cs:10-11`). M2M hard-codes `quantity: 1` (`CreateIntegrationCheckoutCommandHandler.cs:147`). Commerce hop 2 passes product quantity (`InitiateCheckoutCommandHandler.cs:359`) with **unit** amount — correct if every adapter obeys the comment. Stripe does. The others fold qty into one product line — also correct **if** amount is unit. The hazard is a future caller passing a line total into CHIP with qty > 1.

Zero-decimal currencies (JPY, KRW) are `* 100` on every rail. Not a MY launch bug; a latent one.

`CheckoutAmountRules.MyrMinimum = 2.00` applies only to M2M (`CreateIntegrationCheckoutCommandHandler.cs:189`). Commerce `$0` setup bypasses it. Honest.

