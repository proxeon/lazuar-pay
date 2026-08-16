# W2-LP-116 — done

Ops cancel uses `customer_document_number` (INV-/RCPT-/B2C-CONS-), not the ledger GUID. The 72h clock uses MyInvois `validated_at`, not ledger timestamp. Copy is **cancel only** — buyer reject is not sold. Billing cancel handler joins the same document-number keys. Integrator `POST /lhdn/documents/{internalId}/cancel` unchanged.

## Tests run

- `CancelTaxDocumentCommandTests` — VALID in window, 80h refuse, unknown id — **ok**

Not committed. Not pushed.

Tracker `LP-116` **B → P** (cancel Y, reject N).
