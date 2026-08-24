# K10 — Placeholder email blocked on `:5179`

**Track:** Checkout · **Depends:** I15  
**Analysis:** SPA only `!email.trim()`; host `BuyerEmail.IsUsable`  
**IDs:** P20 / K11  
**Goal:** Buyer cannot click Pay with `customer@example.com`.

---

## K10.1 Live today

- [ ] `emailBlocked = email_required && !email.trim()`

## K10.2 Change

- [ ] Unusable if trim empty **or** lowercased equals `customer@example.com`
- [ ] Disable Pay; optional alert “email is required”

## K10.3 Must not

- [ ] Do not invent a second placeholder string
- [ ] Stripe optional: if `email_required` is false, placeholder may still be sent — host allows Stripe without usable email. Do not 400 Stripe in the SPA

## K10.4 Exit

- [ ] Unblocked for K17
