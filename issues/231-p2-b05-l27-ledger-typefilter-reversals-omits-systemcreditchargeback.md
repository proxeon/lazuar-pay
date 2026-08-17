---
number: "231"
id: B05-L27
severity: P2
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 231 — B05-L27 — Ledger `type_filter=reversals` omits `SYSTEM_CREDIT_CHARGEBACK`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L27 — P2 — Ledger `type_filter=reversals` omits `SYSTEM_CREDIT_CHARGEBACK`

`BillingQueryService.cs:56-63`: `reversals` = `GATEWAY_REFUND` + `LHDN_CANCELLATION` only. Credit Notes page is this filter. Utility chargebacks do not appear. `sales` excludes those two and therefore **includes** chargebacks, SaaS fees, top-ups, commissions, zero-checkouts.

---

