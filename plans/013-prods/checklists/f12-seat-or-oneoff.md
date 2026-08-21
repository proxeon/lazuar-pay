# F12 — Seat or one-off

**Track:** Fulfillment · **Depends:** F11, D25  
**Analysis:** [07](../07-fulfillment-ledger-docs.md)  
**IDs:** NP-FUL-002  
**Goal:** Buyer access is a Pay row, not a One membership.

---

## F12.1 Interval

- [ ] If checkout interval is `mo` or `yr`: insert `subscriptions` status **ACTIVE** in the same transaction as `paid`
- [ ] Quantity ≥ 1. Do not invent `PAST_DUE` / `TRIALING` on this write
- [ ] If `one_off`: mark complete = paid checkout; **no** `subscriptions` row
- [ ] Do not recreate Hub `Order` + `OrderCompletedIntegrationEvent`

## F12.2 Access owner

- [ ] The subscription (or paid one-off checkout) **is** access
- [ ] Do not `POST` One to grant the buyer (members, `authz/write`, SCIM)
- [ ] Do not create a Zitadel human for the cardholder
- [ ] Staff membership stays One; buyer is not staff

## F12.3 Must not

- [ ] No `SubscriptionActivatedIntegrationEvent`
- [ ] No `FulfillmentTargets` in-process grant list

## F12.4 Exit

- [ ] `mo`/`yr` → one ACTIVE row; `one_off` → no subscription
- [ ] Unblocked for F19
