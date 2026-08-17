---
number: "253"
id: B06-D34
severity: P2
status: open
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 253 — B06-D34 — Stationery empty TIN is omitted, not “TIN not on file”

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D34 — Stationery empty TIN is omitted, not “TIN not on file” (P2)

Factory fallback seller name is workspace name, then `"Merchant"` (`InvoiceDocumentFactory.cs:30`). Empty TIN is omitted (`BaseInvoiceDocument.cs:50–51`). W2-LP-107-done.md’s “TIN not on file” string is not in the factory. `InvoiceDocumentFactoryTests` locks “not Lazuar Merchant.” That part of the done-file is still overstated. Not a customer-facing lie today.

Legal profile Card 2 never auto-provisions. Submit without config throws “LHDN Tenant Configuration is missing.” (`SubmitTaxDocumentCommand.cs:99–103`). Seed genesis row is a hardcoded org + sandbox-looking TIN (`LhdnDbContext.cs:47–61`). Irrelevant unless that GUID is a live tenant.

