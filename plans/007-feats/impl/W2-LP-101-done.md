# W2-LP-101 — done

Customer-facing commercial numbers are per-org series from `GenerateNextSequenceNumberCommand`. Receipts stay `RCPT-yyyy-#####`. Quotes persist `QT-yyyy-#####` on the checkout session (same number on HTML + draft PDF). B2B sales allocate `INV-yyyy-#####` onto `CustomerDocumentNumber` before any MyInvois UUID. Refunds allocate `CN-yyyy-#####` on the contra ledger row. LHDN UUID is never the printed “No:”.

## Files

- `DocumentSeries` prefix helpers in Billing.Contracts
- `CheckoutSession.DocumentNumber` + Commerce migration `20260819120000_AddCheckoutSessionDocumentNumber`
- `CreateCustomCheckoutCommandHandler` allocates `QT-` once
- `GenerateDraftDocumentQueryHandler` uses persisted quote number
- `GatewayPaymentCompletedHandler` B2B `INV-`; refund handler `CN-`
- `LedgerEntry.AssignB2bInvoice` / `AssignCustomerDocumentNumber`

## Tests run

- `DocumentSeriesTests`, `LedgerEntryAndAccountTypesTests`, `GatewayPaymentCompletedHandlerTests`, `CreateCustomCheckoutAndInitiateSessionTests`, `GenerateDraftDocumentQueryHandlerTests` — **passed** (invoicing filter **58 passed**)

Not committed. Not pushed.

Tracker `LP-101` can move **P → Y**.
