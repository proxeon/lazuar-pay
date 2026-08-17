---
number: "045"
id: B02-C10
severity: P1
status: open
source: plans/009-bugs/02-commerce-subscriptions-billing-engine.md
head: "297ba98"
---

# 045 — B02-C10 — UnitAmount > 0 sentinel cannot represent a $0 snapshot

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/02-commerce-subscriptions-billing-engine.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B02-C10 — P1 — UnitAmount > 0 sentinel cannot represent a $0 snapshot

**Evidence.** `SubscriptionBillingAmount.Unit` and `CommerceMrr.MonthlyEquivalent` both treat `<= 0` as missing and fall back to catalog / fallbackUnit.

**Repro.** ACTIVE, `UnitAmount=0`, `Quantity=1`, product.Price=100, Stripe vaulted, due. Off-session amount 100 (or 108), not 0. MRR 100, not 0.

**Blast radius.** 100% coupon lifetime, COMPED-as-price if anyone stored 0, Wave 3 default-0 rows that were **meant** to stay free. Pre-wave backfill wanting catalog is the conflicting intent. The same operator cannot express both.

**Tests.** `SnapshotZero_FallsBackToCatalog` **asserts** the sentinel. It will go red if you fix this without a real “missing” nullable.

**Fix direction.** Nullable `UnitAmount` or a `HasSnapshot` bit. `0` must be 0. Missing uses catalog.

---

