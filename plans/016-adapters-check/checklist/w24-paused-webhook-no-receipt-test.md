# W24 — Paused org + valid paid webhook: no receipt

**Track:** One HMAC · **Depends:** W21, W22, W23  
**Analysis:** [`../09-tests-inventory.md`](../09-tests-inventory.md) T3  
**IDs:** —  
**Goal:** Plane B cannot pay a suspended org.

---

## W24.1 Method `WebhookTests.Paused_org_does_not_mint_receipt` (Stripe path is enough)

- [ ] Seed rail + open checkout amount 10
- [ ] Set `OrgSettings.ChargesPaused = true` (SQL/EF; HMAC path covered in W23)
- [ ] POST signed Stripe `checkout.session.completed` that would pay if unpaused
- [ ] Assert HTTP 409 or 403 (match W22)
- [ ] Assert `Documents.Count == 0`, checkout `open`
- [ ] Assert no `PspWebhookEvents` row for that `evt_`

## W24.2 Retry after unsuspend

- [ ] Same method or sibling `Paused_then_unpaused_retry_pays`: set paused false, POST same payload → 200, one `RCPT-`

## W24.3 Must not

- [ ] Do not use `{ duplicate: true }` as the paused response
- [ ] Do not only test Start 403 (that already exists as I14)

## W24.4 Exit

- [ ] Green hermetic
- [ ] Unblocked for A99.2 pause bullet
