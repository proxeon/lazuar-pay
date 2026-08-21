# O17 — `one_webhook_events` table

**Track:** One extras · **Depends:** O14  
**Analysis:** [08](../08-one-identity-production.md) §7.1, §7.4  
**Goal:** Idempotent One deliveries, distinct from PSP.

---

## O17.1 Table

- [ ] `one_webhook_events` in the Pay DB (same migrator as D10)
- [ ] **Not** `psp_webhook_events` (D23)
- [ ] Unique on One event id (`X-Lazuar-Event-Id` / envelope `id`)

## O17.2 Idempotency

- [ ] Second POST same event id: 2xx, no second apply
- [ ] Do **not** use `X-Lazuar-Delivery-Id` as the idempotency key
- [ ] Replay of `tenant.suspended` does not double-pause in a harmful way

## O17.3 Must not

- [ ] Share unique `(org_id, provider, event_id)` with Stripe
- [ ] MediatR / outbox-to-self to “process later”

## O17.4 Exit

- [ ] Table + replay test
- [ ] O track complete; **unblocked for B99**
