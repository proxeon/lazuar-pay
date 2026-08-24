# M17 — Billplz webhook copy is not the callback

**Track:** Merchant · **Depends:** L17  
**Analysis:** `<code>{payApi}/v1/webhooks/billplz/{orgId}` vs start `Pay:PublicBaseUrl` + `?checkout_id=`  
**IDs:** B29  
**Goal:** Staff must not paste localhost 8081 into Billplz as the bill callback.

---

## M17.1

- [ ] For billplz, extra sentence: “Dashboard callback is registered at start from `Pay:PublicBaseUrl` (public https). This URL is the path shape; localhost will fail.”
- [ ] Keep path template for CHIP/Stripe/Xendit/Razorpay (those dashboards want that path on a tunnel)

## M17.2 Must not

- [ ] Do not print `Pay:PublicBaseUrl` from Vite (SPA does not have it)
- [ ] Do not add `?checkout_id=` to the hint (per-checkout, unknown at paste time)

## M17.3 Exit

- [ ] Copy exists next to billplz fields
