# W2-LP-103 — done

B2B paid sales get an `INV-` number and a **Tax Invoice** PDF on pay (not only after VALID). Ops **Sales documents** lists receipts + tax invoices. VALID lookup matches `CustomerDocumentNumber` / `TaxInvoiceId` / `ReferenceId` (consolidation batch updates those rows; individual receipts keep `RCPT-`). Submit hook is `B2bTaxInvoiceRequested` consumed by Lhdn — **not** `InvoiceIssued`. Stub TIN `C1234567890` is never submitted.

## Files

- Ops `/invoicing/tax-invoices` remounted as Sales documents
- `GatewayPaymentCompletedHandler` B2B PDF + `B2bTaxInvoiceRequestedIntegrationEvent`
- `B2bTaxInvoiceRequestedIntegrationEventHandler` (CRM TIN required)
- `LhdnDocumentValidatedIntegrationEventHandler` multi-key lookup

## Tests run

- `GatewayPaymentCompletedHandlerTests` B2B case, `B2bTaxInvoiceRequestedIntegrationEventHandlerTests`, `LhdnDocumentValidatedIntegrationEventHandlerTests` — **passed**

Not committed. Not pushed.

Tracker `LP-103` can move **B → P**. **Y** still needs LP-110/111/022 for sandbox VALID + QR.
