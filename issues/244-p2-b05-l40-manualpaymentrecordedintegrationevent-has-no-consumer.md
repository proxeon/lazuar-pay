---
number: "244"
id: B05-L40
severity: P2
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 244 — B05-L40 — `ManualPaymentRecordedIntegrationEvent` has no consumer

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L40 — P2 — `ManualPaymentRecordedIntegrationEvent` has no consumer

Contract exists. Billing README §5 still lists “From B2B/Invoicing: `InvoiceIssuedIntegrationEvent`, `ManualPaymentRecordedIntegrationEvent`.” Manual **enrollment** is a different event and **is** consumed. The recorded-payment event is a lie in the README.

---

