---
number: "017"
id: B06-D19
severity: P0
status: resolved
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
resolved_branch: fix/017-credit-note-tax-totals
---

# 017 — B06-D19 — Type `02` credit note UBL can double-count tax

- **Severity:** P0
- **Status:** resolved
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/017-credit-note-tax-totals`

Credit-note totals treat refunded amount as gross. SST is not added twice. Tax type is SST (`02`) when tax is present.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D19 — Type `02` credit note UBL can double-count tax (P0)

**Status:** open.

```119:136:apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/GatewayRefundCompletedIntegrationEventHandler.cs
            Items = new List<LhdnItemDto>
            {
                new()
                {
                    Description = "Refund",
                    Classification_code = "022",
                    Quantity = 1,
                    Unit_price = (double)@event.RefundedAmount,
                    Tax_rate = 0,
                    Tax_amount = (double)@event.TaxAmount,
                    Subtotal = (double)@event.RefundedAmount,
                    Tax_type_code = LhdnItemDtoTax_type_code._06
                }
            },
            Total_excluding_tax = (double)@event.RefundedAmount,
            Total_tax = (double)@event.TaxAmount,
            Total_including_tax = (double)(@event.RefundedAmount + @event.TaxAmount)
```

`RefundedAmount` on the payments event is the money that left the gateway — typically **gross**. Adding `TaxAmount` again makes `Total_including_tax` larger than the refund. `Tax_type_code` is `_06` (not applicable) while `Tax_amount` may be non-zero. CreditNote.xml still has `<cbc:Percent>0</cbc:Percent>` and BillingReference `<cbc:ID>NA</cbc:ID>` (`CreditNote.xml:27–28`) with only the original UUID filled. That `NA` original document number is a realistic INVALID even when the UUID is right.

`GatewayRefundCompletedIntegrationEventHandlerTests.FullRefund_After72h_SubmitsCreditNoteWithCrmTin` asserts type `_02`, CN number, buyer TIN. It does **not** assert totals.

