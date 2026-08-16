# W2-LP-104 — done

Ops Credit Notes lists automated reversals (no composer). Full refund of a VALID e-invoice ≤72h sends `CancelTaxDocumentCommand`. After 72h, type `02` goes through `SubmitTaxDocumentCommand` with CRM buyer TIN and `CN-yyyy-#####`. No stub `IG1234567890`. Partial refunds still skip LHDN. Original TaxDocument is resolved via payment ledger number / UUID, not Commerce payment GUID.

## Files

- Ops `/invoicing/credit-notes`
- Lhdn `GatewayRefundCompletedIntegrationEventHandler` rewrite
- Billing refund handler allocates `CN-`
- `ILhdnRepository.GetTaxDocumentByLhdnUuidAsync`

## Tests run

- Lhdn refund handler tests (partial / no-doc / 72h cancel / 80h CN) — **passed**
- `GatewayRefundCompletedHandlerTests` / matrix refund — **passed**

Not committed. Not pushed.

Tracker `LP-104` can move **B → P**. **Y** when sandbox cancel/CN is proven against a real UUID.
