# K13 — Poll public GET after return from PSP

**Track:** Checkout UI · **Depends:** K14  
**Analysis:** [00](../00-what-must-be-done.md) §3.6 / §6.2; 014 K19 hole  
**IDs:** NP-CHK-004  
**Goal:** Buyer returning from Stripe/CHIP/Billplz sees verifying → paid, not the Pay form again.

---

## K13.1 Live today

- [x] `StripeHosted` success URL appends `?status=verifying`
- [x] `App.tsx` never reads the query and GETs once

## K13.2 Change

- [x] If `status=verifying` **or** after redirect, poll `GET /v1/pay/{token}` every ~2s, cap ~30s
- [x] States: loading / open (Pay) / verifying / paid / expired / missing / error
- [x] Paid copy already honest (Official Receipt, not membership)
- [x] Stop polling on paid/expired/missing

## K13.3 Must not

- [x] Do not treat query param as paid (K14)
- [x] Do not add OIDC to poll

## K13.4 Exit

- [x] Verifying UI exists
- [x] Unblocked for K14
