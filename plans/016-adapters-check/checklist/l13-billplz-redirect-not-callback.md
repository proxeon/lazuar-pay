# L13 — Billplz redirect ≠ callback

**Track:** Checkout origin · **Depends:** L11  
**Analysis:** [`../06-billplz-crosscheck.md`](../06-billplz-crosscheck.md) B14/B15  
**IDs:** —  
**Goal:** Callback stays public https `Pay:PublicBaseUrl`. Redirect may be the checkout SPA.

---

## L13.1 Live today

- [ ] `callback_url` = PublicBaseUrl + `/v1/webhooks/billplz/{orgId}?checkout_id=`
- [ ] `redirect_url` = localhost:5179 verifying (same as success default)

## L13.2 Change

- [ ] `redirect_url` = L11 success helper
- [ ] `callback_url` **unchanged** (PublicBaseUrl + fail-closed TryPublicBase)
- [ ] Localhost PublicBaseUrl still 400 on **start** (fb15)

## L13.3 Must not

- [ ] Do not send Billplz callback to `:5179`
- [ ] Do not send Billplz redirect to the tunnel unless CheckoutBaseUrl is that tunnel (unusual)

## L13.4 Exit

- [ ] Assert create body: callback host ≠ redirect host in a test where PublicBaseUrl is `https://pay.test.example` and CheckoutBaseUrl is `http://localhost:5179`
