---
number: "141"
id: B09-U12
severity: P1
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 141 — B09-U12 — Sales documents paint receipts as e-invoices

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U12 — Sales documents paint receipts as e-invoices (P1)

**Where:** `TaxInvoicesPage.tsx` 116–117, 168, 193–209; `TaxInvoiceDetailPanel.tsx` 151, 279–288; contrast `PortalDocumentQueryService.Classify` 189–200.  
**What:** Empty “No tax invoices found.” Type = B2C/B2B. Panel title “Tax Document Details.” Cancel e-Invoice on VALID. Portal already knows “Official Receipt.” Ops does not.  
**Walk:** First B2C sale. Merchant opens Sales documents. They think they issued an e-invoice. They did not.

