# H12 — Unique insert + fulfill in one DB transaction

**Track:** Harden · **Depends:** S17  
**Analysis:** [00](../00-what-must-be-done.md) §3.3; [014/08](../../014-evals/08-webhooks-secrets-fulfillment.md) §4.5  
**IDs:** NP-GW-006, NP-FUL-001  
**Goal:** A throw after the event row must not turn Stripe retry into a permanent no-op with no `RCPT-`.

---

## H12.1 Live today (must change)

- [x] `WebhookEndpoints` currently `SaveChanges` the `PspWebhookEventRow` **then** calls `Fulfillment.FulfillPaidAsync` which `BeginTransactionAsync` on its own
- [x] Change: **one** `BeginTransaction` (or one SaveChanges at the end) covering:
  1. unique insert `(orgId, provider, eventId)`
  2. `FulfillPaidAsync` body (paid, charge, journal, `RCPT-`, audit)
- [x] Unique hit → 200 `{ duplicate: true }` **without** a second journal
- [x] Fulfill throw → **rollback** the event row (H25)

## H12.2 InMemory tests

- [x] `PayApiFactory` already ignores EF InMemory transaction warnings — still write the code as one TX so Postgres is correct
- [x] Do not claim “same TX proven on 5435” from InMemory alone; hermetic still locks the call order

## H12.3 Must not

- [x] Do not `PublishAsync` `GatewayPaymentCompletedIntegrationEvent`
- [x] Do not 200 then fulfill in a background job
- [x] Do not ACK 200 **before** the unique insert

## H12.4 Exit

- [x] Replay after **success** still `{ duplicate: true }` and one document (`WebhookTests`)
- [x] Unblocked for H13, H25, C19
