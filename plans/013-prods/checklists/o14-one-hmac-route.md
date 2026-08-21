# O14 — One HMAC route

**Track:** One extras · **Depends:** D29 or O17 table  
**Analysis:** [08](../08-one-identity-production.md) §7  
**IDs:** NP-ONE-017  
**Goal:** A Pay door for One push, not the PSP webhook.

---

## O14.1 Route

- [ ] `POST /v1/one/webhooks` on **8081** (write this path into [`decisions.md`](./decisions.md) if not already locked)
- [ ] **Different** from G18 `POST /v1/webhooks/{provider}/{orgId}`
- [ ] No Bearer; HMAC header (`X-Lazuar-Signature` or the name One actually sends)
- [ ] Not Hub `/api/v1/webhooks/payments/{gateway}`; not `/one/*` inside Pay

## O14.2 Persist

- [ ] Need `one_webhook_events` (O17) — **prefer O14+O17 same commit** if D29 did not already add it
- [ ] Do not reuse `psp_webhook_events` (D23)

## O14.3 Must not

- [ ] No JWT / PAT on this door
- [ ] Do not verify Stripe with One `whsec_`
- [ ] Do not tail Zitadel

## O14.4 Exit

- [ ] Route exists (verify may still be O15)
- [ ] Unblocked for O15 and O17 (or both in the same tip)
