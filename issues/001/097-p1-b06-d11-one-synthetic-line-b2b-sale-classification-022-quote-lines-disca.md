---
number: "097"
id: B06-D11
severity: P1
status: resolved
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
resolved_branch: fix/097-b2b-line-description
---

# 097 — B06-D11 — One synthetic line `"B2B sale"` / classification `022`; quote lines discarded

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/097-b2b-line-description`

Type 01 line description is the product/plan name from payment metadata, not the synthetic "B2B sale".

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D11 — One synthetic line `"B2B sale"` / classification `022`; quote lines discarded (P1)

**Status:** open.

```74:88:apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/B2bTaxInvoiceRequestedIntegrationEventHandler.cs
            Items = new List<LhdnItemDto>
            {
                new()
                {
                    Description = "B2B sale",
                    Classification_code = "022",
                    Quantity = 1,
                    Unit_price = (double)@event.AmountExcludingTax,
                    ...
                }
            },
```

Quote line items never reach UBL. Product name never reaches UBL. Ledger MSIC for B2B is also hardcoded `"022"` (`GatewayPaymentCompletedHandler.cs:69`) — that field is then reused as classification on cons lines (`B2cConsolidationJob.cs:286`). Classification `022` (e-commerce) is not an MSIC. Supplier MSIC in UBL is tenant config or `"00000"` (`ViewModelMapper.cs:42`).

