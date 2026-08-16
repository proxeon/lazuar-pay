# W1-LP-154 — done

Resend webhook parser accepts `data.to` / `data.email.to` / `data.recipient` and tags as object **or** `{name,value}` array. `UNSUBSCRIBE` blocks marketing only; `BOUNCE` / `COMPLAINT` / `ANONYMIZED` block all mail. RFC 8058 `POST /public/communications/unsubscribe` works on the same query URL.

## Files

- `ResendWebhookParser` + `PublicComplianceEndpoints`
- `ISuppressionService` lane split; dispatch transactional; broadcast marketing
- `ResendWebhookParserTests`, `SuppressionLaneTests`, dispatch mock updated

## Tests run

- `ResendWebhookParserTests|SuppressionLaneTests|DispatchMessageIntegrationEventHandlerTests` — **passed**

Not committed. Not pushed.

Tracker `LP-154` **P → Y**.
