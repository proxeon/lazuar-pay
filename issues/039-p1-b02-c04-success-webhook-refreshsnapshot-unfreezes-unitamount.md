---
number: "039"
id: B02-C04
severity: P1
status: open
source: plans/009-bugs/02-commerce-subscriptions-billing-engine.md
head: "297ba98"
---

# 039 — B02-C04 — Success webhook RefreshSnapshot unfreezes UnitAmount

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/02-commerce-subscriptions-billing-engine.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B02-C04 — P1 — Success webhook RefreshSnapshot unfreezes UnitAmount

**Evidence.** `GatewayPaymentCompletedIntegrationEventHandler.Subscription.cs` 66–85. After Activate / Recover / Resume, `RefreshSnapshot(catalogUnit)` where `catalogUnit` is the live `ProductPrice.Amount` for `PriceId`, else `product.Price`.

**Repro.** ACTIVE, `UnitAmount=40` (negotiated), catalog now 90. Due tick charges 40 (or Gross). Webhook success. `UnitAmount` is 90. Next MRR card and next cycle use 90.

**Blast radius.** Every successful renewal, trial convert, arrears pay, clerk record-payment does **not** go through this handler (clerk does not RefreshSnapshot — only the gateway path). Gateway path is all Stripe/CHIP auto-renew and all hosted-bill pays.

**Tests.** `CommerceMrrTests.CatalogEditDoesNotChangeSnapshotMath` lies about this (helper-only). `H6_ActiveRenewal_DoesNotIncrement` asserts dates move, not UnitAmount.

**Fix direction.** Do not `RefreshSnapshot` on renewal unless ProductId/PriceId just changed. If the job already wrote the new plan snapshot, leave it. Catalog edits should move MRR only via an explicit “reprice” verb, which does not exist.

---

