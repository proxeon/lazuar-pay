# D23 — `psp_webhook_events`

**Track:** Database · **Depends:** D16  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** NP-GW-006. Unique `(org_id, provider, event_id)`. Retry no-ops have a row to hit.

---

## D23.1 Table

- [ ] `psp_webhook_events` (or equivalent name, **this** table)
- [ ] Unique `(org_id, provider, event_id)`
- [ ] `org_id` is One tenant id

## D23.2 Not One HMAC

- [ ] **Not** One HMAC delivery rows — that is **O17** (`one_webhook_events`)
- [ ] Different secret, different route later; different table now
- [ ] Do not share this unique key with D18 checkout idempotency

## D23.3 Refuse

- [ ] No `payments.PaymentWebhookLogs` copy
- [ ] No nine `InboxMessages` pairs
- [ ] Handler / signature verify is G18–G21, not this file

## D23.4 Exit

- [ ] Unique constraint exists on a clean migrate
- [ ] Unblocked for D24
