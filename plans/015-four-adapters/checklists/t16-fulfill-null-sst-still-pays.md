# T16 — Hermetic: null SST still mints RCPT-

**Track:** Tax · **Depends:** T10, T13  
**Analysis:** [00](../00-what-must-be-done.md) §7  
**IDs:** —  
**Goal:** Prove the SST throw is gone. Paid path works with `SstRegistered` null.

---

## T16.1 Test

- [ ] Add a test (extend `WebhookTests` or new `FulfillmentTests`)
- [ ] Seed org_settings **without** `SstRegistered` (null), or skip the row entirely
- [ ] `POST` a signed Stripe `checkout.session.completed` with `mode=payment` and `amount_total` > 0
- [ ] Assert HTTP 200
- [ ] Assert one `documents` row, `Number` starts with `RCPT-`, `Title` is Official Receipt
- [ ] Assert journal debit sum equals credit sum
- [ ] Assert checkout `status` is `paid`
- [ ] Assert response/logs do not contain `SST registration unknown`

## T16.2 Must not

- [ ] Do not call live Stripe
- [ ] Do not use Hub module test helpers or `Modules.Payments`

## T16.3 Exit

- [ ] `task pay:test` includes T16 and is green
- [ ] Unblocked for T18
