---
number: "076"
id: B05-L06
severity: P1
status: resolved
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
resolved_branch: fix/076-b2b-tax-resolved-sst
---

# 076 — B05-L06 — B2B MyInvois tax is raw `event.TaxAmount`, not resolved SST

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/076-b2b-tax-resolved-sst`

B2bTaxInvoiceRequested uses resolved SST (event tax or sst_tax_amount metadata), not the raw gateway TaxAmount.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L06 — P1 — B2B MyInvois tax is raw `event.TaxAmount`, not resolved SST

**Where.** `GatewayPaymentCompletedHandler` publishes `B2bTaxInvoiceRequestedIntegrationEvent(..., grossRevenue, @event.TaxAmount, ...)`. `B2bTaxInvoiceRequestedIntegrationEventHandler` copies that into `Total_tax` / line `Tax_amount`. Stripe session tax is usually 0. Ledger used metadata. Legal invoice excluding+tax ≠ cash. Slice 06 owns XML; the **wrong number is born in Billing**.

---

