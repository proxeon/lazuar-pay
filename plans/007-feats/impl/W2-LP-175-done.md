# W2-LP-175 — done

Magic-link buyers see a **Documents** table under subscriptions: date, number (`RCPT-`/`INV-`/`CN-`/`QT-`), type, amount, status, HMAC `download_url`. `GET /public/commerce/{slug}/portal/documents?token=` is token-bound (same magic token as portal). Foreign tenant slug is 404. Rows come from ledger entries for that buyer’s CRM email / profile plus matching quote sessions.

## Files

- TypeSpec `PortalDocumentDto` + public GET
- `PortalDocumentQueryService` + `PublicPortalEndpoints`
- Portal documents table

## Tests run

- TypeSpec gen + `npx tsc --noEmit` portal — **clean**
- Related HMAC / billing lookup tests in the invoicing filter — **58 passed**

Not committed. Not pushed.

Tracker `LP-175` can move **B → Y**.
