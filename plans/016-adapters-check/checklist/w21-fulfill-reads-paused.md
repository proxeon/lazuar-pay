# W21 — Fulfillment does not book when paused

**Track:** One HMAC · **Depends:** W18  
**Analysis:** [`../10-honesty-frontend-risks.md`](../10-honesty-frontend-risks.md) P0-B; live `Fulfillment` ignores pause  
**IDs:** —  
**Goal:** In-flight hosted sessions cannot mint `RCPT-` after suspend.

---

## W21.1 Live today

- [ ] Start 403s when paused (keep, I14)
- [ ] `FulfillPaidAsync` does not read `OrgSettings.ChargesPaused`

## W21.2 Change

- [ ] Load org settings for `checkout.OrgId`
- [ ] If `ChargesPaused` → **do not** set paid, **do not** write charge/journal/document
- [ ] Return without throwing **or** throw a dedicated exception the webhook handler maps — W22 decides HTTP and unique insert
- [ ] This method must not insert `psp_webhook_events` (handler owns that)

## W21.3 Must not

- [ ] Do not delete the checkout
- [ ] Do not treat pause as `expired`

## W21.4 Exit

- [ ] Unblocked for W22, W24
