# T16 — Hermetic: null SST still mints RCPT-

**Track:** Tax · **Depends:** T10, T13  
**Analysis:** [00](../00-what-must-be-done.md) §7  
**IDs:** —  
**Goal:** Prove the SST throw is gone. Paid path works with `SstRegistered` null.

---

## T16.1 Test

- [x] Add a test (extend `WebhookTests` or new `FulfillmentTests`)
- [x] Seed org_settings **without** `SstRegistered` (null), or skip the row entirely
- [x] `POST` a signed Stripe `checkout.session.completed` with `mode=payment` and `amount_total` > 0
- [x] Assert HTTP 200
- [x] Assert one `documents` row, `Number` starts with `RCPT-`, `Title` is Official Receipt
- [x] Assert journal debit sum equals credit sum
- [x] Assert checkout `status` is `paid`
- [x] Assert response/logs do not contain `SST registration unknown`

## T16.2 Must not

- [x] Do not call live Stripe
- [x] Do not use Hub module test helpers or `Modules.Payments`

## T16.3 Exit

- [x] `task pay:test` includes T16 and is green
- [x] Unblocked for T18
