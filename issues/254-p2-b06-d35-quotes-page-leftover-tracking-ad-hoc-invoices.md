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

## Evaluation (current tree, 2026-08-18)

### What the bug is
Ops Quotes is the custom-checkout / proforma list, not the Sales documents tax-invoice list. The page title and empty state already say quotes / proforma. The table toolbar still has leftover chrome “Tracking ad-hoc invoices.” In this product “invoice” is a loaded word (INV- tax invoice vs Official Receipt vs proforma). The leftover teaches the merchant that this page tracks invoices.

### Still present?
**STILL BROKEN**

```41:57:apps/lazuar-ops/src/modules/invoicing/pages/QuotesPage.tsx
    <PageLayout 
      title="Quotes & Proforma Invoices" 
      description="Create custom, one-off quotes and proforma invoices for ad-hoc services or B2B clients."
      ...
            <FileText size={14} /> Tracking ad-hoc invoices
```

Empty state at line 80 is already “No quotes found.” No other file contains `Tracking ad-hoc invoices`.

### Related files
- `apps/lazuar-ops/src/modules/invoicing/pages/QuotesPage.tsx` — the leftover string.
- `apps/lazuar-ops/src/modules/invoicing/components/CreateQuoteModal.tsx` — “Create Proforma Quote.”
- `apps/lazuar-ops/src/modules/invoicing/pages/TaxInvoicesPage.tsx` — actual sales documents / receipts / tax invoices (do not merge the copy).
- `apps/lazuar-portal/src/modules/checkout/components/QuoteView.tsx` — buyer heading “Proforma Invoice.”
- `issues/094-p1-b06-d06-ops-portal-teach-tax-invoice-valid-on-objects-that-are-not-clear.md` / `issues/141-…` — sibling “invoice” overclaims.

### Tests
- No ops frontend test or snapshot asserts this string (no `*.test.*` / `*.spec.*` hit).
- No test would fail if the leftover stayed.
- First regression: QuotesPage source contains “Quotes” / “proforma” in the toolbar and does not contain “Tracking ad-hoc invoices.”

### Reproduction today
Sign in to ops → Invoicing → Quotes & Requests. Read the grey mono toolbar on the table. It still says “Tracking ad-hoc invoices.”

### Blast radius
Merchant-facing copy only. Can be screenshot in a demo next to a real INV-. No money, no PII. Frequency: every Quotes page view.

### Suggested fix
Change the toolbar to “Tracking quotes & proforma” (or drop the chip). Do not rename paid INV- objects. No API change. No TypeSpec regen.

### Evaluation notes
Cheap leftover lie listed in 009 §10 item 10 with D07/D27/D28. Still P2. Not blocked. Not a 161–200 residual.

