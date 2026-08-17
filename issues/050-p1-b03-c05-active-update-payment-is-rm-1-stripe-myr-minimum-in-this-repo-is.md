---
number: "050"
id: B03-C05
severity: P1
status: open
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
---

# 050 — B03-C05 — ACTIVE update-payment is RM 1; Stripe MYR minimum in this repo is RM 2

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C05 — P1 — ACTIVE update-payment is RM 1; Stripe MYR minimum in this repo is RM 2

**Evidence.** Arrears POST hard-codes `1m` (line 145). `CheckoutAmountRules.MyrMinimum = 2.00m` is enforced on **M2M** `CreateIntegrationCheckout`, not on `GenerateCheckoutSessionQuery` (Commerce cashier). Stripe adapter sends `UnitAmountDecimal = amount * 100` (100 sen). Stripe’s documented MYR floor is 2.00; the host’s own rule agrees.

**Repro.** ACTIVE MYR Stripe sub, Update payment method. Session create fails (`amount_too_small`) → portal `?err=1`. Or session creates and capture fails — then B03-C01.

**Blast.** The only authenticated “change card while healthy” path for Malaysian Stripe/CHIP is broken or flaky. Buyers stay on the old PM until PAST_DUE.

**Tests.** None assert RM 1 vs minimum. `CheckoutAmountRules` tests live under Payments and never call arrears.

**Fix direction.** Use Stripe Checkout `mode=setup` (no capture) for ACTIVE updates, **or** charge `max(2, verification)` and treat it as `update_payment` (and fix B03-C01). Do not leave RM 1 as the sold behaviour.

---

