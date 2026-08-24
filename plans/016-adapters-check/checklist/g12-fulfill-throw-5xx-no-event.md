# G12 — Fulfill throw → 5xx, event row absent

**Track:** Prove Beat 1 · **Depends:** G11  
**Analysis:** 09 method `WebhookTests.Fulfill_throw_returns_5xx_event_not_committed_retry_pays` first half  
**IDs:** H25  
**Goal:** Stripe retry is still valid.

---

## G12.1

- [ ] First POST signed paid → 5xx
- [ ] `Documents.Count == 0`
- [ ] `PspWebhookEvents` for that EventId **absent**
- [ ] Checkout still `open`

## G12.2 Must not

- [ ] Do not 200 `{ duplicate: true }`
- [ ] If G11 was skipped: **do not write a lying test**

## G12.3 Exit

- [ ] Green **or** explicit skip comment citing H25.2
- [ ] Unblocked for G13
