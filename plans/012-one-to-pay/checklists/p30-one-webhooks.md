# P30 — One webhooks (parked)

**Do not start until C99. Mandatory before live charges, not before whoami.**  
**Analysis:** [09](../09-webhooks-events.md)

---

## P30.1 Prefer

- [ ] HMAC **push** to Pay `POST /v1/one/webhooks` on **8081**
- [ ] Secret `whsec_…` shown once, stored in Pay
- [ ] Idempotent on One’s event id header

## P30.2 First events that matter for money

- [ ] `tenant.suspended` / `tenant.reactivated` — stop charges
- [ ] Also fail closed on `GET /tenants/{id}` status at charge time (webhook can be late)

## P30.3 Defer even inside P30

- [ ] `member.*` — `/me` remains SoT; JIT joins may not emit `member.accepted`
- [ ] `tenant.created` — lazy upsert on first Pay write
- [ ] Do not grant **buyer** entitlement in One

## P30.4 Must not

- [ ] Tail Zitadel
- [ ] Block C13 on webhooks
- [ ] Treat a down audit/notify service as this work
