---
number: "249"
id: B06-D30
severity: P2
status: open
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 249 — B06-D30 — Draft proforma identity and date are thin

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D30 — Draft proforma identity and date are thin (P2)

Draft customer is session CRM `FullName` + email only (`CommerceDocumentLookup.cs:86–89`, `GenerateDraftDocumentQueryHandler.cs:66–68`). TIN is not printed. Issue date is `DateTime.UtcNow` at download (`73`), not session created-at — the date **moves** on every click. Currency is hardcoded `MYR` (`78`). Quote SST does not exist (CreateQuoteModal totals `qty * unit_price` only).

