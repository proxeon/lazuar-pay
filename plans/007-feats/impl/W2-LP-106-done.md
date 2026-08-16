# W2-LP-106 — done

Buyers get HMAC downloads only when a URL exists. No `/api/billing/invoice?subscription=` href. Portal subscription cards show **Download receipt / tax invoice** from `document_url` + `document_label`. Quote draft PDF uses existing HMAC `draft_pdf_url` on restored `/pay/{id}`. Tax Invoice / Credit Note `DocumentPublished` emails reuse the Official Receipt template when a dedicated template is missing.

## Files

- `PortalSubscriptionDto.document_url` / `document_label`
- `PortalDocumentQueryService` attach-latest
- Portal `page.tsx` un-hide download only if URL present
- `DocumentPublishedIntegrationEventHandler` Tax Invoice / Credit Note

## Tests run

- `DocumentPublishedIntegrationEventHandlerTests` Tax Invoice fallback — **passed**
- `npx tsc --noEmit` portal — **clean**

Not committed. Not pushed.

Tracker `LP-106` can move **B → Y** together with LP-175 (history list).
