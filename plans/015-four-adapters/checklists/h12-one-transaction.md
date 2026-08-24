# H12 — Unique insert + fulfill in one DB transaction

**Track:** Harden · **Depends:** S17  
**Analysis:** [00](../00-what-must-be-done.md) §3.3; [014/08](../../014-evals/08-webhooks-secrets-fulfillment.md) §4.5  
**IDs:** NP-GW-006, NP-FUL-001  
**Goal:** A throw after the event row must not turn Stripe retry into a permanent no-op with no `RCPT-`.

---

## H12.1 Live today (must change)

- [ ] `WebhookEndpoints` currently `SaveChanges` the `PspWebhookEventRow` **then** calls `Fulfillment.FulfillPaidAsync` which `BeginTransactionAsync` on its own
- [ ] Change: **one** `BeginTransaction` (or one SaveChanges at the end) covering:
  1. unique insert `(orgId, provider, eventId)`
  2. `FulfillPaidAsync` body (paid, charge, journal, `RCPT-`, audit)
- [ ] Unique hit → 200 `{ duplicate: true }` **without** a second journal
- [ ] Fulfill throw → **rollback** the event row (H25)

## H12.2 InMemory tests

- [ ] `PayApiFactory` already ignores EF InMemory transaction warnings — still write the code as one TX so Postgres is correct
- [ ] Do not claim “same TX proven on 5435” from InMemory alone; hermetic still locks the call order

## H12.3 Must not

- [ ] Do not `PublishAsync` `GatewayPaymentCompletedIntegrationEvent`
- [ ] Do not 200 then fulfill in a background job
- [ ] Do not ACK 200 **before** the unique insert

## H12.4 Exit

- [ ] Replay after **success** still `{ duplicate: true }` and one document (`WebhookTests`)
- [ ] Unblocked for H13, H25, C19
