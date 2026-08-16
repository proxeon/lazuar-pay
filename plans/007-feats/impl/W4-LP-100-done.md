# W4-LP-100 — done

Official Receipt PDFs no longer print `TIN: N/A` / `TIN not on file`. Empty TIN is omitted. Receipts get a notes + footer disclaimer that they are **not** MyInvois tax invoices. Tax invoices are unchanged.

## Files

- `InvoiceDocumentFactory` — empty TIN; Official Receipt notes
- `BaseInvoiceDocument` — footer “Payment receipt. Not an LHDN e-invoice.”
- `InvoiceDocumentFactoryTests` — 4 passed
- `README.md` — receipt ≠ e-invoice

Tracker `LP-100` **P → Y**.
