# W30 — Optional `webhook.test` ping

**Track:** W · **Depends:** W21  
**Analysis:** [`../03-outbound-webhooks.md`](../03-outbound-webhooks.md) §9.1  
**Goal:** Register can be proved without Stripe. Skip if time-boxed — hatch is complete without this.

**Why:** One has “test ping.” Useful for E14 before a Test start. K99a does **not** require it; Test rail fulfill is enough.

**Related files**

| Path | Role today |
|------|------------|
| W14 register | Endpoint must exist |
| W21 worker | Same ProcessBatch |
| Sibling One webhook test | Judgment |

**Current (`6d730d15`):** N/A.

---

## W30.1

- [ ] Writer `POST /v1/orgs/{orgId}/webhooks/test` enqueues `webhook.test` with tiny `{ ok: true }`
- [ ] Same worker path
- [ ] 404 if no endpoint
- [ ] Spec only if mapped
- [ ] Tests: ProcessBatch POSTs type `webhook.test`

## W30.2 Must not

- [ ] Do not require ping for fulfill

## W30.3 Exit

- [ ] Track W complete with or without W30; K99a does not require ping
