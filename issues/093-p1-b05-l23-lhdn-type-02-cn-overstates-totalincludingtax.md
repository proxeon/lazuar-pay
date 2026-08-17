---
number: "093"
id: B05-L23
severity: P1
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 093 — B05-L23 — LHDN type-02 CN overstates `Total_including_tax`

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L23 — P1 — LHDN type-02 CN overstates `Total_including_tax`

`Lhdn GatewayRefundCompletedIntegrationEventHandler.cs:126-135`: `Unit_price = RefundedAmount`, `Total_excluding_tax = RefundedAmount`, `Total_including_tax = RefundedAmount + TaxAmount`. `RefundedAmount` is Commerce cash (gross). Adding tax again is wrong when `TaxAmount > 0`. Today ops sends 0, so the CN is tax-free and **understates** SST reverse vs the Billing journal (which did scale tax from the original payment). Both directions are wrong depending on whether anyone starts sending `tax_amount`. ≥72h also meters 3 LHDN credits for a CN the merchant did not click.

Slice 06 owns the XML. The amounts are born on this Completed event.

---

