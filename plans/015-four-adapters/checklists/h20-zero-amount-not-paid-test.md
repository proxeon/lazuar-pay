# H20 — Hermetic Stripe amount 0 is not paid

**Track:** Harden · **Depends:** H19  
**Analysis:** [00](../00-what-must-be-done.md) §3.6  
**IDs:** NP-GW-008, F17  
**Goal:** `AmountTotal` 0 / null does not mint `RCPT-` even if `mode=payment`.

---

## H20.1 Fixture

- [ ] Signed `checkout.session.completed` with `mode=payment` and `amount_total: 0` (or null)
- [ ] Open checkout with amount > 0 exists (must **not** be paid from this event)
- [ ] Assert ignored, zero documents, checkout still `open`

## H20.2 Belt

- [ ] `Fulfillment` already returns early if `checkout.Amount <= 0` — keep
- [ ] This test is the **PSP** zero, not the checkout-row zero

## H20.3 Exit

- [ ] Test green
- [ ] Unblocked for C21 (CHIP preauthorized uses the same “not paid” idea)
