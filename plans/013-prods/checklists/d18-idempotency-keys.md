# D18 — Idempotency keys

**Track:** Database · **Depends:** D17  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** Unique `(org_id, key)` → checkout id. Survives process restart.

---

## D18.1 Shape

- [ ] Unique `(org_id, key)` maps to the checkout `id`
- [ ] Table `idempotency_keys` **or** unique on `checkouts` where the key is present — pick one
- [ ] Replaces `CheckoutStore._idempotency` (`orgId + "\n" + key` in memory)
- [ ] Not an outbox. Not PSP `(org_id, provider, event_id)` (D23)

## D18.2 Behavior

- [ ] Second `POST /v1/checkouts` with the same `Idempotency-Key` + `org_id` returns the first session
- [ ] Survives process restart / new replica (shared DB)
- [ ] Existing `CheckoutTests.Create_idempotent_on_key` still holds

## D18.3 Refuse

- [ ] No MediatR inbox “to record the key”
- [ ] Do not reuse this key as the journal / webhook event id

## D18.4 Exit

- [ ] `Create_idempotent_on_key` green against the durable store
- [ ] Unblocked for D19
