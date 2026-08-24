# G15 — Stripe amount mismatch: 400, no event row

**Track:** Prove Beat 1 · **Depends:** D17  
**Analysis:** 09 `WebhookTests.Amount_mismatch_does_not_mint_receipt`  
**IDs:** H14  
**Goal:** 999 sen vs RM10 does not mint `RCPT-` and does not consume `evt_`.

---

## G15.1

- [ ] Seed checkout amount **10**
- [ ] Signed completed `mode=payment` `amount_total:999`
- [ ] 400, `Documents.Count == 0`, checkout `open`
- [ ] **No** `PspWebhookEvents` row for that event id

## G15.2 Must not

- [ ] Do not use 1000 (that is the happy path)

## G15.3 Exit

- [ ] Green; fs11 may be this same method (do not duplicate)
