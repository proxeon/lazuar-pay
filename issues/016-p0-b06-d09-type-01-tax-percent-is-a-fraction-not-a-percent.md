---
number: "016"
id: B06-D09
severity: P0
status: open
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 016 — B06-D09 — Type `01` tax `Percent` is a fraction, not a percent

- **Severity:** P0
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D09 — Type `01` tax `Percent` is a fraction, not a percent (P0)

**Status:** open. 008 did not file this as a numbered defect.

```81:83:apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/B2bTaxInvoiceRequestedIntegrationEventHandler.cs
                    Tax_rate = @event.AmountExcludingTax == 0 ? 0 : (double)(@event.TaxAmount / @event.AmountExcludingTax),
```

If SST is 16 on 200, `Tax_rate = 0.08`. That value is copied into the view model (`ViewModelMapper.cs:96`) and emitted as:

- XML: `<cbc:Percent>{{ format_amount line.tax_rate }}</cbc:Percent>` (`StandardInvoice.xml:131`)
- JSON: `["Percent"] = line.TaxRate` (`UblJsonDocumentBuilder.cs:171`)

B2C consolidation does the **opposite** (correct percent):

```289:289:apps/lazuar-api/Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs
                    TaxRate: taxAmount > 0 ? Math.Round((taxAmount / grossRevenue) * 100, 2) : 0,
```

A product B2B sale with real SST is a realistic MyInvois INVALID even if TIN/ID are perfect. Tests submit `Tax_rate = 0` (`MyInvoisLoopTests.SamplePayload`) and never assert percent scale.

