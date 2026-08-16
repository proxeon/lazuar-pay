# W2-LP-110 — done

B2B MyInvois submit is CRM-backed. `InvoiceIssued` no longer files stub TIN `C1234567890`. Paid B2B sales already publish `B2bTaxInvoiceRequested`; the Lhdn handler now requires a real TIN **and** `id_type`/`id_value` before `SubmitTaxDocument`. Standard/Credit/Consolidated UBL bind supplier postal address from `LhdnTenantConfig` (no Bangunan Merdeka). Ops Tax Invoices is remounted and GET `/lhdn/documents/{INV-…}` is the status source.

## Tests run

- `dotnet test … --filter FullyQualifiedName~MyInvoisLoopTests` — **ok**
- InvoiceIssued no-op; B2B no TIN skip; B2B CRM TIN submit; XML city not Merdeka

Not committed. Not pushed.

Tracker `LP-110` **B → Y** when a merchant checkout with real buyer TIN lands PENDING/SUBMITTED.
