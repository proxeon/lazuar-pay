# K13 — Poll public GET after return from PSP

**Track:** Checkout UI · **Depends:** K14  
**Analysis:** [00](../00-what-must-be-done.md) §3.6 / §6.2; 014 K19 hole  
**IDs:** NP-CHK-004  
**Goal:** Buyer returning from Stripe/CHIP/Billplz sees verifying → paid, not the Pay form again.

---

## K13.1 Live today

- [ ] `StripeHosted` success URL appends `?status=verifying`
- [ ] `App.tsx` never reads the query and GETs once

## K13.2 Change

- [ ] If `status=verifying` **or** after redirect, poll `GET /v1/pay/{token}` every ~2s, cap ~30s
- [ ] States: loading / open (Pay) / verifying / paid / expired / missing / error
- [ ] Paid copy already honest (Official Receipt, not membership)
- [ ] Stop polling on paid/expired/missing

## K13.3 Must not

- [ ] Do not treat query param as paid (K14)
- [ ] Do not add OIDC to poll

## K13.4 Exit

- [ ] Verifying UI exists
- [ ] Unblocked for K14
