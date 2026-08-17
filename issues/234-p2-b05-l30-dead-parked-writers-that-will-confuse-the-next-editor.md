---
number: "234"
id: B05-L30
severity: P2
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 234 — B05-L30 — Dead / parked writers that will confuse the next editor

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L30 — P2 — Dead / parked writers that will confuse the next editor

- `InvoiceIssuedHandler` subscribed; `new InvoiceIssuedIntegrationEvent` in production: **zero**.  
- `ManualPaymentRecordedIntegrationEvent`: contract only, no handler.  
- `RevenueRecognitionJob` unregistered. If someone hosts it, `DeferredRevenueSchedules.Where(...)` **without** `IgnoreQueryFilters` sees 0 rows under empty worker tenant.  
- `ApiCreditPurchasedHandler` unregistered.  
- Recognition would write `REVENUE_RECOGNIZED`; summary `recognized_revenue` / `deferred_revenue` are almost always 0.

---

