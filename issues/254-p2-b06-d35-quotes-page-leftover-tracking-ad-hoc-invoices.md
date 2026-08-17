---
number: "254"
id: B06-D35
severity: P2
status: open
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 254 — B06-D35 — Quotes page leftover “Tracking ad-hoc invoices”

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D35 — Quotes page leftover “Tracking ad-hoc invoices” (P2)

`QuotesPage.tsx:42–43` title is honest (“Quotes & Proforma Invoices”). Line 57 still says “Tracking ad-hoc invoices.” Word “invoices” should not be there.

