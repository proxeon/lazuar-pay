# W2-LP-111 — done

Poller still owns VALID/INVALID. INVALID now publishes `LhdnDocumentValidated` (`Status=INVALID`) so Billing can update the ledger. Join is `CustomerDocumentNumber` **or** `TaxInvoiceId` **or** `ReferenceId`. INVALID does not generate a Tax Invoice PDF. Ops panel polls GET `/lhdn/documents/{document number}` and does not treat SUBMITTED as success.

## Tests run

- `LhdnDocumentValidatedIntegrationEventHandlerTests` — VALID by INV-, CONS batch, INVALID no PDF, unknown id — **ok**

Not committed. Not pushed.

Tracker `LP-111` **B → Y** when a sandbox VALID invoice shows VALID on the remounted ops list without SQL.
