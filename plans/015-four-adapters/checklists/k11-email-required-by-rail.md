# K11 — Require email when the rail needs it

**Track:** Checkout UI · **Depends:** P19  
**Analysis:** [00](../00-what-must-be-done.md) §6.2  
**IDs:** NP-BUY-001  
**Goal:** CHIP/Billplz/Xendit/Razorpay do not 400 after the buyer clicked Pay with an empty email if the UI could have blocked.

---

## K11.1

- [x] Public GET may return `email_required: true` based on `checkout.Provider` or org `active_provider` — add if cheap
- [x] If email required, disable Pay until email non-empty (and not placeholder)
- [x] Stripe may keep email optional
- [x] Still no TIN field

## K11.2 Exit

- [x] UI matches P19
